
using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Advanced;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics.Advanced;

/// <summary>
/// PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE.
/// Certifies that every finding exposes population, stratification and exclusion evidence.
/// </summary>
public sealed class Phase8_T044FindingTransparencyTests
{
    private sealed class FakeLoader : IFeatureVectorLoader
    {
        private readonly AdvancedDataset _dataset;

        public FakeLoader(AdvancedDataset dataset) => _dataset = dataset;

        public Task<AdvancedDataset> LoadAsync(AdvancedAnalysisRequest request, CancellationToken ct)
            => Task.FromResult(_dataset);
    }

    private sealed class CapturingWriter : IAdvancedResultWriter
    {
        public AdvancedAnalysisRunResult? Last { get; private set; }

        public Task<Guid> WriteAsync(AdvancedAnalysisRequest request, AdvancedAnalysisRunResult result, CancellationToken ct)
        {
            Last = result;
            return Task.FromResult(result.RunId);
        }
    }

    private static AdvancedCorrelationComputeService Service(AdvancedDataset dataset)
        => new(
            new FakeLoader(dataset),
            new CapturingWriter(),
            NullLogger<AdvancedCorrelationComputeService>.Instance);

    private static AdvancedAnalysisRequest Request()
        => new(
            OutcomeKey: "defect.edge_crack_rate",
            Grain: "coil",
            WindowDays: 3650,
            TenantId: Guid.Parse("33333333-3333-3333-3333-333333333333"),
            FdrQ: 0.05,
            VifThreshold: 5.0,
            BootstrapIterations: 250,
            PermutationIterations: 80,
            CorrelationId: "PPIQ-T044-FINDING-TRANSPARENCY");

    private static FeatureSeries NumericSeries(string key, Func<int, double?> value, int n)
        => new(
            key,
            VariableType.Numeric,
            Enumerable.Range(0, n)
                .Select(i => new FeatureSample($"sample-{i:000}", value(i), null))
                .ToList());

    private static AdvancedDataset TransparentDataset()
    {
        const int n = 90;

        static double Temperature(int i)
            => i + ((i % 7) * 0.01);

        static double Pressure(int i)
            => (37 * i) % 83 + ((i % 5) * 0.01);

        var outcomes = Enumerable.Range(0, n)
            .Select(i => new OutcomeSample(
                $"sample-{i:000}",
                2.0 * Temperature(i) + 1.2 * Pressure(i),
                null,
                $"heat-{i:000}"))
            .ToList();

        var strata = Enumerable.Range(0, n)
            .ToDictionary(
                i => $"sample-{i:000}",
                i => i < 45 ? "caster-a" : "caster-b");

        var features = new List<FeatureSeries>
        {
            NumericSeries("param_temperature_population_full", i => Temperature(i), n),
            NumericSeries("param_pressure_population_partial", i => i % 10 == 0 ? null : Pressure(i), n),
            NumericSeries("param_noise_population_full", i => ((97 * i + 13) % 101) / 101.0, n)
        };

        return new AdvancedDataset(
            "defect.edge_crack_rate",
            VariableType.Numeric,
            outcomes,
            features,
            strata,
            IndependentHeats: n,
            FreshnessFactor: 0.0,
            RequiredFieldCompleteness: 1.0);
    }

    [Fact]
    public async Task T044_Every_Finding_Carries_Population_Stratification_And_Exclusion_Fields()
    {
        var result = await Service(TransparentDataset()).ComputeAsync(Request(), CancellationToken.None);

        Assert.True(result.CanRun);
        Assert.NotEmpty(result.Findings);

        Assert.All(result.Findings, finding =>
        {
            Assert.True(finding.SampleSize > 0, $"{finding.FeatureKey} must expose paired population/sample size.");
            Assert.True(finding.ExcludedRecords >= 0, $"{finding.FeatureKey} must expose dropped/excluded record count.");
            Assert.False(string.IsNullOrWhiteSpace(finding.StratificationReason));
            Assert.False(string.IsNullOrWhiteSpace(finding.ProvenanceHandle));
            Assert.Contains("not a guaranteed root cause", finding.HonestyCaveat, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task T044_Partial_Feature_Shows_Dropped_Record_Count()
    {
        var result = await Service(TransparentDataset()).ComputeAsync(Request(), CancellationToken.None);

        var partial = Assert.Single(result.Findings, f => f.FeatureKey == "param_pressure_population_partial");

        Assert.True(partial.SampleSize < 90);
        Assert.True(partial.ExcludedRecords > 0);
    }

    [Fact]
    public async Task T044_Transparency_Projector_Produces_Complete_Surface_For_Every_Finding()
    {
        var result = await Service(TransparentDataset()).ComputeAsync(Request(), CancellationToken.None);

        var transparency = AdvancedFindingTransparencyProjector.Project(result);

        Assert.Equal(result.Findings.Count, transparency.Count);

        Assert.All(transparency, item =>
        {
            Assert.True(item.IsComplete, $"{item.FeatureKey} transparency surface must be complete.");
            Assert.Contains("coil", item.PopulationLabel);
            Assert.Contains("window=3650d", item.PopulationLabel);
            Assert.Contains("defect.edge_crack_rate", item.PopulationLabel);
            Assert.True(item.PopulationSize >= item.PairedSampleSize);
            Assert.False(string.IsNullOrWhiteSpace(item.ExclusionSummary));
            Assert.False(string.IsNullOrWhiteSpace(item.StratificationReason));
            Assert.False(string.IsNullOrWhiteSpace(item.ProvenanceHandle));
        });
    }

    [Fact]
    public async Task T044_Stratification_Status_Is_Surfaceable_For_Every_Finding()
    {
        var result = await Service(TransparentDataset()).ComputeAsync(Request(), CancellationToken.None);

        var transparency = AdvancedFindingTransparencyProjector.Project(result);

        Assert.All(transparency, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.StratificationReason));
            Assert.True(item.StratificationEvaluated || item.StratificationReason.StartsWith("Stratification not evaluated", StringComparison.OrdinalIgnoreCase));
        });

        Assert.Contains(transparency, item => item.StratificationEvaluated);
    }
}
