using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class Phase3_ValueImpactEngineTests
{
    private static CostAssumptionSet CompleteBands(int version = 1) => new(
        version, "EUR",
        CostPerTon: null,
        DowngradeDeltaPerTon: new CostBand(80m, 100m, 120m),
        ScrapCostPerTon: null,
        DowntimeCostPerMin: new CostBand(40m, 60m, 80m),
        GradePremiumPerTon: new CostBand(150m, 200m, 250m),
        EnergyPricePerMwh: null);

    private static ValueImpactInputs SampleInputs() => new(
        FindingRef: "finding:demo-001",
        CoilId: "COIL-0001",
        DefectCode: "EDGE_CRACK",
        DefectRateDelta: 0.02m,
        MonthlyVolumeTons: 10000m,
        ProductionStopMinutes: 120m,
        YieldLossTons: 50m,
        UseScrapCost: false);

    [Fact]
    public void Compute_ProducesDeterministicRange_FromBands()
    {
        var result = new ValueImpactEngine().Compute(SampleInputs(), CompleteBands());

        Assert.False(result.IsAbstained);
        Assert.Null(result.AbstainReason);
        Assert.Equal("EUR", result.Currency);
        Assert.Equal(1, result.AssumptionVersion);
        Assert.Equal(3, result.Terms.Count);

        // defect:   0.02*10000=200t -> 200*{80,100,120} = {16000,20000,24000}
        // downtime: 120min          -> 120*{40,60,80}   = { 4800, 7200, 9600}
        // yield:    50t             ->  50*{150,200,250} = { 7500,10000,12500}
        // total                                          = {28300,37200,46100}
        Assert.Equal(28300.00m, result.Low);
        Assert.Equal(37200.00m, result.Mid);
        Assert.Equal(46100.00m, result.High);
        Assert.True(result.Low < result.Mid && result.Mid < result.High);
    }

    [Fact]
    public void Compute_Abstains_WhenRequiredBandMissing()
    {
        var bands = CompleteBands() with { DowntimeCostPerMin = null };
        var result = new ValueImpactEngine().Compute(SampleInputs(), bands);

        Assert.True(result.IsAbstained);
        Assert.Equal(0m, result.Low);
        Assert.Equal(0m, result.Mid);
        Assert.Equal(0m, result.High);
        Assert.Empty(result.Terms);
        Assert.NotNull(result.AbstainReason);
        Assert.Contains("downtime_cost_per_min", result.AbstainReason!);
    }

    [Fact]
    public void Compute_Abstains_WhenBandIncomplete()
    {
        var bands = CompleteBands() with { GradePremiumPerTon = new CostBand(300m, 200m, 250m) };
        var result = new ValueImpactEngine().Compute(SampleInputs(), bands);

        Assert.True(result.IsAbstained);
        Assert.Contains("grade_premium_per_ton", result.AbstainReason!);
    }
}