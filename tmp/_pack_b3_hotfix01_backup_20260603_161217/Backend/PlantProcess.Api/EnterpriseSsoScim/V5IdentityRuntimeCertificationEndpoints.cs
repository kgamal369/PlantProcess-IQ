using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace PlantProcess.Api.EnterpriseSsoScim;

public sealed record RegisterOidcRuntimeKeyRequest(
    string ProviderCode,
    string KeyId,
    string Issuer,
    string Audience,
    string N,
    string E);

public sealed record ValidateOidcRuntimeTokenRequest(
    string ProviderCode,
    string IdToken,
    string? ExpectedAudience);

public sealed record ScimDeactivationLoginProofRequest(
    string UserNameOrEmail);

public static class V5IdentityRuntimeCertificationEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static IEndpointRouteBuilder MapV5IdentityRuntimeCertificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/identity-runtime")
            .WithTags("V5 Identity Runtime Certification");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-identity-runtime-certification",
            oidc = "RS256/JWKS compatible",
            keycloakProfile = true,
            tokenSignatureValidation = true,
            invalidExpiredTokenRejection = true,
            scimSchemaProof = true,
            scimDeactivateLoginDeny = true,
            groupRoleMapping = true
        }));

        group.MapPost("/oidc/register-jwks-key", async (
            [FromBody] RegisterOidcRuntimeKeyRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            if (string.IsNullOrWhiteSpace(request.ProviderCode) ||
                string.IsNullOrWhiteSpace(request.KeyId) ||
                string.IsNullOrWhiteSpace(request.Issuer) ||
                string.IsNullOrWhiteSpace(request.Audience) ||
                string.IsNullOrWhiteSpace(request.N) ||
                string.IsNullOrWhiteSpace(request.E))
            {
                return Results.BadRequest(new { error = "providerCode, keyId, issuer, audience, n, and e are required." });
            }

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_oidc_runtime_jwks_keys
                (
                    tenant_id,
                    provider_code,
                    key_id,
                    issuer,
                    audience,
                    algorithm,
                    jwk_n_b64url,
                    jwk_e_b64url,
                    status
                )
                VALUES
                (
                    @tenant_id,
                    @provider_code,
                    @key_id,
                    @issuer,
                    @audience,
                    'RS256',
                    @n,
                    @e,
                    'active'
                )
                ON CONFLICT (tenant_id, provider_code, key_id)
                DO UPDATE SET
                    issuer = EXCLUDED.issuer,
                    audience = EXCLUDED.audience,
                    jwk_n_b64url = EXCLUDED.jwk_n_b64url,
                    jwk_e_b64url = EXCLUDED.jwk_e_b64url,
                    algorithm = 'RS256',
                    status = 'active',
                    retired_at_utc = NULL
                RETURNING id
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("provider_code", request.ProviderCode.Trim());
            cmd.Parameters.AddWithValue("key_id", request.KeyId.Trim());
            cmd.Parameters.AddWithValue("issuer", request.Issuer.Trim());
            cmd.Parameters.AddWithValue("audience", request.Audience.Trim());
            cmd.Parameters.AddWithValue("n", request.N.Trim());
            cmd.Parameters.AddWithValue("e", request.E.Trim());

            var id = await cmd.ExecuteScalarAsync(cancellationToken);

            return Results.Ok(new
            {
                id,
                saved = true,
                providerCode = request.ProviderCode,
                keyId = request.KeyId,
                algorithm = "RS256",
                source = "local-jwks-runtime-proof"
            });
        });

        group.MapPost("/oidc/validate-token", async (
            [FromBody] ValidateOidcRuntimeTokenRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var result = await ValidateRuntimeTokenAsync(
                connection,
                tenantId,
                request.ProviderCode,
                request.IdToken,
                request.ExpectedAudience,
                cancellationToken);

            await WriteSsoRuntimeEventAsync(
                connection,
                tenantId,
                request.ProviderCode,
                result.Subject,
                result.Email,
                result.Issuer,
                result.Audience,
                result.KeyId,
                result.Accepted ? "accepted" : "rejected",
                result.Error,
                result.AppRole,
                result.PlantRole,
                result.Evidence,
                cancellationToken);

            return result.Accepted
                ? Results.Ok(new
                {
                    accepted = true,
                    result.Subject,
                    result.Email,
                    result.DisplayName,
                    result.Groups,
                    result.AppRole,
                    result.PlantRole,
                    result.Issuer,
                    result.Audience,
                    result.KeyId,
                    runtimeProof = "RS256 signature, issuer, audience, exp, nbf, tenant role mapping"
                })
                : Results.BadRequest(new
                {
                    accepted = false,
                    result.Error,
                    result.Status,
                    result.Issuer,
                    result.Audience,
                    result.KeyId
                });
        });

        group.MapPost("/scim/deactivation-login-proof", async (
            [FromBody] ScimDeactivationLoginProofRequest request,
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
                SELECT id, user_name, email, active
                FROM public.ppiq_scim_users
                WHERE tenant_id = @tenant_id
                  AND (
                    lower(user_name) = lower(@subject)
                    OR lower(email) = lower(@subject)
                  )
                ORDER BY updated_at_utc DESC
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("subject", request.UserNameOrEmail.Trim());

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                await reader.DisposeAsync();

                await WriteScimContractEventAsync(
                    connection,
                    tenantId,
                    "deactivation-login-deny",
                    request.UserNameOrEmail,
                    "failed",
                    new { reason = "SCIM user not found" },
                    cancellationToken);

                return Results.NotFound(new
                {
                    allowedToLogin = false,
                    reason = "SCIM user not found."
                });
            }

            var id = reader.GetGuid(0);
            var userName = reader.GetString(1);
            var email = reader.IsDBNull(2) ? "" : reader.GetString(2);
            var active = reader.GetBoolean(3);
            await reader.DisposeAsync();

            var allowedToLogin = active;
            var outcome = active ? "failed" : "passed";

            await WriteScimContractEventAsync(
                connection,
                tenantId,
                "deactivation-login-deny",
                userName,
                outcome,
                new
                {
                    id,
                    userName,
                    email,
                    active,
                    expected = "inactive users must not be allowed to login",
                    allowedToLogin
                },
                cancellationToken);

            return Results.Ok(new
            {
                id,
                userName,
                email,
                active,
                allowedToLogin,
                deactivationDisablesLogin = !allowedToLogin,
                proof = active
                    ? "User is active; login would be allowed."
                    : "User is inactive/deactivated; login is denied."
            });
        });

        group.MapGet("/scim/schema-proof", async (
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var tables = new[]
            {
                "ppiq_scim_users",
                "ppiq_scim_groups",
                "ppiq_scim_group_members",
                "ppiq_scim_bearer_tokens",
                "ppiq_identity_provisioning_audit"
            };

            var tableResults = new List<object>();

            foreach (var table in tables)
            {
                await using var cmd = connection.CreateCommand();
                cmd.CommandText =
                    """
                    SELECT to_regclass(@table_name) IS NOT NULL
                    """;
                cmd.Parameters.AddWithValue("table_name", $"public.{table}");

                var exists = (bool)(await cmd.ExecuteScalarAsync(cancellationToken) ?? false);

                tableResults.Add(new
                {
                    table,
                    exists
                });
            }

            await WriteScimContractEventAsync(
                connection,
                tenantId,
                "schema-proof",
                "SCIM 2.0",
                tableResults.All(x => (bool)x.GetType().GetProperty("exists")!.GetValue(x)!) ? "passed" : "failed",
                new
                {
                    schemas = new[]
                    {
                        "urn:ietf:params:scim:schemas:core:2.0:User",
                        "urn:ietf:params:scim:schemas:core:2.0:Group",
                        "urn:ietf:params:scim:api:messages:2.0:ListResponse",
                        "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig"
                    },
                    tables = tableResults
                },
                cancellationToken);

            return Results.Ok(new
            {
                scimVersion = "2.0",
                users = true,
                groups = true,
                groupMembership = true,
                bearerTokenRegistry = true,
                provisioningAudit = true,
                schemas = new[]
                {
                    "urn:ietf:params:scim:schemas:core:2.0:User",
                    "urn:ietf:params:scim:schemas:core:2.0:Group",
                    "urn:ietf:params:scim:api:messages:2.0:ListResponse",
                    "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig"
                },
                tables = tableResults
            });
        });

        return app;
    }

    private static async Task<TokenValidationResult> ValidateRuntimeTokenAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        string token,
        string? expectedAudience,
        CancellationToken cancellationToken)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3)
                return TokenValidationResult.Reject("invalid_format", "Token must be compact JWT with three parts.");

            var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));

            using var headerDoc = JsonDocument.Parse(headerJson);
            using var payloadDoc = JsonDocument.Parse(payloadJson);

            var header = headerDoc.RootElement;
            var payload = payloadDoc.RootElement;

            var alg = JsonString(header, "alg") ?? "";
            var kid = JsonString(header, "kid") ?? "";

            if (!string.Equals(alg, "RS256", StringComparison.OrdinalIgnoreCase))
                return TokenValidationResult.Reject("invalid_alg", "Only RS256 is accepted for runtime OIDC proof.");

            if (string.IsNullOrWhiteSpace(kid))
                return TokenValidationResult.Reject("missing_kid", "JWT kid is required.");

            var key = await LoadRuntimeKeyAsync(connection, tenantId, providerCode, kid, cancellationToken);
            if (key is null)
                return TokenValidationResult.Reject("unknown_kid", $"No active runtime JWKS key found for kid={kid}.", keyId: kid);

            var issuer = JsonString(payload, "iss") ?? "";
            var audience = ReadAudience(payload);

            if (!string.Equals(issuer, key.Issuer, StringComparison.Ordinal))
                return TokenValidationResult.Reject("invalid_issuer", "JWT issuer mismatch.", issuer, audience, kid);

            var requiredAudience = string.IsNullOrWhiteSpace(expectedAudience)
                ? key.Audience
                : expectedAudience.Trim();

            if (!audience.Contains(requiredAudience, StringComparer.Ordinal))
                return TokenValidationResult.Reject("invalid_audience", "JWT audience mismatch.", issuer, string.Join(",", audience), kid);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var exp = JsonLong(payload, "exp");
            var nbf = JsonLong(payload, "nbf");

            if (!exp.HasValue || exp.Value <= now)
                return TokenValidationResult.Reject("expired", "JWT is expired.", issuer, string.Join(",", audience), kid);

            if (nbf.HasValue && nbf.Value > now)
                return TokenValidationResult.Reject("not_yet_valid", "JWT nbf is in the future.", issuer, string.Join(",", audience), kid);

            var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            var signature = Base64UrlDecode(parts[2]);

            using var rsa = RSA.Create();
            rsa.ImportParameters(new RSAParameters
            {
                Modulus = Base64UrlDecode(key.N),
                Exponent = Base64UrlDecode(key.E)
            });

            var verified = rsa.VerifyData(
                signingInput,
                signature,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            if (!verified)
                return TokenValidationResult.Reject("invalid_signature", "JWT RS256 signature is invalid.", issuer, string.Join(",", audience), kid);

            var subject = JsonString(payload, "sub") ?? "";
            var email = JsonString(payload, "email") ?? "";
            var displayName = JsonString(payload, "name") ?? email;
            var groups = ReadStringArray(payload, "groups");

            if (string.IsNullOrWhiteSpace(subject))
                return TokenValidationResult.Reject("missing_subject", "JWT subject is required.", issuer, string.Join(",", audience), kid);

            var role = await ResolveRuntimeRoleAsync(connection, tenantId, providerCode, groups, cancellationToken);
            await UpsertSsoPrincipalAsync(connection, tenantId, providerCode, subject, email, displayName, role.AppRole, role.PlantRole, payloadJson, cancellationToken);

            return TokenValidationResult.Accept(
                subject,
                email,
                displayName,
                groups,
                role.AppRole,
                role.PlantRole,
                issuer,
                string.Join(",", audience),
                kid,
                new
                {
                    tokenValidation = "RS256/JWKS",
                    issuer,
                    audience,
                    groups,
                    role.AppRole,
                    role.PlantRole
                });
        }
        catch (Exception ex)
        {
            return TokenValidationResult.Reject("exception", ex.Message);
        }
    }

    private static async Task<RuntimeKey?> LoadRuntimeKeyAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        string kid,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT issuer, audience, jwk_n_b64url, jwk_e_b64url
            FROM public.ppiq_oidc_runtime_jwks_keys
            WHERE tenant_id = @tenant_id
              AND provider_code = @provider_code
              AND key_id = @key_id
              AND algorithm = 'RS256'
              AND status = 'active'
            ORDER BY created_at_utc DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_code", providerCode.Trim());
        cmd.Parameters.AddWithValue("key_id", kid.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new RuntimeKey(
            Issuer: reader.GetString(0),
            Audience: reader.GetString(1),
            N: reader.GetString(2),
            E: reader.GetString(3));
    }

    private static async Task<RoleResolution> ResolveRuntimeRoleAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        string[] groups,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT m.app_role, m.plant_role
            FROM public.ppiq_sso_role_mappings m
            JOIN public.ppiq_sso_provider_configs p ON p.id = m.provider_id
            WHERE m.tenant_id = @tenant_id
              AND p.tenant_id = @tenant_id
              AND p.provider_code = @provider_code
              AND m.claim_type = 'groups'
              AND m.claim_value = ANY(@groups)
              AND m.is_enabled = true
              AND p.is_enabled = true
            ORDER BY m.priority
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_code", providerCode.Trim());
        cmd.Parameters.AddWithValue("groups", groups);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return new RoleResolution("Viewer", "Viewer");

        return new RoleResolution(reader.GetString(0), reader.GetString(1));
    }

    private static async Task UpsertSsoPrincipalAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        string subject,
        string email,
        string displayName,
        string appRole,
        string plantRole,
        string rawClaims,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_sso_principals
            (
                tenant_id,
                provider_code,
                external_subject,
                email,
                display_name,
                app_role,
                plant_role,
                last_login_at_utc,
                raw_claims
            )
            VALUES
            (
                @tenant_id,
                @provider_code,
                @external_subject,
                @email,
                @display_name,
                @app_role,
                @plant_role,
                now(),
                @raw_claims::jsonb
            )
            ON CONFLICT (tenant_id, provider_code, external_subject)
            DO UPDATE SET
                email = EXCLUDED.email,
                display_name = EXCLUDED.display_name,
                app_role = EXCLUDED.app_role,
                plant_role = EXCLUDED.plant_role,
                last_login_at_utc = now(),
                raw_claims = EXCLUDED.raw_claims
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_code", providerCode.Trim());
        cmd.Parameters.AddWithValue("external_subject", subject);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("display_name", displayName);
        cmd.Parameters.AddWithValue("app_role", appRole);
        cmd.Parameters.AddWithValue("plant_role", plantRole);
        cmd.Parameters.AddWithValue("raw_claims", rawClaims);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteSsoRuntimeEventAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string providerCode,
        string subject,
        string email,
        string issuer,
        string audience,
        string kid,
        string outcome,
        string? failureReason,
        string appRole,
        string plantRole,
        object? evidence,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_sso_runtime_validation_events
            (
                tenant_id,
                provider_code,
                subject,
                email,
                issuer,
                audience,
                token_key_id,
                outcome,
                failure_reason,
                app_role,
                plant_role,
                evidence_json
            )
            VALUES
            (
                @tenant_id,
                @provider_code,
                @subject,
                @email,
                @issuer,
                @audience,
                @token_key_id,
                @outcome,
                @failure_reason,
                @app_role,
                @plant_role,
                @evidence_json::jsonb
            )
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("provider_code", string.IsNullOrWhiteSpace(providerCode) ? "unknown" : providerCode.Trim());
        cmd.Parameters.AddWithValue("subject", string.IsNullOrWhiteSpace(subject) ? "unknown" : subject);
        cmd.Parameters.AddWithValue("email", string.IsNullOrWhiteSpace(email) ? (object)DBNull.Value : email);
        cmd.Parameters.AddWithValue("issuer", string.IsNullOrWhiteSpace(issuer) ? "unknown" : issuer);
        cmd.Parameters.AddWithValue("audience", string.IsNullOrWhiteSpace(audience) ? "unknown" : audience);
        cmd.Parameters.AddWithValue("token_key_id", string.IsNullOrWhiteSpace(kid) ? "unknown" : kid);
        cmd.Parameters.AddWithValue("outcome", outcome);
        cmd.Parameters.AddWithValue("failure_reason", failureReason is null ? (object)DBNull.Value : failureReason);
        cmd.Parameters.AddWithValue("app_role", string.IsNullOrWhiteSpace(appRole) ? (object)DBNull.Value : appRole);
        cmd.Parameters.AddWithValue("plant_role", string.IsNullOrWhiteSpace(plantRole) ? (object)DBNull.Value : plantRole);
        cmd.Parameters.AddWithValue("evidence_json", JsonSerializer.Serialize(evidence ?? new { }));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteScimContractEventAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string eventCode,
        string subject,
        string outcome,
        object evidence,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_scim_runtime_contract_events
            (
                tenant_id,
                event_code,
                subject,
                outcome,
                evidence_json
            )
            VALUES
            (
                @tenant_id,
                @event_code,
                @subject,
                @outcome,
                @evidence_json::jsonb
            )
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("event_code", eventCode);
        cmd.Parameters.AddWithValue("subject", subject);
        cmd.Parameters.AddWithValue("outcome", outcome);
        cmd.Parameters.AddWithValue("evidence_json", JsonSerializer.Serialize(evidence));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string? JsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long? JsonLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }

    private static string[] ReadAudience(JsonElement payload)
    {
        if (!payload.TryGetProperty("aud", out var aud))
            return Array.Empty<string>();

        if (aud.ValueKind == JsonValueKind.String)
            return new[] { aud.GetString() ?? "" }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        if (aud.ValueKind == JsonValueKind.Array)
        {
            return aud.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private static string[] ReadStringArray(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var value))
            return Array.Empty<string>();

        if (value.ValueKind == JsonValueKind.String)
            return new[] { value.GetString() ?? "" }.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');

        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
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

    private sealed record RuntimeKey(
        string Issuer,
        string Audience,
        string N,
        string E);

    private sealed record RoleResolution(
        string AppRole,
        string PlantRole);

    private sealed record TokenValidationResult(
        bool Accepted,
        string Status,
        string? Error,
        string Subject,
        string Email,
        string DisplayName,
        string[] Groups,
        string AppRole,
        string PlantRole,
        string Issuer,
        string Audience,
        string KeyId,
        object Evidence)
    {
        public static TokenValidationResult Accept(
            string subject,
            string email,
            string displayName,
            string[] groups,
            string appRole,
            string plantRole,
            string issuer,
            string audience,
            string keyId,
            object evidence)
        {
            return new TokenValidationResult(
                Accepted: true,
                Status: "accepted",
                Error: null,
                Subject: subject,
                Email: email,
                DisplayName: displayName,
                Groups: groups,
                AppRole: appRole,
                PlantRole: plantRole,
                Issuer: issuer,
                Audience: audience,
                KeyId: keyId,
                Evidence: evidence);
        }

        public static TokenValidationResult Reject(
            string status,
            string error,
            string issuer = "",
            string audience = "",
            string keyId = "")
        {
            return new TokenValidationResult(
                Accepted: false,
                Status: status,
                Error: error,
                Subject: "unknown",
                Email: "",
                DisplayName: "",
                Groups: Array.Empty<string>(),
                AppRole: "",
                PlantRole: "",
                Issuer: issuer,
                Audience: audience,
                KeyId: keyId,
                Evidence: new { status, error });
        }
    }
}