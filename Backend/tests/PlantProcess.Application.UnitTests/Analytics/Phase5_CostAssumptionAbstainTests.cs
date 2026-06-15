using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

/// <summary>PPIQ-504: an incomplete per-tenant cost table makes the value engine abstain.</summary>
public sealed class Phase5_CostAssumptionAbstainTests
{
    private static ValueImpactInputs Inputs() => new(
        FindingRef: "PPIQ-FIND-504",
        CoilId: "C-0044170",
        DefectCode: "edge_crack",
        DefectRateDelta: 0.02m,
        MonthlyVolumeTons: 5000m,
        ProductionStopMinutes: 30m,
        YieldLossTons: 12m,
        UseScrapCost: false);

    private static CostAssumptionSet Complete() => new(
        Version: 1,
        Currency: "EUR",
        CostPerTon: new CostBand(800m, 820m, 840m),
        DowngradeDeltaPerTon: new CostBand(100m, 120m, 140m),
        ScrapCostPerTon: new CostBand(600m, 650m, 700m),
        DowntimeCostPerMin: new CostBand(50m, 60m, 70m),
        GradePremiumPerTon: new CostBand(200m, 250m, 300m),
        EnergyPricePerMwh: new CostBand(60m, 70m, 80m));

    [Fact]
    public void PPIQ_504_Incomplete_cost_table_abstains_no_fabricated_euro()
    {
        var incomplete = Complete() with { DowntimeCostPerMin = null };
        var result = new ValueImpactEngine().Compute(Inputs(), incomplete);

        Assert.True(result.IsAbstained);
        Assert.Equal(0m, result.Mid);
        Assert.Equal(0m, result.High);
        Assert.False(string.IsNullOrWhiteSpace(result.AbstainReason));
    }

    [Fact]
    public void PPIQ_504_Complete_cost_table_yields_a_bounded_euro_range()
    {
        var result = new ValueImpactEngine().Compute(Inputs(), Complete());

        Assert.False(result.IsAbstained);
        Assert.True(result.Low <= result.Mid && result.Mid <= result.High);
        Assert.True(result.High > 0m);
    }
}