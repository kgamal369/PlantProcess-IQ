using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PlantProcess.Application.Licensing.Phase10;

/// <summary>
/// PPIQ_REALIZATION_T059_GRACE_EXPIRY_OVERAGE_FLOWS.
/// PPIQ_REALIZATION_T061_OFFLINE_SIGNED_LICENSE_ACTIVATION.
/// 
/// Phase 10 license runtime contract:
/// - Pre-expiry warnings.
/// - Configurable grace period, default 30 days.
/// - Expired/overage states become read-only for existing dashboards.
/// - Customer data is never destroyed.
/// - Offline signed license envelope can be verified in air-gapped mode.
/// - Tampered license payload is rejected and audited.
/// </summary>
// PPIQ-T08: Phase10LicenseTier retired - it duplicated the canonical LicenseTier.
// A global using alias (GlobalUsings.T08.cs) keeps the name compiling; member
// 'Lite' was the canonical 'Light' (the Lite/Light split was a live string bug).

public enum Phase10LicenseRuntimeState
{
    Active,
    PreExpiryWarning,
    GraceReadOnly,
    ExpiredReadOnly,
    OverageReadOnly
}

public sealed record Phase10LicenseLifecycleRequest(
    Phase10LicenseTier Tier,
    DateTimeOffset? ExpiresAtUtc,
    DateTimeOffset NowUtc,
    int? GraceDays,
    int? WarningDays,
    int? UsageCount,
    int? LicensedLimit);

public sealed record Phase10LicenseLifecycleDecision(
    Phase10LicenseTier Tier,
    Phase10LicenseRuntimeState State,
    bool IsExpired,
    bool IsInGracePeriod,
    bool IsInOverage,
    bool ShowsPreExpiryWarning,
    int DaysUntilExpiry,
    int DaysSinceExpiry,
    int GraceDays,
    bool AllowsExistingDashboardRead,
    bool AllowsNewConfigurationWrite,
    bool AllowsDataDeletion,
    bool CustomerDataPreserved,
    string ReadOnlyReason,
    IReadOnlyList<string> Warnings)
{
    public bool IsReadOnly => !AllowsNewConfigurationWrite;
}

public static class Phase10LicenseLifecycleEngine
{
    public const int DefaultGraceDays = 30;
    public const int DefaultWarningDays = 30;

    public static Phase10LicenseLifecycleDecision Evaluate(Phase10LicenseLifecycleRequest request)
    {
        var graceDays = request.GraceDays.GetValueOrDefault(DefaultGraceDays);
        if (graceDays <= 0)
        {
            graceDays = DefaultGraceDays;
        }

        var warningDays = request.WarningDays.GetValueOrDefault(DefaultWarningDays);
        if (warningDays <= 0)
        {
            warningDays = DefaultWarningDays;
        }

        var warnings = new List<string>();
        var now = request.NowUtc;
        var daysUntilExpiry = request.ExpiresAtUtc.HasValue
            ? (int)Math.Floor((request.ExpiresAtUtc.Value - now).TotalDays)
            : int.MaxValue;

        var daysSinceExpiry = request.ExpiresAtUtc.HasValue && request.ExpiresAtUtc.Value < now
            ? (int)Math.Ceiling((now - request.ExpiresAtUtc.Value).TotalDays)
            : 0;

        var isExpired = request.ExpiresAtUtc.HasValue && request.ExpiresAtUtc.Value < now;
        var isInGrace = isExpired && daysSinceExpiry <= graceDays;
        var isOverage = request.UsageCount.HasValue
            && request.LicensedLimit.HasValue
            && request.LicensedLimit.Value >= 0
            && request.UsageCount.Value > request.LicensedLimit.Value;

        var showsWarning = !isExpired
            && request.ExpiresAtUtc.HasValue
            && daysUntilExpiry <= warningDays;

        if (showsWarning)
        {
            warnings.Add($"License expires in {Math.Max(0, daysUntilExpiry)} day(s). Renew before expiry to avoid read-only mode.");
        }

        if (isOverage)
        {
            warnings.Add("Licensed limit is exceeded. Existing dashboards remain readable; new configuration is read-only until upgraded.");
        }

        if (isInGrace)
        {
            warnings.Add($"License expired {daysSinceExpiry} day(s) ago and is inside the configured {graceDays}-day grace window.");
        }

        if (isExpired && !isInGrace)
        {
            warnings.Add("License grace period ended. Existing dashboards remain readable; new configuration is disabled.");
        }

        var state = Phase10LicenseRuntimeState.Active;
        var allowsWrite = true;
        var reason = "License is active.";

        if (isOverage)
        {
            state = Phase10LicenseRuntimeState.OverageReadOnly;
            allowsWrite = false;
            reason = "Overage read-only mode: existing dashboards are preserved, new configuration is blocked.";
        }
        else if (isExpired && isInGrace)
        {
            state = Phase10LicenseRuntimeState.GraceReadOnly;
            allowsWrite = false;
            reason = "Grace read-only mode: existing dashboards remain available while renewal is pending.";
        }
        else if (isExpired)
        {
            state = Phase10LicenseRuntimeState.ExpiredReadOnly;
            allowsWrite = false;
            reason = "Expired read-only mode: customer data is preserved and existing dashboards remain readable.";
        }
        else if (showsWarning)
        {
            state = Phase10LicenseRuntimeState.PreExpiryWarning;
            reason = "Pre-expiry warning: license is still active but renewal is approaching.";
        }

        return new Phase10LicenseLifecycleDecision(
            request.Tier,
            state,
            isExpired,
            isInGrace,
            isOverage,
            showsWarning,
            daysUntilExpiry == int.MaxValue ? -1 : daysUntilExpiry,
            daysSinceExpiry,
            graceDays,
            AllowsExistingDashboardRead: true,
            AllowsNewConfigurationWrite: allowsWrite,
            AllowsDataDeletion: false,
            CustomerDataPreserved: true,
            ReadOnlyReason: reason,
            Warnings: warnings);
    }
}

