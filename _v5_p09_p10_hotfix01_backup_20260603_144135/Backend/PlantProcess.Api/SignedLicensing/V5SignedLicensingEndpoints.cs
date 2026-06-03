using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace PlantProcess.Api.SignedLicensing;

public sealed record CreateDevLicenseRequest(
    string Tier = "Enterprise",
    int MaxUsers = 40,
    int MaxSources = 12,
    int MaxPages = 50,
    string AssistantMode = "private_endpoint",
    string DeploymentMode = "on_prem",
    int ValidDays = 365);

public sealed record ActivateLicenseRequest(
    string LicenseKey,
    string KeyId,
    string Algorithm,
    object Payload,
    string SignatureBase64);

public sealed record VerifyLicenseRequest(
    string LicenseKey,
    object Payload,
    string SignatureBase64,
    string KeyId);

public static class V5SignedLicensingEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static IEndpointRouteBuilder MapV5SignedLicensingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/licensing")
            .WithTags("V5 Signed Licensing");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-signed-licensing",
            offlineVerification = true,
            antiTamper = true,
            entitlementSource = "verified_signed_license",
            localAlgorithm = "ecdsa-p256-dev",
            ed25519ContractReady = true
        }));

        group.MapPost("/dev/create-license", async (
            [FromBody] CreateDevLicenseRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var key = await EnsureDevSigningKeyAsync(connection, cancellationToken);

            var now = DateTime.UtcNow;
            var expires = now.AddDays(Math.Clamp(request.ValidDays, 1, 3650));

            var licenseKey = $"PPIQ-{tenantId:N[..8]}-{DateTime.UtcNow:yyyyMMddHHmmss}";

            var payload = new
            {
                tenantId,
                licenseKey,
                tier = request.Tier,
                features = new
                {
                    visualMapper = true,
                    blendedProvenance = true,
                    assistantGateway = true,
                    scim = true,
                    sso = true,
                    signedLicensing = true
                },
                limits = new
                {
                    maxUsers = request.MaxUsers,
                    maxSources = request.MaxSources,
                    maxPages = request.MaxPages
                },
                assistantMode = request.AssistantMode,
                deploymentMode = request.DeploymentMode,
                issuedAtUtc = now,
                expiresAtUtc = expires,
                graceUntilUtc = expires.AddDays(14)
            };

            var canonical = CanonicalJson(payload);
            var signature = SignPayload(key.PrivateKeyPem, canonical);

            return Results.Ok(new
            {
                licenseKey,
                keyId = key.KeyId,
                algorithm = key.Algorithm,
                payload,
                signatureBase64 = signature,
                activateWith = "/api/v5/licensing/activate"
            });
        });

        group.MapPost("/activate", async (
            [FromBody] ActivateLicenseRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);
            var canonical = CanonicalJson(request.Payload);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var key = await LoadPublicKeyAsync(connection, request.KeyId, cancellationToken);

            if (key.KeyId.Length == 0)
                return Results.BadRequest(new { activated = false, error = "Signing key not found." });

            var verified = VerifyPayload(key.PublicKeyPem, canonical, request.SignatureBase64);
            var payloadDoc = JsonDocument.Parse(canonical);
            var licenseTenant = payloadDoc.RootElement.GetProperty("tenantId").GetGuid();

            if (licenseTenant != tenantId)
                verified = false;

            var issuedAt = payloadDoc.RootElement.GetProperty("issuedAtUtc").GetDateTime();
            var expiresAt = payloadDoc.RootElement.GetProperty("expiresAtUtc").GetDateTime();
            var graceUntil = payloadDoc.RootElement.GetProperty("graceUntilUtc").GetDateTime();

            var status = verified
                ? (DateTime.UtcNow > graceUntil ? "expired" : "valid")
                : "invalid_signature";

            await using var tx = await connection.BeginTransactionAsync(cancellationToken);

            var artifactId = await InsertArtifactAsync(
                connection,
                tx,
                tenantId,
                request.LicenseKey,
                request.KeyId,
                request.Algorithm,
                canonical,
                request.SignatureBase64,
                issuedAt,
                expiresAt,
                graceUntil,
                status,
                verified ? null : "Signature or tenant mismatch.",
                cancellationToken);

            if (status == "valid")
            {
                await UpsertEntitlementsAsync(connection, tx, tenantId, artifactId, payloadDoc.RootElement, cancellationToken);
            }

            await WriteLicenseEventAsync(connection, tx, tenantId, artifactId, "activate", status == "valid" ? "success" : "failure", status, new
            {
                request.LicenseKey,
                status
            }, cancellationToken);

            await tx.CommitAsync(cancellationToken);

            return Results.Ok(new
            {
                activated = status == "valid",
                status,
                artifactId,
                sourceOfTruth = "verified_signed_license"
            });
        });

        group.MapGet("/current", async (
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
                    tier,
                    max_users,
                    max_sources,
                    max_pages,
                    assistant_mode,
                    deployment_mode,
                    features,
                    expires_at_utc,
                    grace_until_utc,
                    is_valid,
                    source_of_truth
                FROM public.ppiq_v5_license_source_of_truth
                WHERE tenant_id = @tenant_id
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return Results.Ok(new
                {
                    isValid = false,
                    tier = "Unlicensed",
                    sourceOfTruth = "no_verified_signed_license",
                    warning = "No verified signed license activated."
                });
            }

            return Results.Ok(new
            {
                tier = reader.GetString(0),
                maxUsers = reader.GetInt32(1),
                maxSources = reader.GetInt32(2),
                maxPages = reader.GetInt32(3),
                assistantMode = reader.GetString(4),
                deploymentMode = reader.GetString(5),
                features = reader.GetString(6),
                expiresAtUtc = reader.GetDateTime(7),
                graceUntilUtc = reader.IsDBNull(8) ? null : reader.GetDateTime(8).ToString("O"),
                isValid = reader.GetBoolean(9),
                sourceOfTruth = reader.GetString(10)
            });
        });

        group.MapPost("/verify", async (
            [FromBody] VerifyLicenseRequest request,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var key = await LoadPublicKeyAsync(connection, request.KeyId, cancellationToken);

            if (key.KeyId.Length == 0)
                return Results.BadRequest(new { verified = false, error = "Signing key not found." });

            var canonical = CanonicalJson(request.Payload);
            var verified = VerifyPayload(key.PublicKeyPem, canonical, request.SignatureBase64);

            return Results.Ok(new
            {
                verified,
                request.LicenseKey,
                key.KeyId,
                key.Algorithm,
                offline = true
            });
        });

        group.MapPost("/tamper-test", async (
            [FromBody] ActivateLicenseRequest request,
            NpgsqlDataSource dataSource,
            CancellationToken cancellationToken) =>
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var key = await LoadPublicKeyAsync(connection, request.KeyId, cancellationToken);

            if (key.KeyId.Length == 0)
                return Results.BadRequest(new { tamperRejected = true, reason = "Signing key not found." });

            var original = CanonicalJson(request.Payload);
            var tampered = original.Replace("\"tier\":\"", "\"tier\":\"Tampered-", StringComparison.Ordinal);
            var verified = VerifyPayload(key.PublicKeyPem, tampered, request.SignatureBase64);

            return Results.Ok(new
            {
                tamperRejected = !verified,
                verifiedAfterTamper = verified
            });
        });

        return app;
    }

    private sealed record SigningKey(string KeyId, string Algorithm, string PublicKeyPem, string PrivateKeyPem);

    private static async Task<SigningKey> EnsureDevSigningKeyAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var existing = await LoadSigningKeyAsync(connection, "local-dev-ecdsa-p256", cancellationToken);

        if (existing.KeyId.Length > 0)
            return existing;

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var privatePem = ecdsa.ExportECPrivateKeyPem();
        var publicPem = ecdsa.ExportSubjectPublicKeyInfoPem();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_license_signing_keys
            (
                key_id,
                algorithm,
                public_key_pem,
                private_key_pem_encrypted
            )
            VALUES
            (
                'local-dev-ecdsa-p256',
                'ecdsa-p256-dev',
                @public_key,
                @private_key
            )
            ON CONFLICT (key_id)
            DO NOTHING
            """;
        cmd.Parameters.AddWithValue("public_key", publicPem);
        cmd.Parameters.AddWithValue("private_key", privatePem);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return new SigningKey("local-dev-ecdsa-p256", "ecdsa-p256-dev", publicPem, privatePem);
    }

    private static async Task<SigningKey> LoadSigningKeyAsync(
        NpgsqlConnection connection,
        string keyId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT key_id, algorithm, public_key_pem, COALESCE(private_key_pem_encrypted, '')
            FROM public.ppiq_license_signing_keys
            WHERE key_id = @key_id
              AND is_active = true
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("key_id", keyId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return new SigningKey("", "", "", "");

        return new SigningKey(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3));
    }

    private static async Task<(string KeyId, string Algorithm, string PublicKeyPem)> LoadPublicKeyAsync(
        NpgsqlConnection connection,
        string keyId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT key_id, algorithm, public_key_pem
            FROM public.ppiq_license_signing_keys
            WHERE key_id = @key_id
              AND is_active = true
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("key_id", keyId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return ("", "", "");

        return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    private static string SignPayload(string privateKeyPem, string canonicalPayload)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);

        var signature = ecdsa.SignData(
            Encoding.UTF8.GetBytes(canonicalPayload),
            HashAlgorithmName.SHA256);

        return Convert.ToBase64String(signature);
    }

    private static bool VerifyPayload(string publicKeyPem, string canonicalPayload, string signatureBase64)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(publicKeyPem);

            return ecdsa.VerifyData(
                Encoding.UTF8.GetBytes(canonicalPayload),
                Convert.FromBase64String(signatureBase64),
                HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<Guid> InsertArtifactAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        Guid tenantId,
        string licenseKey,
        string keyId,
        string algorithm,
        string canonicalPayload,
        string signature,
        DateTime issuedAt,
        DateTime expiresAt,
        DateTime graceUntil,
        string status,
        string? error,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_signed_license_artifacts
            (
                tenant_id,
                license_key,
                key_id,
                algorithm,
                payload_json,
                payload_canonical,
                signature_base64,
                issued_at_utc,
                expires_at_utc,
                grace_until_utc,
                verification_status,
                verification_error,
                is_active
            )
            VALUES
            (
                @tenant_id,
                @license_key,
                @key_id,
                @algorithm,
                @payload_json::jsonb,
                @payload_canonical,
                @signature_base64,
                @issued_at_utc,
                @expires_at_utc,
                @grace_until_utc,
                @verification_status,
                @verification_error,
                true
            )
            ON CONFLICT (tenant_id, license_key)
            DO UPDATE SET
                payload_json = EXCLUDED.payload_json,
                payload_canonical = EXCLUDED.payload_canonical,
                signature_base64 = EXCLUDED.signature_base64,
                verification_status = EXCLUDED.verification_status,
                verification_error = EXCLUDED.verification_error,
                activated_at_utc = now(),
                is_active = true
            RETURNING id
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("license_key", licenseKey);
        cmd.Parameters.AddWithValue("key_id", keyId);
        cmd.Parameters.AddWithValue("algorithm", algorithm);
        cmd.Parameters.AddWithValue("payload_json", canonicalPayload);
        cmd.Parameters.AddWithValue("payload_canonical", canonicalPayload);
        cmd.Parameters.AddWithValue("signature_base64", signature);
        cmd.Parameters.AddWithValue("issued_at_utc", issuedAt);
        cmd.Parameters.AddWithValue("expires_at_utc", expiresAt);
        cmd.Parameters.AddWithValue("grace_until_utc", graceUntil);
        cmd.Parameters.AddWithValue("verification_status", status);
        cmd.Parameters.AddWithValue("verification_error", error is null ? DBNull.Value : error);

        return (Guid)(await cmd.ExecuteScalarAsync(cancellationToken) ?? Guid.Empty);
    }

    private static async Task UpsertEntitlementsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        Guid tenantId,
        Guid artifactId,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var limits = payload.GetProperty("limits");

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_license_entitlement_projection
            (
                tenant_id,
                license_artifact_id,
                tier,
                max_users,
                max_sources,
                max_pages,
                assistant_mode,
                deployment_mode,
                features,
                issued_at_utc,
                expires_at_utc,
                grace_until_utc,
                is_valid,
                source_of_truth
            )
            VALUES
            (
                @tenant_id,
                @license_artifact_id,
                @tier,
                @max_users,
                @max_sources,
                @max_pages,
                @assistant_mode,
                @deployment_mode,
                @features::jsonb,
                @issued_at_utc,
                @expires_at_utc,
                @grace_until_utc,
                true,
                'verified_signed_license'
            )
            ON CONFLICT (tenant_id)
            DO UPDATE SET
                license_artifact_id = EXCLUDED.license_artifact_id,
                tier = EXCLUDED.tier,
                max_users = EXCLUDED.max_users,
                max_sources = EXCLUDED.max_sources,
                max_pages = EXCLUDED.max_pages,
                assistant_mode = EXCLUDED.assistant_mode,
                deployment_mode = EXCLUDED.deployment_mode,
                features = EXCLUDED.features,
                issued_at_utc = EXCLUDED.issued_at_utc,
                expires_at_utc = EXCLUDED.expires_at_utc,
                grace_until_utc = EXCLUDED.grace_until_utc,
                is_valid = true,
                source_of_truth = 'verified_signed_license',
                updated_at_utc = now()
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("license_artifact_id", artifactId);
        cmd.Parameters.AddWithValue("tier", payload.GetProperty("tier").GetString() ?? "Unlicensed");
        cmd.Parameters.AddWithValue("max_users", limits.GetProperty("maxUsers").GetInt32());
        cmd.Parameters.AddWithValue("max_sources", limits.GetProperty("maxSources").GetInt32());
        cmd.Parameters.AddWithValue("max_pages", limits.GetProperty("maxPages").GetInt32());
        cmd.Parameters.AddWithValue("assistant_mode", payload.GetProperty("assistantMode").GetString() ?? "extractive");
        cmd.Parameters.AddWithValue("deployment_mode", payload.GetProperty("deploymentMode").GetString() ?? "local");
        cmd.Parameters.AddWithValue("features", payload.GetProperty("features").GetRawText());
        cmd.Parameters.AddWithValue("issued_at_utc", payload.GetProperty("issuedAtUtc").GetDateTime());
        cmd.Parameters.AddWithValue("expires_at_utc", payload.GetProperty("expiresAtUtc").GetDateTime());
        cmd.Parameters.AddWithValue("grace_until_utc", payload.GetProperty("graceUntilUtc").GetDateTime());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task WriteLicenseEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        Guid tenantId,
        Guid artifactId,
        string eventType,
        string outcome,
        string message,
        object details,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText =
            """
            INSERT INTO public.ppiq_license_events
            (
                tenant_id,
                license_artifact_id,
                event_type,
                outcome,
                message,
                details
            )
            VALUES
            (
                @tenant_id,
                @license_artifact_id,
                @event_type,
                @outcome,
                @message,
                @details::jsonb
            )
            """;

        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("license_artifact_id", artifactId);
        cmd.Parameters.AddWithValue("event_type", eventType);
        cmd.Parameters.AddWithValue("outcome", outcome);
        cmd.Parameters.AddWithValue("message", message);
        cmd.Parameters.AddWithValue("details", JsonSerializer.Serialize(details));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string CanonicalJson(object value)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value));
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });

        WriteSorted(doc.RootElement, writer);
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteSorted(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(prop.Name);
                    WriteSorted(prop.Value, writer);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteSorted(item, writer);
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var i))
                    writer.WriteNumberValue(i);
                else
                    writer.WriteRawValue(element.GetRawText());
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                writer.WriteBooleanValue(element.GetBoolean());
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
        }
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