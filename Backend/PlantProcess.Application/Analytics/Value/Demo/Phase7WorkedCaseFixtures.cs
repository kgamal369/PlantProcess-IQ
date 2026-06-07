
using PlantProcess.Application.Analytics.Value;

namespace PlantProcess.Application.Analytics.Value.Demo;

/// <summary>
/// PPIQ_REALIZATION_T038_EUR_28K_56K_WORKED_CASE_FIXTURE.
/// Deterministic demo fixture for the doctrine worked case.
/// This is a demo/proof fixture, not a hard-coded production claim and not a guaranteed saving.
/// </summary>
public static class Phase7WorkedCaseFixtures
{
    public const string CaseCode = "PPIQ-P07-T038-EDGE-CRACK-EUR-28K-56K";
    public const string FindingRef = "finding:edge-crack-demo-28k-56k";
    public const string CoilId = "DEMO-COIL-EDGE-CRACK-001";
    public const string DefectCode = "EDGE_CRACK";

    public static Phase7WorkedCase EdgeCrackEur28k56k()
    {
        var assumptions = new CostAssumptionSet(
            Version: 38,
            Currency: "EUR",
            CostPerTon: null,
            DowngradeDeltaPerTon: new CostBand(140m, 210m, 280m),
            ScrapCostPerTon: new CostBand(300m, 400m, 500m),
            DowntimeCostPerMin: new CostBand(50m, 75m, 100m),
            GradePremiumPerTon: new CostBand(100m, 150m, 200m),
            EnergyPricePerMwh: null)
        {
            CreatedBy = "phase-07-t038-worked-case",
            CreatedAtUtc = new DateTimeOffset(2026, 6, 7, 0, 0, 0, TimeSpan.Zero),
            EffectiveFromUtc = new DateTimeOffset(2026, 6, 7, 0, 0, 0, TimeSpan.Zero)
        };

        var inputs = new ValueImpactInputs(
            FindingRef,
            CoilId,
            DefectCode,
            DefectRateDelta: 0.02m,
            MonthlyVolumeTons: 10_000m,
            ProductionStopMinutes: 0m,
            YieldLossTons: 0m,
            UseScrapCost: false);

        return new Phase7WorkedCase(
            CaseCode,
            "Edge-crack downgrade worked case: EUR 28k-56k/month bounded impact.",
            inputs,
            assumptions,
            ExpectedLow: 28_000m,
            ExpectedMid: 42_000m,
            ExpectedHigh: 56_000m,
            DoctrineNote:
                "200 affected tons/month × EUR 140/210/280 downgrade delta per ton = EUR 28k/42k/56k per month. This is a projected bounded range, not a guaranteed saving.");
    }
}

public sealed record Phase7WorkedCase(
    string CaseCode,
    string Title,
    ValueImpactInputs Inputs,
    CostAssumptionSet Assumptions,
    decimal ExpectedLow,
    decimal ExpectedMid,
    decimal ExpectedHigh,
    string DoctrineNote);
