namespace PlantProcess.Api.TestMode;

/// <summary>
/// PPIQ-T022: one switch surface for testing every auth/license combination locally
/// and on the server without code edits. Bound from section "PPIQ_TESTMODE"
/// (env: PPIQ_TESTMODE__SeedUsers, PPIQ_TESTMODE__ForceTier, ...).
/// The full switch table lives in docs/TESTMODE.md - a guard test keeps them in sync.
/// </summary>
public sealed class PpiqTestModeOptions
{
    public const string SectionName = "PPIQ_TESTMODE";

    /// <summary>Seed one known user per role (Admin / Executive / ProcessEngineer / Operator).</summary>
    public bool SeedUsers { get; set; } = false;

    /// <summary>Override the effective license tier: Lite | Pro | ProPlus | Enterprise. Empty = off.</summary>
    public string? ForceTier { get; set; }

    /// <summary>Expose GET /admin/testmode-status echoing every active toggle.</summary>
    public bool StatusEndpoint { get; set; } = true;

    /// <summary>Required to run ANY test-mode switch when ASPNETCORE_ENVIRONMENT=Production.</summary>
    public bool IExplicitlyAcceptRisk { get; set; } = false;

    // PPIQ-T08: canonical member is 'Light' (LicenseTier.Light=1). 'Lite' is accepted as a
    // legacy spelling and normalized so existing docs/env files keep working.
    public static readonly string[] ValidTiers = { "Light", "Lite", "Pro", "ProPlus", "Enterprise" };

    public static string NormalizeTier(string tier) =>
        string.Equals(tier, "Lite", StringComparison.OrdinalIgnoreCase) ? "Light" : tier;

    public bool AnySwitchActive =>
        SeedUsers || !string.IsNullOrWhiteSpace(ForceTier);
}

/// <summary>Pure, dependency-free validation so the Production-refusal rule is unit-testable.</summary>
public static class PpiqTestModeGuard
{
    public static void Validate(PpiqTestModeOptions options, string environmentName)
    {
        if (!string.IsNullOrWhiteSpace(options.ForceTier)
            && !PpiqTestModeOptions.ValidTiers.Contains(options.ForceTier, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"PPIQ-T022: ForceTier '{options.ForceTier}' is invalid. Valid: {string.Join("|", PpiqTestModeOptions.ValidTiers)}.");
        }

        var isProduction = string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
        if (isProduction && options.AnySwitchActive && !options.IExplicitlyAcceptRisk)
        {
            throw new InvalidOperationException(
                "PPIQ-T022: test-mode switches are REFUSED in Production. " +
                "Set PPIQ_TESTMODE__IExplicitlyAcceptRisk=true ONLY if you accept the risk on this server.");
        }
    }
}