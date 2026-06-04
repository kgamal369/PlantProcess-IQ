using PlantProcess.Analytics.Core.Kpi;
using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class P3_ValueEvidenceKpiCoverageTests
{
    private static CostAssumptionSet CompleteBands(int version = 7) => new(
        version,
        "EUR",
        CostPerTon: null,
        DowngradeDeltaPerTon: new CostBand(80m, 100m, 120m),
        ScrapCostPerTon: new CostBand(180m, 220m, 260m),
        DowntimeCostPerMin: new CostBand(40m, 60m, 80m),
        GradePremiumPerTon: new CostBand(150m, 200m, 250m),
        EnergyPricePerMwh: null);

    [Fact]
    public void T016_cost_assumption_validator_rejects_negative_and_unordered_bands()
    {
        var invalid = CompleteBands() with
        {
            DowngradeDeltaPerTon = new CostBand(100m, 70m, 90m),
            DowntimeCostPerMin = new CostBand(-1m, 20m, 30m)
        };

        var errors = CostAssumptionValidator.Validate(invalid);

        Assert.Contains(errors, x => x.Contains("downgradeDeltaPerTon") && x.Contains("low <= mid <= high"));
        Assert.Contains(errors, x => x.Contains("downtimeCostPerMin") && x.Contains("non-negative"));
    }

    [Fact]
    public void T018_value_range_reproduces_worked_range_and_carries_provenance_terms()
    {
        var inputs = new ValueImpactInputs(
            FindingRef: "finding:p3-worked-example",
            CoilId: "MAT-1001",
            DefectCode: "DEFECT-A",
            DefectRateDelta: 0.02m,
            MonthlyVolumeTons: 10000m,
            ProductionStopMinutes: 120m,
            YieldLossTons: 50m,
            UseScrapCost: false);

        var result = new ValueImpactEngine().Compute(inputs, CompleteBands());

        Assert.False(result.IsAbstained);
        Assert.Equal(28300.00m, result.Low);
        Assert.Equal(37200.00m, result.Mid);
        Assert.Equal(46100.00m, result.High);
        Assert.Equal(3, result.Terms.Count);
        Assert.All(result.Terms, term => Assert.NotNull(term.Handle));
    }

    [Fact]
    public void T018_value_engine_abstains_when_basis_missing()
    {
        var bands = CompleteBands() with { DowntimeCostPerMin = null };

        var result = new ValueImpactEngine().Compute(
            new ValueImpactInputs("finding:p3-abstain", null, null, 0.01m, 1000m, 60m, 5m),
            bands);

        Assert.True(result.IsAbstained);
        Assert.Contains("downtime_cost_per_min", result.AbstainReason);
        Assert.Equal(0m, result.Low);
        Assert.Empty(result.Terms);
    }

    [Fact]
    public void T019_kpi_sql_view_definition_evaluates_threshold_severity()
    {
        var kpi = new KpiDefinition(
            Code: "first_pass_quality_yield",
            Name: "First-pass quality yield",
            Kind: KpiKind.SqlView,
            Expression: null,
            SqlView: "SELECT value FROM canon.vw_kpi_first_pass_quality_yield LIMIT 1",
            MeasureCode: null,
            Unit: "%",
            TenantTargets: new Dictionary<string, double> { ["tenant-a"] = 95.0 },
            Threshold: new KpiThreshold(90.0, 85.0, AlertDirection.BelowTargetIsBad));

        var result = new KpiEngine().EvaluateMeasured(
            kpi,
            "tenant-a",
            measuredValue: 88.0,
            dataset: "canon.vw_kpi_first_pass_quality_yield",
            filters: Array.Empty<string>(),
            timeWindow: "last_30_days",
            refreshedAtUtc: DateTimeOffset.UtcNow,
            sampleSize: 5142);

        Assert.Equal(KpiSeverity.Warning, result.Severity);
        Assert.True(result.AlertRaised);
        Assert.Equal("%", result.Metadata.Unit);
    }

    [Fact]
    public void T020_coverage_reconciles_population_and_completeness()
    {
        var coverage = new FindingCoverageEvidence(
            Population: 5604,
            Included: 5142,
            Excluded: 462,
            ExcludedReasons: new Dictionary<string, int>
            {
                ["missing_material_link"] = 398,
                ["ambiguous_business_key"] = 64
            });

        Assert.True(coverage.IsArithmeticallyConsistent);
        Assert.Equal(0.917559, coverage.Completeness, precision: 6);
        Assert.Contains("excluded", coverage.Summary);
    }

    [Fact]
    public void T021_blended_provenance_requires_weight_sum_to_one()
    {
        var evidence = new BlendedProvenanceEvidence(
            MaterialId: "MAT-TRANSITION-001",
            IsTransition: true,
            Contributors:
            [
                new BlendedProvenanceContributor("PARENT-A", 0.70m, "modeled-transition-window", "modeled"),
                new BlendedProvenanceContributor("PARENT-B", 0.30m, "modeled-transition-window", "modeled")
            ],
            HonestyCaveat: "Attribution is shared because this material is blended/transition material.");

        Assert.True(evidence.IsValid);
        Assert.Equal(1.0m, evidence.WeightTotal);
    }
}