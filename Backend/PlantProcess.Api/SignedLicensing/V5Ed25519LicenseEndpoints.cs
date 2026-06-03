using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace PlantProcess.Api.SignedLicensing;

public sealed record Ed25519ActivateLicenseRequest(
    string LicenseJws,
    string? ExpectedInstanceId);

public sealed record Ed25519OfflineVerifyRequest(
    string LicenseJws,
    string PublicKeyB64,
    string? ExpectedTenantId,
    string? ExpectedInstanceId);

public sealed record Ed25519EntitlementCheckRequest(
    string Feature,
    string? DbTierOverride);

public static class V5Ed25519LicenseEndpoints
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly IReadOnlyDictionary<string, string> RequiredTierByFeature =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ReadOnlySourceRegistry"] = "Light",
            ["CsvImport"] = "Light",
            ["ExcelImport"] = "Light",
            ["PostgreSqlConnector"] = "Pro",
            ["SqlServerConnector"] = "Enterprise",
            ["OracleConnector"] = "Enterprise",
            ["MySqlConnector"] = "Enterprise",
            ["RestApiConnector"] = "Enterprise",
            ["OpcUaHistorianConnector"] = "Enterprise",
            ["DbLinkConfiguration"] = "Light",
            ["SourceSnapshotImport"] = "Light",
            ["IncrementalImport"] = "Pro",
            ["SchemaSqlViewBuilder"] = "Pro",
            ["CrossSourceJoinExecution"] = "Pro",
            ["KpiViewBuilder"] = "ProPlus",
            ["WidgetScriptLayer"] = "ProPlus",
            ["DashboardPageBuilder"] = "Light",
            ["DashboardWidgetBuilder"] = "Light",
            ["DashboardLayoutPersistence"] = "Light",
            ["DataQualityBasicScan"] = "Light",
            ["DataQualityFullScan"] = "Pro",
            ["RiskDashboardView"] = "Pro",
            ["RiskDashboardContributors"] = "ProPlus",
            ["CorrelationManualRun"] = "Pro",
            ["CorrelationScheduledRun"] = "ProPlus",
            ["InvestigationWorkflow"] = "ProPlus",
            ["MlWorkspacePreview"] = "ProPlus",
            ["MlLearningJobs"] = "ProPlus",
            ["SuggestionRecommendation"] = "ProPlus",
            ["BasicInvestigationReportPdf"] = "Pro",
            ["FullGenealogyReportPdf"] = "ProPlus",
            ["BrandedReportPdf"] = "Enterprise"
        };

    private static readonly IReadOnlyDictionary<string, int> TierRank =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Light"] = 1,
            ["Pro"] = 2,
            ["ProPlus"] = 3,
            ["Pro Plus"] = 3,
            ["Enterprise"] = 4
        };

    public static IEndpointRouteBuilder MapV5Ed25519LicenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v5/licensing/ed25519")
            .WithTags("V5 Strict Ed25519 Licensing");

        group.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            component = "v5-ed25519-signed-licensing",
            algorithm = "Ed25519",
            offlineVerification = true,
            productServerSigning = false,
            signingTool = "tools/v5/generate-ed25519-license.mjs",
            sourceOfTruth = "public.ppiq_v_ed25519_current_entitlements",
            dbTierOverrideIgnored = true
        }));

        group.MapPost("/verify-offline", (
            [FromBody] Ed25519OfflineVerifyRequest request) =>
        {
            var result = VerifyCompactJws(
                request.LicenseJws,
                request.PublicKeyB64,
                request.ExpectedTenantId,
                request.ExpectedInstanceId);

            return result.IsValid
                ? Results.Ok(new
                {
                    valid = true,
                    result.Header,
                    result.Payload,
                    result.LicenseKey,
                    result.Tier,
                    result.ExpiresAtUtc,
                    result.KeyId
                })
                : Results.BadRequest(new
                {
                    valid = false,
                    result.Status,
                    result.Error
                });
        });

        group.MapPost("/activate", async (
            [FromBody] Ed25519ActivateLicenseRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var parsedHeader = TryReadHeader(request.LicenseJws);
            if (parsedHeader is null)
            {
                return Results.BadRequest(new
                {
                    activated = false,
                    status = "invalid_payload",
                    error = "Invalid compact JWS header."
                });
            }

            var publicKey = await GetActivePublicKeyAsync(
                connection,
                tenantId,
                parsedHeader.KeyId,
                cancellationToken);

            if (publicKey is null)
            {
                return Results.BadRequest(new
                {
                    activated = false,
                    status = "invalid_payload",
                    error = $"No active Ed25519 public key found for kid={parsedHeader.KeyId}."
                });
            }

            var verification = VerifyCompactJws(
                request.LicenseJws,
                publicKey,
                tenantId.ToString(),
                request.ExpectedInstanceId);

            if (!verification.IsValid)
            {
                return Results.BadRequest(new
                {
                    activated = false,
                    verification.Status,
                    verification.Error
                });
            }

            await StoreVerifiedLicenseAsync(
                connection,
                tenantId,
                request.LicenseJws,
                verification,
                cancellationToken);

            return Results.Ok(new
            {
                activated = true,
                verificationStatus = "valid",
                sourceOfTruth = "verified_ed25519_license",
                verification.LicenseKey,
                verification.Tier,
                verification.KeyId,
                verification.IssuedAtUtc,
                verification.ExpiresAtUtc,
                verification.InstanceId,
                features = verification.Features,
                limits = verification.Limits
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
                    license_key,
                    key_id,
                    tier,
                    payload_json::text,
                    features_json::text,
                    limits_json::text,
                    issued_at_utc,
                    expires_at_utc,
                    instance_id,
                    activated_at_utc
                FROM public.ppiq_v_ed25519_current_entitlements
                WHERE tenant_id = @tenant_id
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("tenant_id", tenantId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return Results.NotFound(new
                {
                    hasVerifiedLicense = false,
                    sourceOfTruth = "verified_ed25519_license",
                    message = "No active valid Ed25519 license is activated for this tenant."
                });
            }

            return Results.Ok(new
            {
                hasVerifiedLicense = true,
                sourceOfTruth = "verified_ed25519_license",
                licenseKey = reader.GetString(0),
                keyId = reader.GetString(1),
                tier = reader.GetString(2),
                payload = JsonDocument.Parse(reader.GetString(3)).RootElement.Clone(),
                features = JsonDocument.Parse(reader.GetString(4)).RootElement.Clone(),
                limits = JsonDocument.Parse(reader.GetString(5)).RootElement.Clone(),
                issuedAtUtc = reader.GetDateTime(6),
                expiresAtUtc = reader.GetDateTime(7),
                instanceId = reader.IsDBNull(8) ? null : reader.GetString(8),
                activatedAtUtc = reader.GetDateTime(9)
            });
        });

        group.MapPost("/entitlement-check", async (
            [FromBody] Ed25519EntitlementCheckRequest request,
            NpgsqlDataSource dataSource,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var tenantId = ResolveTenantId(http);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await SetTenantAsync(connection, tenantId, cancellationToken);

            var current = await GetCurrentVerifiedLicenseAsync(connection, tenantId, cancellationToken);
            if (current is null)
            {
                return Results.BadRequest(new
                {
                    allowed = false,
                    sourceOfTruth = "verified_ed25519_license",
                    error = "No verified active license exists."
                });
            }

            var feature = string.IsNullOrWhiteSpace(request.Feature)
                ? "Unknown"
                : request.Feature.Trim();

            var requiredTier = RequiredTierByFeature.TryGetValue(feature, out var required)
                ? required
                : "Enterprise";

            var allowed = IsTierAllowed(current.Tier, requiredTier);

            await using var audit = connection.CreateCommand();
            audit.CommandText =
                """
                INSERT INTO public.ppiq_ed25519_entitlement_audit
                (
                    tenant_id,
                    license_key,
                    requested_feature,
                    verified_tier,
                    required_tier,
                    is_allowed,
                    ignored_db_tier_override,
                    evidence_json
                )
                VALUES
                (
                    @tenant_id,
                    @license_key,
                    @requested_feature,
                    @verified_tier,
                    @required_tier,
                    @is_allowed,
                    @ignored_db_tier_override,
                    @evidence_json::jsonb
                )
                """;
            audit.Parameters.AddWithValue("tenant_id", tenantId);
            audit.Parameters.AddWithValue("license_key", current.LicenseKey);
            audit.Parameters.AddWithValue("requested_feature", feature);
            audit.Parameters.AddWithValue("verified_tier", current.Tier);
            audit.Parameters.AddWithValue("required_tier", requiredTier);
            audit.Parameters.AddWithValue("is_allowed", allowed);
            audit.Parameters.AddWithValue("ignored_db_tier_override", string.IsNullOrWhiteSpace(request.DbTierOverride) ? DBNull.Value : request.DbTierOverride);
            audit.Parameters.AddWithValue("evidence_json", JsonSerializer.Serialize(new
            {
                message = "Entitlement decision uses verified Ed25519 license tier only.",
                ignoredDbTierOverride = request.DbTierOverride,
                sourceOfTruth = "public.ppiq_v_ed25519_current_entitlements"
            }));
            await audit.ExecuteNonQueryAsync(cancellationToken);

            return Results.Ok(new
            {
                allowed,
                feature,
                verifiedTier = current.Tier,
                requiredTier,
                ignoredDbTierOverride = request.DbTierOverride,
                sourceOfTruth = "verified_ed25519_license",
                noDbEditEscalation = true
            });
        });

        return app;
    }

    private static bool IsTierAllowed(string actualTier, string requiredTier)
    {
        var actualRank = TierRank.TryGetValue(actualTier, out var a) ? a : 0;
        var requiredRank = TierRank.TryGetValue(requiredTier, out var r) ? r : 999;

        return actualRank >= requiredRank;
    }

    private static async Task<string?> GetActivePublicKeyAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string keyId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT public_key_b64
            FROM public.ppiq_ed25519_license_public_keys
            WHERE tenant_id = @tenant_id
              AND key_id = @key_id
              AND algorithm = 'Ed25519'
              AND status = 'active'
            ORDER BY created_at_utc DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);
        cmd.Parameters.AddWithValue("key_id", keyId);

        return (await cmd.ExecuteScalarAsync(cancellationToken))?.ToString();
    }

    private static async Task<CurrentVerifiedLicense?> GetCurrentVerifiedLicenseAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT license_key, tier
            FROM public.ppiq_v_ed25519_current_entitlements
            WHERE tenant_id = @tenant_id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("tenant_id", tenantId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new CurrentVerifiedLicense(
            LicenseKey: reader.GetString(0),
            Tier: reader.GetString(1));
    }

    private static async Task StoreVerifiedLicenseAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        string compactJws,
        VerificationResult verification,
        CancellationToken cancellationToken)
    {
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);

        await using (var supersede = connection.CreateCommand())
        {
            supersede.Transaction = tx;
            supersede.CommandText =
                """
                UPDATE public.ppiq_ed25519_activated_licenses
                SET activation_status = 'superseded'
                WHERE tenant_id = @tenant_id
                  AND activation_status = 'active'
                  AND license_key <> @license_key
                """;
            supersede.Parameters.AddWithValue("tenant_id", tenantId);
            supersede.Parameters.AddWithValue("license_key", verification.LicenseKey);
            await supersede.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText =
                """
                INSERT INTO public.ppiq_ed25519_activated_licenses
                (
                    tenant_id,
                    license_key,
                    key_id,
                    compact_jws,
                    tier,
                    payload_json,
                    features_json,
                    limits_json,
                    issued_at_utc,
                    expires_at_utc,
                    instance_id,
                    verification_status,
                    activation_status,
                    verification_error,
                    last_verified_at_utc
                )
                VALUES
                (
                    @tenant_id,
                    @license_key,
                    @key_id,
                    @compact_jws,
                    @tier,
                    @payload_json::jsonb,
                    @features_json::jsonb,
                    @limits_json::jsonb,
                    @issued_at_utc,
                    @expires_at_utc,
                    @instance_id,
                    'valid',
                    'active',
                    NULL,
                    now()
                )
                ON CONFLICT (tenant_id, license_key)
                DO UPDATE SET
                    key_id = EXCLUDED.key_id,
                    compact_jws = EXCLUDED.compact_jws,
                    tier = EXCLUDED.tier,
                    payload_json = EXCLUDED.payload_json,
                    features_json = EXCLUDED.features_json,
                    limits_json = EXCLUDED.limits_json,
                    issued_at_utc = EXCLUDED.issued_at_utc,
                    expires_at_utc = EXCLUDED.expires_at_utc,
                    instance_id = EXCLUDED.instance_id,
                    verification_status = 'valid',
                    activation_status = 'active',
                    verification_error = NULL,
                    last_verified_at_utc = now()
                """;

            cmd.Parameters.AddWithValue("tenant_id", tenantId);
            cmd.Parameters.AddWithValue("license_key", verification.LicenseKey);
            cmd.Parameters.AddWithValue("key_id", verification.KeyId);
            cmd.Parameters.AddWithValue("compact_jws", compactJws);
            cmd.Parameters.AddWithValue("tier", verification.Tier);
            cmd.Parameters.AddWithValue("payload_json", verification.PayloadJson);
            cmd.Parameters.AddWithValue("features_json", verification.FeaturesJson);
            cmd.Parameters.AddWithValue("limits_json", verification.LimitsJson);
            cmd.Parameters.AddWithValue("issued_at_utc", verification.IssuedAtUtc.UtcDateTime);
            cmd.Parameters.AddWithValue("expires_at_utc", verification.ExpiresAtUtc.UtcDateTime);
            cmd.Parameters.AddWithValue("instance_id", verification.InstanceId is null ? DBNull.Value : verification.InstanceId);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    private static ParsedHeader? TryReadHeader(string compactJws)
    {
        try
        {
            var parts = compactJws.Split('.');
            if (parts.Length != 3)
                return null;

            var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            using var doc = JsonDocument.Parse(headerJson);
            var root = doc.RootElement;

            var alg = JsonString(root, "alg") ?? "";
            var kid = JsonString(root, "kid") ?? "";

            if (!string.Equals(alg, "EdDSA", StringComparison.OrdinalIgnoreCase))
                return null;

            if (string.IsNullOrWhiteSpace(kid))
                return null;

            return new ParsedHeader(kid);
        }
        catch
        {
            return null;
        }
    }

    private static VerificationResult VerifyCompactJws(
        string compactJws,
        string publicKeyB64,
        string? expectedTenantId,
        string? expectedInstanceId)
    {
        try
        {
            var parts = compactJws.Split('.');
            if (parts.Length != 3)
                return VerificationResult.Fail("invalid_payload", "License must be compact JWS with three parts.");

            var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            var signature = Base64UrlDecode(parts[2]);

            var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));

            using var headerDoc = JsonDocument.Parse(headerJson);
            using var payloadDoc = JsonDocument.Parse(payloadJson);

            var header = headerDoc.RootElement.Clone();
            var payload = payloadDoc.RootElement.Clone();

            var alg = JsonString(header, "alg") ?? "";
            var typ = JsonString(header, "typ") ?? "";
            var kid = JsonString(header, "kid") ?? "";

            if (!string.Equals(alg, "EdDSA", StringComparison.OrdinalIgnoreCase))
                return VerificationResult.Fail("invalid_payload", "License alg must be EdDSA for Ed25519.");

            if (!typ.Contains("license", StringComparison.OrdinalIgnoreCase))
                return VerificationResult.Fail("invalid_payload", "License typ must identify a license artifact.");

            if (string.IsNullOrWhiteSpace(kid))
                return VerificationResult.Fail("invalid_payload", "License kid is required.");

            var publicKeyBytes = Convert.FromBase64String(publicKeyB64);
            if (publicKeyBytes.Length != 32)
                return VerificationResult.Fail("invalid_payload", "Ed25519 public key must be 32 raw bytes encoded as base64.");

            var verifier = new Ed25519Signer();
            verifier.Init(false, new Ed25519PublicKeyParameters(publicKeyBytes, 0));
            verifier.BlockUpdate(signingInput, 0, signingInput.Length);

            if (!verifier.VerifySignature(signature))
                return VerificationResult.Fail("invalid_signature", "Ed25519 signature verification failed.");

            var tenantId = JsonString(payload, "tenantId") ?? "";
            var licenseKey = JsonString(payload, "licenseKey") ?? "";
            var tier = JsonString(payload, "tier") ?? "";
            var instanceId = JsonString(payload, "instanceId");
            var issuedAtText = JsonString(payload, "issuedAtUtc") ?? "";
            var expiresAtText = JsonString(payload, "expiresAtUtc") ?? "";

            if (!string.IsNullOrWhiteSpace(expectedTenantId) &&
                !string.Equals(tenantId, expectedTenantId, StringComparison.OrdinalIgnoreCase))
            {
                return VerificationResult.Fail("invalid_payload", "License tenantId does not match expected tenant.");
            }

            if (!string.IsNullOrWhiteSpace(expectedInstanceId) &&
                !string.IsNullOrWhiteSpace(instanceId) &&
                !string.Equals(instanceId, expectedInstanceId, StringComparison.OrdinalIgnoreCase))
            {
                return VerificationResult.Fail("instance_mismatch", "License instanceId does not match this instance.");
            }

            if (string.IsNullOrWhiteSpace(licenseKey))
                return VerificationResult.Fail("invalid_payload", "licenseKey is required.");

            if (!TierRank.ContainsKey(tier))
                return VerificationResult.Fail("invalid_payload", "tier is invalid.");

            if (!DateTimeOffset.TryParse(
                    issuedAtText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var issuedAt))
            {
                return VerificationResult.Fail("invalid_payload", "issuedAtUtc is invalid.");
            }

            if (!DateTimeOffset.TryParse(
                    expiresAtText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var expiresAt))
            {
                return VerificationResult.Fail("invalid_payload", "expiresAtUtc is invalid.");
            }

            if (expiresAt <= DateTimeOffset.UtcNow)
                return VerificationResult.Fail("expired", "License is expired.");

            var featuresJson = payload.TryGetProperty("features", out var features)
                ? features.GetRawText()
                : "[]";

            var limitsJson = payload.TryGetProperty("limits", out var limits)
                ? limits.GetRawText()
                : "{}";

            return VerificationResult.Success(
                KeyId: kid,
                LicenseKey: licenseKey,
                Tier: tier,
                Header: header,
                Payload: payload,
                PayloadJson: payloadJson,
                FeaturesJson: featuresJson,
                LimitsJson: limitsJson,
                IssuedAtUtc: issuedAt,
                ExpiresAtUtc: expiresAt,
                InstanceId: instanceId);
        }
        catch (Exception ex)
        {
            return VerificationResult.Fail("tampered", ex.Message);
        }
    }

    private static string? JsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
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

    private sealed record ParsedHeader(string KeyId);

    private sealed record CurrentVerifiedLicense(string LicenseKey, string Tier);

    private sealed record VerificationResult(
        bool IsValid,
        string Status,
        string? Error,
        string KeyId,
        string LicenseKey,
        string Tier,
        JsonElement? Header,
        JsonElement? Payload,
        string PayloadJson,
        string FeaturesJson,
        string LimitsJson,
        DateTimeOffset IssuedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        string? InstanceId,
        object? Features,
        object? Limits)
    {
        public static VerificationResult Fail(string status, string error)
        {
            return new VerificationResult(
                IsValid: false,
                Status: status,
                Error: error,
                KeyId: "",
                LicenseKey: "",
                Tier: "",
                Header: null,
                Payload: null,
                PayloadJson: "{}",
                FeaturesJson: "[]",
                LimitsJson: "{}",
                IssuedAtUtc: DateTimeOffset.MinValue,
                ExpiresAtUtc: DateTimeOffset.MinValue,
                InstanceId: null,
                Features: Array.Empty<string>(),
                Limits: new { });
        }

        public static VerificationResult Success(
            string KeyId,
            string LicenseKey,
            string Tier,
            JsonElement Header,
            JsonElement Payload,
            string PayloadJson,
            string FeaturesJson,
            string LimitsJson,
            DateTimeOffset IssuedAtUtc,
            DateTimeOffset ExpiresAtUtc,
            string? InstanceId)
        {
            return new VerificationResult(
                IsValid: true,
                Status: "valid",
                Error: null,
                KeyId: KeyId,
                LicenseKey: LicenseKey,
                Tier: Tier,
                Header: Header,
                Payload: Payload,
                PayloadJson: PayloadJson,
                FeaturesJson: FeaturesJson,
                LimitsJson: LimitsJson,
                IssuedAtUtc: IssuedAtUtc,
                ExpiresAtUtc: ExpiresAtUtc,
                InstanceId: InstanceId,
                Features: JsonDocument.Parse(FeaturesJson).RootElement.Clone(),
                Limits: JsonDocument.Parse(LimitsJson).RootElement.Clone());
        }
    }
}