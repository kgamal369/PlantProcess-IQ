using System.Linq;
using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests;

public class ValueImpactEngineTests
{
    private static readonly ValueImpactEngine Engine = new();

    // v4 demo defaults (§7.5 DX51D worked case).
    private static CostAssumptionSet DemoAssumptions(int version = 1) => new(
        version, "EUR",
        CostPerTon: new CostBand(600, 700, 820),
        DowngradeDeltaPerTon: new CostBand(80, 120, 160),
        ScrapCostPerTon: new CostBand(240, 300, 360),
        DowntimeCostPerMin: new CostBand(100, 150, 200),
        GradePremiumPerTon: new CostBand(110, 155, 200),
        EnergyPricePerMwh: new CostBand(60, 85, 120));

    // DX51D edge-crack: 2% defect-rate delta on 8000 t/mo; 90 production-stop min; 60 t yield loss.
    private static ValueImpactInputs Dx51dEdgeCrack() => new(
        FindingRef: "finding-dx51d-edgecrack",
        CoilId: "C00001-001",
        DefectCode: "EDGE_CRACK",
        DefectRateDelta: 0.02m,
        MonthlyVolumeTons: 8000m,
        ProductionStopMinutes: 90m,
        YieldLossTons: 60m);

    [Fact]
    public void DX51D_edge_crack_yields_expected_mid_and_band()
    {
        var r = Engine.Compute(Dx51dEdgeCrack(), DemoAssumptions());

        Assert.False(r.IsAbstained);
        Assert.Equal(42000m, r.Mid);   // 19200 + 13500 + 9300
        Assert.Equal(28400m, r.Low);   // 12800 + 9000 + 6600
        Assert.Equal(55600m, r.High);  // 25600 + 18000 + 12000
    }

    [Fact]
    public void Every_term_exposes_inputs_and_a_resolvable_handle()
    {
        var r = Engine.Compute(Dx51dEdgeCrack(), DemoAssumptions());
        Assert.Equal(3, r.Terms.Count);
        Assert.All(r.Terms, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.InputsJson));
            Assert.NotNull(t.Handle);
            Assert.False(string.IsNullOrWhiteSpace(t.Handle.Id));
        });
    }

    [Fact]
    public void Downtime_term_uses_production_stop_minutes_not_raw_downtime()
    {
        var withProductionStop = Engine.Compute(Dx51dEdgeCrack(), DemoAssumptions());

        // Raw equipment-stop minutes (e.g. 312 from the caster cascade) would inflate the result.
        var rawInputs = Dx51dEdgeCrack() with { ProductionStopMinutes = 312m };
        var withRaw = Engine.Compute(rawInputs, DemoAssumptions());

        Assert.Equal(42000m, withProductionStop.Mid);
        Assert.NotEqual(withProductionStop.Mid, withRaw.Mid); // contract: production-stop, not raw
    }

    [Fact]
    public void Missing_downgrade_cost_per_ton_abstains_with_named_assumption()
    {
        var assumptions = DemoAssumptions() with { DowngradeDeltaPerTon = null };
        var r = Engine.Compute(Dx51dEdgeCrack(), assumptions);

        Assert.True(r.IsAbstained);
        Assert.Equal(0m, r.Mid);
        Assert.Contains("downgrade_delta_per_ton", r.AbstainReason!);
    }

    [Fact]
    public void Identical_inputs_are_deterministic()
    {
        var a = Engine.Compute(Dx51dEdgeCrack(), DemoAssumptions());
        var b = Engine.Compute(Dx51dEdgeCrack(), DemoAssumptions());
        Assert.Equal(a.Low, b.Low);
        Assert.Equal(a.Mid, b.Mid);
        Assert.Equal(a.High, b.High);
        Assert.Equal(a.Terms.Select(t => t.Mid), b.Terms.Select(t => t.Mid));
    }
}