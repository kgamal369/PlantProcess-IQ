using System;
using PlantProcess.Application.Licensing.Phase10;
using Xunit;

namespace PlantProcess.Application.UnitTests.Licensing;

public sealed class Phase10LicenseRuntimeTests
{
    [Fact]
    public void T059_PreExpiry_Warning_Appears_Before_Expiry()
    {
        var now = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

        var decision = Phase10LicenseLifecycleEngine.Evaluate(new Phase10LicenseLifecycleRequest(
            Phase10LicenseTier.Pro,
            now.AddDays(15),
            now,
            GraceDays: 30,
            WarningDays: 30,
            UsageCount: 3,
            LicensedLimit: 10));

        Assert.Equal(Phase10LicenseRuntimeState.PreExpiryWarning, decision.State);
        Assert.True(decision.ShowsPreExpiryWarning);
        Assert.False(decision.IsReadOnly);
        Assert.Contains(decision.Warnings, x => x.Contains("expires in", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void T059_Expired_Inside_Grace_Drops_To_ReadOnly_But_Preserves_Dashboards()
    {
        var now = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

        var decision = Phase10LicenseLifecycleEngine.Evaluate(new Phase10LicenseLifecycleRequest(
            Phase10LicenseTier.ProPlus,
            now.AddDays(-7),
            now,
            GraceDays: 30,
            WarningDays: 30,
            UsageCount: 2,
            LicensedLimit: 10));

        Assert.Equal(Phase10LicenseRuntimeState.GraceReadOnly, decision.State);
        Assert.True(decision.IsReadOnly);
        Assert.True(decision.AllowsExistingDashboardRead);
        Assert.True(decision.CustomerDataPreserved);
        Assert.False(decision.AllowsDataDeletion);
    }

    [Fact]
    public void T059_Expired_After_Grace_Remains_ReadOnly_And_Never_Destroys_Data()
    {
        var now = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

        var decision = Phase10LicenseLifecycleEngine.Evaluate(new Phase10LicenseLifecycleRequest(
            Phase10LicenseTier.Lite,
            now.AddDays(-45),
            now,
            GraceDays: 30,
            WarningDays: 30,
            UsageCount: 1,
            LicensedLimit: 1));

        Assert.Equal(Phase10LicenseRuntimeState.ExpiredReadOnly, decision.State);
        Assert.True(decision.IsReadOnly);
        Assert.True(decision.AllowsExistingDashboardRead);
        Assert.True(decision.CustomerDataPreserved);
        Assert.False(decision.AllowsDataDeletion);
    }

    [Fact]
    public void T059_Overage_Drops_To_ReadOnly_Until_Upgrade()
    {
        var now = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

        var decision = Phase10LicenseLifecycleEngine.Evaluate(new Phase10LicenseLifecycleRequest(
            Phase10LicenseTier.Pro,
            now.AddDays(90),
            now,
            GraceDays: 30,
            WarningDays: 30,
            UsageCount: 12,
            LicensedLimit: 10));

        Assert.Equal(Phase10LicenseRuntimeState.OverageReadOnly, decision.State);
        Assert.True(decision.IsInOverage);
        Assert.True(decision.IsReadOnly);
        Assert.Contains("Overage", decision.ReadOnlyReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T061_Offline_Signed_License_Activates_In_Airgap_Mode()
    {
        var issued = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
        var envelope = Phase10OfflineLicenseActivation.CreateSignedDemoEnvelope(
            "lic-demo-001",
            "tenant-demo",
            Phase10LicenseTier.Enterprise,
            issued,
            issued.AddYears(1));

        var result = Phase10OfflineLicenseActivation.Verify(envelope);

        Assert.True(result.Accepted, result.FailureReason);
        Assert.Equal(Phase10LicenseTier.Enterprise, result.ActivatedTier);
        Assert.False(result.Audit.TamperRejected);
        Assert.Equal("Ed25519", result.Audit.Algorithm);
    }

    [Fact]
    public void T061_Tampered_Offline_License_Is_Rejected_And_Audited()
    {
        var issued = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);
        var envelope = Phase10OfflineLicenseActivation.CreateSignedDemoEnvelope(
            "lic-demo-002",
            "tenant-demo",
            Phase10LicenseTier.Pro,
            issued,
            issued.AddYears(1));

        var tampered = envelope with { Tier = Phase10LicenseTier.Enterprise };

        var result = Phase10OfflineLicenseActivation.Verify(tampered);

        Assert.False(result.Accepted);
        Assert.True(result.Audit.TamperRejected);
        Assert.Contains("Tamper", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }
}