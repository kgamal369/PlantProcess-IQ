// PPIQ-T08 - pin the tier->feature resolution against the REAL map:
// VerifiedEd25519LicenseService.RequiredTierByFeature (private static, read via
// reflection - it has no public accessor by design). Values verified against the
// 12-Jun source. If a pinned tier ever changes, that is a PRICING decision and this
// test failing is exactly the review trigger it should be.
using System.Reflection;
using PlantProcess.Api.SignedLicensing;
using PlantProcess.Application.Licensing.Contracts;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Licensing;

[Trait("Task", "T08")]
public sealed class T08TierFeatureSnapshotTests
{
    private static IReadOnlyDictionary<LicenseFeature, LicenseTier> Map()
    {
        var field = typeof(VerifiedEd25519LicenseService).GetField(
            "RequiredTierByFeature", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return (IReadOnlyDictionary<LicenseFeature, LicenseTier>)field!.GetValue(null)!;
    }

    [SkippableTheory]
    [InlineData(LicenseFeature.WidgetScriptLayer,       LicenseTier.ProPlus)]
    [InlineData(LicenseFeature.OpcUaHistorianConnector, LicenseTier.Enterprise)]
    [InlineData(LicenseFeature.SchemaSqlViewBuilder,    LicenseTier.Pro)]
    [InlineData(LicenseFeature.CorrelationScheduledRun, LicenseTier.ProPlus)]
    [InlineData(LicenseFeature.InvestigationWorkflow,   LicenseTier.ProPlus)]
    [InlineData(LicenseFeature.DbLinkConfiguration,     LicenseTier.Light)]
    [InlineData(LicenseFeature.CsvImport,               LicenseTier.Light)]
    [InlineData(LicenseFeature.SqlServerConnector,      LicenseTier.Enterprise)]
    public void Headline_feature_minimum_tier_is_stable(LicenseFeature feature, LicenseTier expectedMinimum)
    {
        Assert.Equal(expectedMinimum, Map()[feature]);
    }

    [SkippableFact]
    public void Every_license_feature_has_a_tier_mapping()
    {
        var map = Map();
        var missing = Enum.GetValues<LicenseFeature>().Where(f => !map.ContainsKey(f)).ToList();
        Assert.True(missing.Count == 0,
            "PPIQ-T08: LicenseFeature members without a RequiredTierByFeature entry - " +
            "an unmapped feature silently resolves to default behavior: " + string.Join(", ", missing));
    }
}