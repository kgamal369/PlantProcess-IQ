// PPIQ-T022 - Production-refusal contract for test-mode switches.
// Pure unit tests (no host, no DB) - homed in Api.IntegrationTests because this is the
// test project that references PlantProcess.Api, where PpiqTestModeGuard lives.
using PlantProcess.Api.TestMode;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.TestMode;

[Trait("Task", "T022")]
public sealed class PpiqTestModeGuardTests
{
    [SkippableFact]
    public void Production_with_active_switch_and_no_accept_risk_is_refused()
    {
        var o = new PpiqTestModeOptions { SeedUsers = true };
        var ex = Assert.Throws<InvalidOperationException>(() => PpiqTestModeGuard.Validate(o, "Production"));
        Assert.Contains("REFUSED in Production", ex.Message);
    }

    [SkippableFact]
    public void Production_with_explicit_accept_risk_is_allowed()
    {
        var o = new PpiqTestModeOptions { SeedUsers = true, ForceTier = "Pro", IExplicitlyAcceptRisk = true };
        PpiqTestModeGuard.Validate(o, "Production");
    }

    [SkippableFact]
    public void Development_switches_are_allowed_without_accept_risk()
    {
        var o = new PpiqTestModeOptions { SeedUsers = true, ForceTier = "Enterprise" };
        PpiqTestModeGuard.Validate(o, "Development");
    }

    [SkippableTheory]
    [InlineData("Gold")]
    [InlineData("enterprise-plus")]
    public void Invalid_ForceTier_values_are_rejected(string tier)
    {
        var o = new PpiqTestModeOptions { ForceTier = tier };
        Assert.Throws<InvalidOperationException>(() => PpiqTestModeGuard.Validate(o, "Development"));
    }

    [SkippableFact]
    public void Inactive_options_never_throw_anywhere()
    {
        PpiqTestModeGuard.Validate(new PpiqTestModeOptions(), "Production");
    }
}