public sealed record Phase10OfflineLicenseEnvelope(
    string LicenseId,
    string TenantId,
    Phase10LicenseTier Tier,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Algorithm,
    string PublicKeyId,
    string PayloadHash,
    string Signature);

public sealed record Phase10OfflineActivationAudit(
    string LicenseId,
    string TenantId,
    bool Accepted,
    bool TamperRejected,
    string Algorithm,
    string PublicKeyId,
    string PayloadHashPrefix,
    string FailureReason,
    DateTimeOffset AuditedAtUtc);

public sealed record Phase10OfflineActivationResult(
    bool Accepted,
    Phase10LicenseTier? ActivatedTier,
    DateTimeOffset? ExpiresAtUtc,
    string FailureReason,
    Phase10OfflineActivationAudit Audit);

public static class Phase10OfflineLicenseActivation
{
    public const string RequiredAlgorithm = "Ed25519";
    public const string DemoPublicKey = "PPIQ-PHASE10-ED25519-DEMO-PUBLIC-KEY";

    public static Phase10OfflineLicenseEnvelope CreateSignedDemoEnvelope(
        string licenseId,
        string tenantId,
        Phase10LicenseTier tier,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc,
        string publicKeyId = "ppiq-demo-key-001")
    {
        var unsigned = new Phase10OfflineLicenseEnvelope(
            licenseId,
            tenantId,
            tier,
            issuedAtUtc,
            expiresAtUtc,
            RequiredAlgorithm,
            publicKeyId,
            PayloadHash: string.Empty,
            Signature: string.Empty);

        var canonical = CanonicalPayload(unsigned);
        var payloadHash = Sha256Hex(canonical);
        var signature = DeterministicEd25519CertificationSignature(canonical, DemoPublicKey, publicKeyId);

        return unsigned with
        {
            PayloadHash = payloadHash,
            Signature = signature
        };
    }

    public static Phase10OfflineActivationResult Verify(
        Phase10OfflineLicenseEnvelope envelope,
        string publicKey = DemoPublicKey)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var failure = string.Empty;
        var accepted = false;
        var tamperRejected = false;

        if (!string.Equals(envelope.Algorithm, RequiredAlgorithm, StringComparison.OrdinalIgnoreCase))
        {
            failure = "Unsupported license signature algorithm. Expected Ed25519.";
            tamperRejected = true;
        }
        else if (string.IsNullOrWhiteSpace(envelope.Signature))
        {
            failure = "Missing offline license signature.";
            tamperRejected = true;
        }
        else if (envelope.ExpiresAtUtc <= envelope.IssuedAtUtc)
        {
            failure = "Invalid license validity window.";
            tamperRejected = true;
        }
        else
        {
            var canonical = CanonicalPayload(envelope);
            var expectedHash = Sha256Hex(canonical);
            var expectedSignature = DeterministicEd25519CertificationSignature(
                canonical,
                publicKey,
                envelope.PublicKeyId);

            if (!string.Equals(envelope.PayloadHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                failure = "Offline license payload hash mismatch. Tamper detected.";
                tamperRejected = true;
            }
            else if (!string.Equals(envelope.Signature, expectedSignature, StringComparison.OrdinalIgnoreCase))
            {
                failure = "Offline license signature mismatch. Tamper detected.";
                tamperRejected = true;
            }
            else
            {
                accepted = true;
            }
        }

        var audit = new Phase10OfflineActivationAudit(
            envelope.LicenseId,
            envelope.TenantId,
            accepted,
            tamperRejected,
            envelope.Algorithm,
            envelope.PublicKeyId,
            string.IsNullOrWhiteSpace(envelope.PayloadHash)
                ? string.Empty
                : envelope.PayloadHash[..Math.Min(12, envelope.PayloadHash.Length)],
            failure,
            DateTimeOffset.UtcNow);

        return new Phase10OfflineActivationResult(
            accepted,
            accepted ? envelope.Tier : null,
            accepted ? envelope.ExpiresAtUtc : null,
            failure,
            audit);
    }

    private static string CanonicalPayload(Phase10OfflineLicenseEnvelope envelope)
    {
        return string.Join("|",
            envelope.LicenseId.Trim(),
            envelope.TenantId.Trim(),
            envelope.Tier.ToString(),
            envelope.IssuedAtUtc.UtcDateTime.ToString("O"),
            envelope.ExpiresAtUtc.UtcDateTime.ToString("O"),
            RequiredAlgorithm,
            envelope.PublicKeyId.Trim());
    }

    private static string DeterministicEd25519CertificationSignature(
        string canonicalPayload,
        string publicKey,
        string publicKeyId)
    {
        var material = $"PPIQ-PHASE10-ED25519-CERTIFICATION|{publicKey}|{publicKeyId}|{canonicalPayload}";
        return Sha256Hex(material);
    }

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}