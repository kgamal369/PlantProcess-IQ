using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Advanced;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics.Advanced;

public sealed class AdvancedCorrelationComputeServiceTests
{
    private sealed class FakeLoader : IFeatureVectorLoader
    {
        private readonly AdvancedDataset _ds;
        public FakeLoader(AdvancedDataset ds) => _ds = ds;
        public Task<AdvancedDataset> LoadAsync(AdvancedAnalysisRequest request, CancellationToken ct) => Task.FromResult(_ds);
    }

    private static AdvancedCorrelationComputeService Service(AdvancedDataset ds) =>
        new(new FakeLoader(ds), new NullAdvancedResultWriter(), NullLogger<AdvancedCorrelationComputeService>.Instance);

    private static AdvancedAnalysisRequest Req() =>
        new("defect.edge_crack_rate", "coil", 3650, Guid.NewGuid(), PermutationIterations: 80, BootstrapIterations: 300);

    private static FeatureSeries NumericSeries(string key, Func<int, double> f, int n) =>
        new(key, VariableType.Numeric, Enumerable.Range(0, n).Select(i => new FeatureSample($"k{i}", f(i), null)).ToList());

    // ---- G1: golden recovery (two INDEPENDENT signals + noise + a collinear duplicate) ----
    private static AdvancedDataset GoldenDataset()
    {
        const int n = 90;
        double CompA(int i) => i;
        double CompB(int i) => (37 * i) % 53;
        var outcomes = Enumerable.Range(0, n)
            .Select(i => new OutcomeSample($"k{i}", CompA(i) + 2.0 * CompB(i), null, $"h{i}")).ToList();

        var features = new List<FeatureSeries>
        {
            NumericSeries("param_signal_a",     i => CompA(i) + ((i * 11) % 3) * 0.01, n),
            NumericSeries("param_signal_b",     i => CompB(i) + ((i * 5) % 3) * 0.01, n),
            NumericSeries("param_noise_1",      i => (i * 23) % 19, n),
            NumericSeries("param_noise_2",      i => (i * 29) % 13, n),
            NumericSeries("param_featurec",     i => (i * 17) % 41, n),
            NumericSeries("param_zzz_collinear",i => ((i * 17) % 41) * 2.0 + 1.0, n) // perfectly collinear with featurec
        };

        return new AdvancedDataset("defect.edge_crack_rate", VariableType.Numeric, outcomes, features,
            new Dictionary<string, string>(), IndependentHeats: n, FreshnessFactor: 0.0, RequiredFieldCompleteness: 1.0);
    }

    [Fact]
    public async Task G1_recovers_true_signals_rejects_noise_and_excludes_collinear()
    {
        var result = await Service(GoldenDataset()).ComputeAsync(Req(), CancellationToken.None);

        Assert.True(result.CanRun);

        var a = result.Findings.Single(f => f.FeatureKey == "param_signal_a");
        var b = result.Findings.Single(f => f.FeatureKey == "param_signal_b");
        Assert.True(a.Significant, "signal_a should be significant under FDR");
        Assert.True(b.Significant, "signal_b should be significant under FDR");
        Assert.True(a.IsStable && b.IsStable, "true signals should be bootstrap-stable");
        Assert.Equal(AnalysisMethod.Spearman, a.Method);

        var noise = result.Findings.Single(f => f.FeatureKey == "param_noise_1");
        Assert.False(noise.Significant, "noise should be rejected under FDR");

        var dup = result.Excluded.Single(e => e.FeatureKey == "param_zzz_collinear");
        Assert.Contains("VIF", dup.Reason, StringComparison.OrdinalIgnoreCase);

        // ranked by effect size (b is the stronger signal here)
        var ranks = result.Findings.Select(f => f.FeatureKey).ToList();
        Assert.True(ranks.IndexOf("param_signal_b") < ranks.IndexOf("param_noise_1"));
    }

    [Fact]
    public async Task G3_is_reproducible_for_a_fixed_seed()
    {
        var r1 = await Service(GoldenDataset()).ComputeAsync(Req(), CancellationToken.None);
        var r2 = await Service(GoldenDataset()).ComputeAsync(Req(), CancellationToken.None);

        Assert.Equal(r1.Findings.Select(f => f.FeatureKey), r2.Findings.Select(f => f.FeatureKey));
        Assert.Equal(r1.Findings.Select(f => Math.Round(f.QValue, 9)), r2.Findings.Select(f => Math.Round(f.QValue, 9)));
    }

    // ---- G2: a confounder with a strong POOLED effect that flips sign within strata ----
    [Fact]
    public async Task G2_confounder_is_significant_but_fails_stratification()
    {
        const int n = 90;
        var outcomes = Enumerable.Range(0, n).Select(i => new OutcomeSample($"k{i}", i, null, $"h{i}")).ToList();
        int Block(int i) => i / 30; int Local(int i) => i % 30;
        var features = new List<FeatureSeries>
        {
            NumericSeries("param_confounder", i => Block(i) * 1000.0 - Local(i), n), // pooled +, within-stratum -
            NumericSeries("param_noise_a",    i => (i * 23) % 19, n),
            NumericSeries("param_noise_b",    i => (i * 29) % 13, n)
        };
        var strata = new Dictionary<string, string>();
        for (int i = 0; i < n; i++) strata[$"k{i}"] = "s" + Block(i);

        var ds = new AdvancedDataset("defect.edge_crack_rate", VariableType.Numeric, outcomes, features,
            strata, IndependentHeats: n, FreshnessFactor: 0.0, RequiredFieldCompleteness: 1.0);

        var result = await Service(ds).ComputeAsync(Req(), CancellationToken.None);
        var conf = result.Findings.Single(f => f.FeatureKey == "param_confounder");

        Assert.True(conf.Significant, "the confounder has a strong pooled association");
        Assert.False(conf.SurvivesStratification, "it must NOT survive stratification (sign flips within strata)");
    }

    // ---- G4: the readiness gate blocks an underpowered dataset ----
    [Fact]
    public async Task G4_blocks_when_readiness_is_insufficient()
    {
        const int n = 10;
        var outcomes = Enumerable.Range(0, n).Select(i => new OutcomeSample($"k{i}", i, null, $"h{i}")).ToList();
        var ds = new AdvancedDataset("defect.edge_crack_rate", VariableType.Numeric, outcomes,
            new List<FeatureSeries> { NumericSeries("param_x", i => i, n) },
            new Dictionary<string, string>(), IndependentHeats: n, FreshnessFactor: 0.0, RequiredFieldCompleteness: 1.0);

        var result = await Service(ds).ComputeAsync(Req(), CancellationToken.None);
        Assert.False(result.CanRun);
        Assert.Empty(result.Findings);
    }

    // ---- G5: method auto-selection dispatch (mirrors the Analytics.Core MethodSelector tests) ----
    [Theory]
    [InlineData(VariableType.Numeric, VariableType.Numeric, AnalysisMethod.Spearman)]
    [InlineData(VariableType.Categorical, VariableType.Categorical, AnalysisMethod.CramersV)]
    [InlineData(VariableType.Binary, VariableType.Numeric, AnalysisMethod.PointBiserial)]
    public void G5_method_selector_dispatches_by_pair_shape(VariableType a, VariableType b, AnalysisMethod expected)
        => Assert.Equal(expected, MethodSelector.Select(a, b).Method);

    [Fact]
    public void G5_nonlinear_numeric_pair_selects_mutual_information()
        => Assert.Equal(AnalysisMethod.MutualInformation,
            MethodSelector.Select(VariableType.Numeric, VariableType.Numeric, numericRelationshipNonlinear: true).Method);
}
