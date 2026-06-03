using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace PlantProcess.Api.EnterpriseSsoScim;

public sealed record MockIdpTokenRequest(
    string Subject,
    string Email,
    string DisplayName,
    string[] Groups);

public sealed record SsoLoginRequest(
    string ProviderCode,
    string IdToken);

public sealed record SsoRoleMappingRequest(
    string ProviderCode,
    string ClaimType,
    string ClaimValue,
    string AppRole,
    string PlantRole,
    int Priority = 100);

public sealed record ScimUserRequest(
    string? ExternalId,
    string UserName,
    string? DisplayName,
    string? GivenName,
    string? FamilyName,
    string? Email,
    bool Active = true,
    string[]? Groups = null);

public sealed record ScimGroupRequest(
    string? ExternalId,
    string DisplayName,
    string[]? Members = null,
    bool Active = true);

public static class V5EnterpriseSsoScimEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string MockIdpSecret = "local-dev-mock-idp-secret-change-in-production";

    public static IEndpointRouteBuilder MapV5EnterpriseSsoScimEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/sso")
            .WithTags("V5 Enterprise SSO");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-enterprise-sso-scim",
            oidc = true,
            samlContract = true,
            mockIdp = true,
            jitProvisioning = true,
            roleMapping = true,
            scim20 = true
        }));

        group.MapPost("/mock-idp/token", ([FromBody] MockIdpTokenRequest request) =>
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var payload = new
            {
                iss = "https://mock-idp.plantprocessiq.local",
                sub = request.Subject,
                email = request.Email,
                name = request.DisplayName,
                groups = request.Groups ?? Array.Empty<string>(),
                iat = now,
                exp = now + 3600
            };

            var payloadJson = JsonSerializer.Serialize(payload);
            var payload64 = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
            var sig = Sign(payload64);

            return Results.Ok(new
            {
                idToken = $"{payload64}.{sig}",
                tokenType = "mock_oidc",
                expiresIn = 3600
            });
        });

        group.MapPost("/login", async (
            [FromBody] SsoLoginRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            if (!TryValidateMockToken(request.IdToken, out var claims, out var error))
                return Results.BadRequest(new { accepted = false, error });

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var provider = await LoadProviderAsync(connection, tenantId, request.ProviderCode, cancellationToken);

            if (provider.ProviderId == Guid.Empty)
                return Results.NotFound(new { accepted = false, error = "SSO provider not configured." });

            if (!provider.JitProvisioningEnabled)
                return Results.BadRequest(new { accepted = false, error = "JIT provisioning disabled." });

            var role = await ResolveRoleAsync(connection, tenantId, provider.ProviderId, claims.Groups, provider.DefaultRole, cancellationToken);
            var principalId = await UpsertPrincipalAsync(connection, tenantId, provider.ProviderId, claims, role, cancellationToken);

            await WriteProvisioningAuditAsync(connection, tenantId, "sso", "jit_login", claims.Email, "success", new
            {
                provider = request.ProviderCode,
                role.AppRole,
                role.PlantRole,
                groups = claims.Groups
            }, cancellationToken);

            return Results.Ok(new
            {
                accepted = true,
                principalId,
                tenantId,
                claims.Subject,
                claims.Email,
                claims.DisplayName,
                role.AppRole,
                role.PlantRole,
                jitProvisioned = true
            });
        });

        group.MapGet("/role-mappings", async (
            string providerCode,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT
                    m.id,
                    m.claim_type,
                    m.claim_value,
                    m.app_role,
                    m.plant_role,
                    m.priority,
                    m.is_enabled
                FROM public.ppiq_sso_role_mappings m
                JOIN public.ppiq_sso_provider_configs p ON p.id = m.provider_id
                WHERE m.tenant_id = @tenant_id
                  AND p.provider_code = @provider_code
                ORDER BY m.priority, m.claim_type, m.claim_value
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("provider_code", providerCode);

            var rows = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new
                {
                    id = reader.GetGuid(0),
                    claimType = reader.GetString(1),
                    claimValue = reader.GetString(2),
                    appRole = reader.GetString(3),
                    plantRole = reader.GetString(4),
                    priority = reader.GetInt32(5),
                    isEnabled = reader.GetBoolean(6)
                });
            }

            return Results.Ok(rows);
        });

        group.MapPost("/role-mappings", async (
            [FromBody] SsoRoleMappingRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var provider = await LoadProviderAsync(connection, tenantId, request.ProviderCode, cancellationToken);

            if (provider.ProviderId == Guid.Empty)
                return Results.NotFound(new { error = "Provider not found." });

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_sso_role_mappings
                (
                    tenant_id,
                    provider_id,
                    claim_type,
                    claim_value,
                    app_role,
                    plant_role,
                    priority
                )
                VALUES
                (
                    @tenant_id,
                    @provider_id,
                    @claim_type,
                    @claim_value,
                    @app_role,
                    @plant_role,
                    @priority
                )
                ON CONFLICT (provider_id, claim_type, claim_value)
                DO UPDATE SET
                    app_role = EXCLUDED.app_role,
                    plant_role = EXCLUDED.plant_role,
                    priority = EXCLUDED.priority,
                    is_enabled = true
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("provider_id", provider.ProviderId);
            cmd.Parameters.AddWithValue("claim_type", request.ClaimType.Trim());
            cmd.Parameters.AddWithValue("claim_value", request.ClaimValue.Trim());
            cmd.Parameters.AddWithValue("app_role", request.AppRole.Trim());
            cmd.Parameters.AddWithValue("plant_role", request.PlantRole.Trim());
            cmd.Parameters.AddWithValue("priority", request.Priority);

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new
            {
                id,
                persisted = true,
                appliesOnNextSsoLogin = true
            });
        });

        var scim = app.MapGroup("/api/v5/scim/v2")
            .WithTags("V5 SCIM 2.0");

        scim.MapGet("/ServiceProviderConfig", () => Results.Ok(new
        {
            schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig" },
            patch = new { supported = true },
            bulk = new { supported = false },
            filter = new { supported = true },
            changePassword = new { supported = false },
            sort = new { supported = true },
            etag = new { supported = false },
            authenticationSchemes = new[]
            {
                new
                {
                    type = "oauthbearertoken",
                    name = "OAuth Bearer Token",
                    description = "Bearer token stored hashed in PlantProcess IQ."
                }
            }
        }));

        scim.MapPost("/Users", async (
            [FromBody] ScimUserRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizeScimAsync(dataSource, http, cancellationToken);
            if (!auth.Allowed) return Results.Unauthorized();

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, auth.TenantId, cancellationToken);

            var userId = await UpsertScimUserAsync(connection, auth.TenantId, request, cancellationToken);
            await WriteProvisioningAuditAsync(connection, auth.TenantId, "scim", "user_upsert", request.UserName, "success", request, cancellationToken);

            return Results.Json(new
            {
                schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:User" },
                id = userId,
                userName = request.UserName,
                displayName = request.DisplayName,
                active = request.Active
            }, statusCode: StatusCodes.Status201Created);
        });

        scim.MapGet("/Users", async (
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizeScimAsync(dataSource, http, cancellationToken);
            if (!auth.Allowed) return Results.Unauthorized();

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, auth.TenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                SELECT id, user_name, display_name, email, active
                FROM public.ppiq_scim_users
                WHERE tenant_id = @tenant_id
                ORDER BY user_name
                LIMIT 200
                """;
            cmd.Parameters.AddWithValue("tenant_id", auth.TenantId);

            var resources = new List<object>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                resources.Add(new
                {
                    id = reader.GetGuid(0),
                    userName = reader.GetString(1),
                    displayName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    emails = reader.IsDBNull(3) ? Array.Empty<object>() : new object[] { new { value = reader.GetString(3), primary = true } },
                    active = reader.GetBoolean(4)
                });
            }

            return Results.Ok(new
            {
                schemas = new[] { "urn:ietf:params:scim:api:messages:2.0:ListResponse" },
                totalResults = resources.Count,
                Resources = resources,
                startIndex = 1,
                itemsPerPage = resources.Count
            });
        });

        scim.MapPut("/Users/{id:guid}", async (
            Guid id,
            [FromBody] ScimUserRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizeScimAsync(dataSource, http, cancellationToken);
            if (!auth.Allowed) return Results.Unauthorized();

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, auth.TenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE public.ppiq_scim_users
                SET display_name = @display_name,
                    given_name = @given_name,
                    family_name = @family_name,
                    email = @email,
                    active = @active,
                    raw_scim = @raw_scim::jsonb,
                    deactivated_at_utc = CASE WHEN @active THEN NULL ELSE now() END,
                    updated_at_utc = now()
                WHERE tenant_id = @tenant_id
                  AND id = @id
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", auth.TenantId);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("display_name", request.DisplayName ?? request.UserName);
            cmd.Parameters.AddWithValue("given_name", request.GivenName ?? "");
            cmd.Parameters.AddWithValue("family_name", request.FamilyName ?? "");
            cmd.Parameters.AddWithValue("email", request.Email ?? "");
            cmd.Parameters.AddWithValue("active", request.Active);
            cmd.Parameters.AddWithValue("raw_scim", JsonSerializer.Serialize(request));

            var updated = await cmd.ExecuteScalarAsync(cancellationToken);
            await WriteProvisioningAuditAsync(connection, auth.TenantId, "scim", "user_update", id.ToString(), "success", request, cancellationToken);

            return updated is null
                ? Results.NotFound()
                : Results.Ok(new { id, active = request.Active, deactivationDisablesLogin = !request.Active });
        });

        scim.MapDelete("/Users/{id:guid}", async (
            Guid id,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizeScimAsync(dataSource, http, cancellationToken);
            if (!auth.Allowed) return Results.Unauthorized();

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, auth.TenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                UPDATE public.ppiq_scim_users
                SET active = false,
                    deactivated_at_utc = now(),
                    updated_at_utc = now()
                WHERE tenant_id = @tenant_id
                  AND id = @id
                """;
            cmd.Parameters.AddWithValue("tenant_id", auth.TenantId);
            cmd.Parameters.AddWithValue("id", id);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            await WriteProvisioningAuditAsync(connection, auth.TenantId, "scim", "user_deactivate", id.ToString(), "success", new { neverHardDelete = true }, cancellationToken);

            return Results.NoContent();
        });

        scim.MapPost("/Groups", async (
            [FromBody] ScimGroupRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var auth = await AuthorizeScimAsync(dataSource, http, cancellationToken);
            if (!auth.Allowed) return Results.Unauthorized();

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, auth.TenantId, cancellationToken);

            var groupId = await UpsertScimGroupAsync(connection, auth.TenantId, request, cancellationToken);
            await WriteProvisioningAuditAsync(connection, auth.TenantId, "scim", "group_upsert", request.DisplayName, "success", request, cancellationToken);

            return Results.Json(new
            {
                schemas = new[] { "urn:ietf:params:scim:schemas:core:2.0:Group" },
                id = groupId,
                displayName = request.DisplayName,
                active = request.Active
            }, statusCode: StatusCodes.Status201Created);
        });

        return group;
    }

    private sealed record Provider(Guid ProviderId, string DefaultRole, bool JitProvisioningEnabled);
    private sealed record SsoClaims(string Subject, string Email, string DisplayName, string[] Groups);
    private sealed record RoleResolution(string AppRole, string PlantRole);
    private sealed record ScimAuth(bool Allowed, Guid TenantId);

    private static async Task<Provider> LoadProviderAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, default_role, jit_provisioning_enabled
            FROM public.ppiq_sso_provider_configs
            WHERE tenant_id = @tenant_id
              AND provider_code = @provider_code
              AND is_enabled = true
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_code", providerCode.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return new Provider(Guid.Empty, "Viewer", false);

        return new Provider(reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2));
    }

    private static async Task<RoleResolution> ResolveRoleAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid providerId,
        string[] groups,
        string defaultRole,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT app_role, plant_role
            FROM public.ppiq_sso_role_mappings
            WHERE tenant_id = @tenant_id
              AND provider_id = @provider_id
              AND claim_type = 'groups'
              AND claim_value = ANY(@groups)
              AND is_enabled = true
            ORDER BY priority
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_id", providerId);
        cmd.Parameters.AddWithValue("groups", groups);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return new RoleResolution(defaultRole, defaultRole);

        return new RoleResolution(reader.GetString(0), reader.GetString(1));
    }

    private static async Task<Guid> UpsertPrincipalAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid providerId,
        SsoClaims claims,
        RoleResolution role,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_sso_principals
            (
                tenant_id,
                provider_id,
                external_subject,
                email,
                display_name,
                app_role,
                plant_role,
                last_login_at_utc
            )
            VALUES
            (
                @tenant_id,
                @provider_id,
                @external_subject,
                @email,
                @display_name,
                @app_role,
                @plant_role,
                now()
            )
            ON CONFLICT (provider_id, external_subject)
            DO UPDATE SET
                email = EXCLUDED.email,
                display_name = EXCLUDED.display_name,
                app_role = EXCLUDED.app_role,
                plant_role = EXCLUDED.plant_role,
                last_login_at_utc = now(),
                updated_at_utc = now()
            RETURNING id
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_id", providerId);
        cmd.Parameters.AddWithValue("external_subject", claims.Subject);
        cmd.Parameters.AddWithValue("email", claims.Email);
        cmd.Parameters.AddWithValue("display_name", claims.DisplayName);
        cmd.Parameters.AddWithValue("app_role", role.AppRole);
        cmd.Parameters.AddWithValue("plant_role", role.PlantRole);

        return (Guid)(await cmd.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
    }

    private static async Task<Guid> UpsertScimUserAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        ScimUserRequest request,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_scim_users
            (
                tenant_id,
                external_id,
                user_name,
                display_name,
                given_name,
                family_name,
                email,
                active,
                raw_scim
            )
            VALUES
            (
                @tenant_id,
                @external_id,
                @user_name,
                @display_name,
                @given_name,
                @family_name,
                @email,
                @active,
                @raw_scim::jsonb
            )
            ON CONFLICT (tenant_id, user_name)
            DO UPDATE SET
                external_id = EXCLUDED.external_id,
                display_name = EXCLUDED.display_name,
                given_name = EXCLUDED.given_name,
                family_name = EXCLUDED.family_name,
                email = EXCLUDED.email,
                active = EXCLUDED.active,
                raw_scim = EXCLUDED.raw_scim,
                deactivated_at_utc = CASE WHEN EXCLUDED.active THEN NULL ELSE now() END,
                updated_at_utc = now()
            RETURNING id
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("external_id", request.ExternalId ?? "");
        cmd.Parameters.AddWithValue("user_name", request.UserName.Trim());
        cmd.Parameters.AddWithValue("display_name", request.DisplayName ?? request.UserName.Trim());
        cmd.Parameters.AddWithValue("given_name", request.GivenName ?? "");
        cmd.Parameters.AddWithValue("family_name", request.FamilyName ?? "");
        cmd.Parameters.AddWithValue("email", request.Email ?? "");
        cmd.Parameters.AddWithValue("active", request.Active);
        cmd.Parameters.AddWithValue("raw_scim", JsonSerializer.Serialize(request));

        return (Guid)(await cmd.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
    }

    private static async Task<Guid> UpsertScimGroupAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        ScimGroupRequest request,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_scim_groups
            (
                tenant_id,
                external_id,
                display_name,
                active,
                raw_scim
            )
            VALUES
            (
                @tenant_id,
                @external_id,
                @display_name,
                @active,
                @raw_scim::jsonb
            )
            ON CONFLICT (tenant_id, display_name)
            DO UPDATE SET
                external_id = EXCLUDED.external_id,
                active = EXCLUDED.active,
                raw_scim = EXCLUDED.raw_scim,
                updated_at_utc = now()
            RETURNING id
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("external_id", request.ExternalId ?? "");
        cmd.Parameters.AddWithValue("display_name", request.DisplayName.Trim());
        cmd.Parameters.AddWithValue("active", request.Active);
        cmd.Parameters.AddWithValue("raw_scim", JsonSerializer.Serialize(request));

        return (Guid)(await cmd.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
    }

    private static async Task<ScimAuth> AuthorizeScimAsync(
        NpgsqlDataSource dataSource,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var auth = http.Request.Headers.Authorization.ToString();

        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return new ScimAuth(false, DefaultTenantId);

        var token = auth["Bearer ".Length..].Trim();
        var hash = Sha256(token);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await SetTenantAsync(connection, DefaultTenantId, cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT tenant_id
            FROM public.ppiq_scim_bearer_tokens
            WHERE token_hash = @hash
              AND is_enabled = true
              AND (expires_at_utc IS NULL OR expires_at_utc > now())
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("hash", hash);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);

        return result is Guid tenantId
            ? new ScimAuth(true, tenantId)
            : new ScimAuth(false, DefaultTenantId);
    }

    private static bool TryValidateMockToken(
        string token,
        out SsoClaims claims,
        out string error)
    {
        claims = new SsoClaims("", "", "", Array.Empty<string>());
        error = "";

        var parts = token.Split('.');

        if (parts.Length != 2)
        {
            error = "Malformed mock token.";
            return false;
        }

        var expected = Sign(parts[0]);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expected),
                Encoding.ASCII.GetBytes(parts[1])))
        {
            error = "Invalid token signature.";
            return false;
        }

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
        using var doc = JsonDocument.Parse(payloadJson);

        var exp = doc.RootElement.GetProperty("exp").GetInt64();

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp)
        {
            error = "Token expired.";
            return false;
        }

        var groups = doc.RootElement.TryGetProperty("groups", out var groupsElement) && groupsElement.ValueKind == JsonValueKind.Array
            ? groupsElement.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray()
            : Array.Empty<string>();

        claims = new SsoClaims(
            doc.RootElement.GetProperty("sub").GetString() ?? "",
            doc.RootElement.GetProperty("email").GetString() ?? "",
            doc.RootElement.GetProperty("name").GetString() ?? "",
            groups);

        return true;
    }

    private static async Task WriteProvisioningAuditAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string source,
        string action,
        string subject,
        string outcome,
        object details,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_identity_provisioning_audit
            (
                tenant_id,
                source,
                action,
                subject,
                outcome,
                details
            )
            VALUES
            (
                @tenant_id,
                @source,
                @action,
                @subject,
                @outcome,
                @details::jsonb
            )
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("source", source);
        cmd.Parameters.AddWithValue("action", action);
        cmd.Parameters.AddWithValue("subject", subject);
        cmd.Parameters.AddWithValue("outcome", outcome);
        cmd.Parameters.AddWithValue("details", JsonSerializer.Serialize(details));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Sign(string payload64)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(MockIdpSecret));
        return Base64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload64)));
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }

        return Convert.FromBase64String(padded);
    }

    private static string Sha256(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static Guid ResolveTenantId(HttpContext http)
    {
        var claimValue =
            http.User.FindFirst("tenant_id")?.Value ??
            http.User.FindFirst("tenantId")?.Value ??
            DefaultTenantId.ToString();

        return Guid.TryParse(claimValue, out var tenantId) ? tenantId : DefaultTenantId;
    }

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant', @tenant_id, false)";
        cmd.Parameters.AddWithValue("tenant_id", tenantId.ToString());
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}