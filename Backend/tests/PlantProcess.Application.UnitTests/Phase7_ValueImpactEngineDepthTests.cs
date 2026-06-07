
using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class Phase7_ValueImpactEngineDepthTests
{
    private static CostAssumptionSet CompleteBands(int version = 7) => new(
        version,
        "EUR",
        CostPerTon: null,
        DowngradeDeltaPerTon: new CostBand(80m, 100m, 120m),
        ScrapCostPerTon: new CostBand(250m, 350m, 500m),
        DowntimeCostPerMin: new CostBand(40m, 60m, 80m),
        GradePremiumPerTon: new CostBand(150m, 200m, 250m),
        EnergyPricePerMwh: null);

    [Fact]
    public void T037_ComputesBoundedExpectedRange_WithProvenanceAndNoGuarantee()
    {
        var result = new ValueImpactEngine().Compute(
            new ValueImpactInputs(
                "finding:phase7-001",
                "COIL-0001",
                "EDGE_CRACK",
                DefectRateDelta: 0.02m,
                MonthlyVolumeTons: 10000m,
                ProductionStopMinutes: 120m,
                YieldLossTons: 50m),
            CompleteBands());

        Assert.False(result.IsAbstained);
        Assert.True(result.IsMonotonic);
        Assert.Equal(28300.00m, result.Low);
        Assert.Equal(37200.00m, result.Expected);
        Assert.Equal(46100.00m, result.High);
        Assert.Equal(result.Mid, result.Expected);
        Assert.Equal("BoundedRange", result.SupportStatus);
        Assert.Contains("not a guaranteed saving", result.HonestyCaveat);
        Assert.All(result.Terms, term =>
        {
            Assert.True(term.IsMonotonic);
            Assert.Contains("findingRef", term.InputsJson);
            Assert.False(string.IsNullOrWhiteSpace(term.Handle.Id));
        });
    }

    [Fact]
    public void T037_Abstains_WhenScrapBandRequiredButMissing()
    {
        var assumptions = CompleteBands() with { ScrapCostPerTon = null };

        var result = new ValueImpactEngine().Compute(
            new ValueImpactInputs(
                "finding:phase7-002",
                "COIL-0002",
                "SCRAP_RISK",
                DefectRateDelta: 0.01m,
                MonthlyVolumeTons: 10000m,
                ProductionStopMinutes: 10m,
                YieldLossTons: 5m,
                UseScrapCost: true),
            assumptions);

        Assert.True(result.IsAbstained);
        Assert.Equal(0m, result.Expected);
        Assert.Contains("scrap_cost_per_ton", result.AbstainReason);
        Assert.Contains("No value claim emitted", result.HonestyCaveat);
    }

    [Fact]
    public void T037_Abstains_WhenBandIsNotLowExpectedHigh()
    {
        var assumptions = CompleteBands() with
        {
            DowntimeCostPerMin = new CostBand(80m, 60m, 40m)
        };

        var result = new ValueImpactEngine().Compute(
            new ValueImpactInputs(
                "finding:phase7-003",
                null,
                null,
                DefectRateDelta: 0.01m,
                MonthlyVolumeTons: 10000m,
                ProductionStopMinutes: 60m,
                YieldLossTons: 10m),
            assumptions);

        Assert.True(result.IsAbstained);
        Assert.Contains("downtime_cost_per_min", result.AbstainReason);
        Assert.Contains("low <= expected <= high", result.AbstainReason);
    }

    [Fact]
    public void T037_NegativeImprovementFactor_StillProducesMonotonicBoundedRange()
    {
        var result = new ValueImpactEngine().Compute(
            new ValueImpactInputs(
                "finding:phase7-004",
                "COIL-0004",
                "EDGE_CRACK",
                DefectRateDelta: -0.02m,
                MonthlyVolumeTons: 10000m,
                ProductionStopMinutes: 0m,
                YieldLossTons: 0m),
            CompleteBands());

        Assert.False(result.IsAbstained);
        Assert.True(result.IsMonotonic);
        Assert.Equal(-24000.00m, result.Low);
        Assert.Equal(-20000.00m, result.Expected);
        Assert.Equal(-16000.00m, result.High);
    }
}
