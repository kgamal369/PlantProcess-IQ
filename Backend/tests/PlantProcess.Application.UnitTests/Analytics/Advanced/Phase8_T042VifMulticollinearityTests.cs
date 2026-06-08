
using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Advanced;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics.Advanced;

public sealed class Phase8_T042VifMulticollinearityTests
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

    private static AdvancedAnalysisRequest Request(double vifThreshold = 5.0)
        => new(
            OutcomeKey: "defect.edge_crack_rate",
            Grain: "coil",
            WindowDays: 3650,
            TenantId: Guid.NewGuid(),
            VifThreshold: vifThreshold,
            BootstrapIterations: 250,
            PermutationIterations: 80);

    private static FeatureSeries NumericSeries(string key, Func<int, double> value, int n)
        => new(
            key,
            VariableType.Numeric,
            Enumerable.Range(0, n)
                .Select(i => new FeatureSample($"k{i}", value(i), null))
                .ToList());

    private static AdvancedDataset DatasetWithDeliberateCollinearity()
    {
        const int n = 90;

        static double Temperature(int i) => i + ((i % 5) * 0.01);
        static double Pressure(int i) => ((37 * i) % 53) + ((i % 3) * 0.01);
        static double Noise(int i) => (17 * i) % 29;

        var outcomes = Enumerable.Range(0, n)
            .Select(i => new OutcomeSample(
                $"k{i}",
                3.0 * Temperature(i) + 1.5 * Pressure(i) + (((11 * i) % 7) - 3) * 0.05,
                null,
                $"heat-{i}"))
            .ToList();

        var features = new List<FeatureSeries>
        {
            NumericSeries("param_true_temperature", i => Temperature(i), n),
            NumericSeries("param_true_temperature_duplicate", i => Temperature(i) * 2.0 + 100.0, n),
            NumericSeries("param_independent_pressure", i => Pressure(i), n),
            NumericSeries("param_noise_randomized", i => Noise(i), n)
        };

        return new AdvancedDataset(
            "defect.edge_crack_rate",
            VariableType.Numeric,
            outcomes,
            features,
            new Dictionary<string, string>(),
            IndependentHeats: n,
            FreshnessFactor: 0.0,
            RequiredFieldCompleteness: 1.0);
    }

    [Fact]
    public async Task T042_Detects_And_Excludes_Collinear_Duplicate_By_Vif()
    {
        var result = await Service(DatasetWithDeliberateCollinearity())
            .ComputeAsync(Request(vifThreshold: 5.0), CancellationToken.None);

        Assert.True(result.CanRun);

        var excludedDuplicate = Assert.Single(
            result.Excluded,
            e => e.FeatureKey == "param_true_temperature_duplicate");

        Assert.Contains("Collinear", excludedDuplicate.Reason);
        Assert.Contains("VIF", excludedDuplicate.Reason);
        Assert.Contains("removed to keep one representative", excludedDuplicate.Reason);

        Assert.Contains(result.Findings, f => f.FeatureKey == "param_true_temperature");
        Assert.DoesNotContain(result.Findings, f => f.FeatureKey == "param_true_temperature_duplicate");
    }

    [Fact]
    public async Task T042_Effect_Ranking_Remains_Stable_After_Vif_Pruning()
    {
        var result = await Service(DatasetWithDeliberateCollinearity())
            .ComputeAsync(Request(vifThreshold: 5.0), CancellationToken.None);

        Assert.True(result.CanRun);

        var collinearPairInFindings = result.Findings.Count(f =>
            f.FeatureKey == "param_true_temperature" ||
            f.FeatureKey == "param_true_temperature_duplicate");

        Assert.Equal(1, collinearPairInFindings);

        var top = result.Findings
            .OrderByDescending(f => Math.Abs(f.EffectSize))
            .First();

        Assert.Contains(top.FeatureKey, new[]
        {
            "param_true_temperature",
            "param_independent_pressure"
        });

        Assert.All(result.Findings, f => Assert.InRange(f.QValue, 0.0, 1.0));
        Assert.All(result.Findings, f => Assert.False(double.IsNaN(f.EffectSize)));
    }

    [Fact]
    public void T042_Core_VarianceInflation_Flags_Deliberate_Collinearity()
    {
        const int n = 50;

        var matrix = Enumerable.Range(0, n)
            .Select(i => new[]
            {
                (double)i,
                (double)i * 2.0 + 1.0,
                (double)((13 * i) % 17)
            })
            .ToArray();

        var vif = VarianceInflation.Compute(matrix, threshold: 5.0);

        Assert.Equal(5.0, vif.Threshold);
        Assert.Contains(0, vif.Flagged);
        Assert.Contains(1, vif.Flagged);
        Assert.True(double.IsPositiveInfinity(vif.Vif[0]) || vif.Vif[0] >= 5.0);
        Assert.True(double.IsPositiveInfinity(vif.Vif[1]) || vif.Vif[1] >= 5.0);
    }
}
