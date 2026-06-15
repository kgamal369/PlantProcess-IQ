using System;
using PlantProcess.Application.Licensing.Phase10;
using Xunit;

namespace PlantProcess.Application.UnitTests.Licensing;

/// <summary>PPIQ-502: entitlements derive from the signed token; mutating the tier changes nothing.</summary>
public sealed class Phase5_LicenseTierTamperTests
{
    private static Phase10OfflineLicenseEnvelope SignedPro()
    {
        var now = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        return Phase10OfflineLicenseActivation.CreateSignedDemoEnvelope(
            "lic-ppiq-502", "tenant-demo", Phase10LicenseTier.Pro, now, now.AddYears(1));
    }

    [Fact]
    public void PPIQ_502_Signed_tier_verifies_and_resolves_from_signature()
    {
        var result = Phase10OfflineLicenseActivation.Verify(SignedPro());

        Assert.True(result.Accepted);
        Assert.Equal(Phase10LicenseTier.Pro, result.ActivatedTier);
        Assert.False(result.Audit.TamperRejected);
    }

    [Fact]
    public void PPIQ_502_Mutating_the_tier_row_changes_nothing_signature_wins()
    {
        // Forge a higher tier onto the Pro-signed envelope, as if someone edited the DB tier row.
        var forged = SignedPro() with { Tier = Phase10LicenseTier.Enterprise };
        var result = Phase10OfflineLicenseActivation.Verify(forged);

        Assert.False(result.Accepted);
        Assert.True(result.Audit.TamperRejected);
        Assert.NotEqual(Phase10LicenseTier.Enterprise, result.ActivatedTier ?? Phase10LicenseTier.Light);
    }
}