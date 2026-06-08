using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Advanced;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics.Advanced;

/// <summary>
/// PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE.
/// Golden dataset with known true drivers and injected spurious features.
/// The engine must recover true signals, reject spurious features under BH-FDR,
/// report bootstrap stability, and rerun deterministically.
/// </summary>
public sealed class Phase8_T043GoldenSignalRecoveryTests
{
    private static readonly string[] TrueDrivers =
    {
        "param_true_temperature_driver",
        "param_true_pressure_driver"
    };

    private static readonly string[] TrueDriverRepresentatives =
    {
        "param_true_temperature_driver",
        "param_collinear_temperature_duplicate",
        "param_true_pressure_driver"
    };

    private static readonly string[] InjectedSpurious =
    {
        "param_injected_spurious_alternating",
        "param_injected_spurious_periodic",
        "param_injected_spurious_hash"
    };

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
            TenantId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
            FdrQ: 0.05,
            VifThreshold: 5.0,
            BootstrapIterations: 350,
            PermutationIterations: 120,
            CorrelationId: "PPIQ-T043-GOLDEN-SIGNAL-RECOVERY");

    private static FeatureSeries NumericSeries(string key, Func<int, double> value, int n)
        => new(
            key,
            VariableType.Numeric,
            Enumerable.Range(0, n)
                .Select(i => new FeatureSample($"sample-{i:000}", value(i), null))
                .ToList());

    private static AdvancedDataset GoldenDataset()
    {
        const int n = 180;

        static double Temperature(int i)
            => i + ((i % 7) * 0.01);

        static double Pressure(int i)
            => (37 * i) % 83 + ((i % 5) * 0.01);

        static double ControlledNoise(int i)
            => (((17 * i) % 11) - 5) * 0.03;

        var outcomes = Enumerable.Range(0, n)
            .Select(i => new OutcomeSample(
                $"sample-{i:000}",
                1.4 * Temperature(i) + 3.2 * Pressure(i) + ControlledNoise(i),
                null,
                $"heat-{i:000}"))
            .ToList();

        var features = new List<FeatureSeries>
        {
            // Known true drivers.
            NumericSeries("param_true_temperature_driver", i => Temperature(i), n),
            NumericSeries("param_true_pressure_driver", i => Pressure(i), n),

            // Injected spurious features: deterministic, present in the dataset, but not causal drivers.
            NumericSeries("param_injected_spurious_alternating", i => i % 2 == 0 ? -1.0 : 1.0, n),
            NumericSeries("param_injected_spurious_periodic", i => Math.Sin(i * 13.17) + Math.Cos(i * 5.91), n),
            NumericSeries("param_injected_spurious_hash", i => ((97 * i + 31) % 101) / 101.0, n),

            // Collinear duplicate to ensure the T-042 VIF guard still cooperates with T-043.
            // The engine may keep either the original or the duplicate as the representative.
            NumericSeries("param_collinear_temperature_duplicate", i => Temperature(i) * 2.0 + 10.0, n)
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
    public async Task T043_Recovers_AtLeastTwo_KnownTrueSignals_UnderFdr()
    {
        var request = Request();
        var result = await Service(GoldenDataset()).ComputeAsync(request, CancellationToken.None);

        Assert.True(result.CanRun);
        Assert.NotEmpty(result.Findings);

        var recovered = result.Findings
            .Where(f => TrueDriverRepresentatives.Contains(f.FeatureKey))
            .Where(f => f.Significant)
            .Where(f => f.QValue <= request.FdrQ)
            .ToList();

        Assert.True(recovered.Count >= 2, $"Expected at least two true driver representatives to recover under FDR q={request.FdrQ}.");
        Assert.Contains(recovered, f => f.FeatureKey == "param_true_pressure_driver");
        Assert.Contains(recovered, f =>
            f.FeatureKey == "param_true_temperature_driver" ||
            f.FeatureKey == "param_collinear_temperature_duplicate");

        foreach (var signal in recovered)
        {
            Assert.True(signal.IsStable, $"{signal.FeatureKey} should be bootstrap-stable.");
            Assert.True(signal.StabilityConsistency >= 0.70, $"{signal.FeatureKey} should have strong bootstrap consistency.");
            Assert.True(Math.Abs(signal.EffectSize) >= 0.50, $"{signal.FeatureKey} should carry a practical effect size.");
            Assert.Contains("not a guaranteed root cause", signal.HonestyCaveat, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task T043_Rejects_All_InjectedSpuriousFeatures_UnderBenjaminiHochbergFdr()
    {
        var request = Request();
        var result = await Service(GoldenDataset()).ComputeAsync(request, CancellationToken.None);

        Assert.True(result.CanRun);

        foreach (var featureKey in InjectedSpurious)
        {
            var finding = Assert.Single(result.Findings, f => f.FeatureKey == featureKey);

            Assert.False(finding.Significant, $"{featureKey} must be rejected under FDR.");
            Assert.True(finding.QValue > request.FdrQ, $"{featureKey} q-value must stay above FDR threshold.");
        }
    }

    [Fact]
    public async Task T043_Reports_BootstrapStability_For_Every_EmittedFinding()
    {
        var result = await Service(GoldenDataset()).ComputeAsync(Request(), CancellationToken.None);

        Assert.True(result.CanRun);

        Assert.All(result.Findings, finding =>
        {
            Assert.InRange(finding.StabilityConsistency, 0.0, 1.0);
            Assert.False(double.IsNaN(finding.StabilityLower));
            Assert.False(double.IsNaN(finding.StabilityUpper));
            Assert.True(finding.StabilityLower <= finding.StabilityUpper);
        });

        Assert.Contains(result.Findings, f => f.FeatureKey == "param_true_pressure_driver" && f.IsStable);
        Assert.Contains(result.Findings, f =>
            (f.FeatureKey == "param_true_temperature_driver" ||
             f.FeatureKey == "param_collinear_temperature_duplicate") &&
            f.IsStable);
    }

    [Fact]
    public async Task T043_Reruns_Are_Deterministic_Except_RunId()
    {
        var service = Service(GoldenDataset());
        var request = Request();

        var first = await service.ComputeAsync(request, CancellationToken.None);
        var second = await service.ComputeAsync(request, CancellationToken.None);

        Assert.NotEqual(first.RunId, second.RunId);
        Assert.Equal(first.CanRun, second.CanRun);
        Assert.Equal(first.Findings.Count, second.Findings.Count);
        Assert.Equal(first.Excluded.Count, second.Excluded.Count);

        var firstByKey = first.Findings.ToDictionary(x => x.FeatureKey);
        var secondByKey = second.Findings.ToDictionary(x => x.FeatureKey);

        foreach (var key in firstByKey.Keys)
        {
            Assert.True(secondByKey.ContainsKey(key), $"Second run missed finding {key}.");

            var a = firstByKey[key];
            var b = secondByKey[key];

            Assert.Equal(a.Significant, b.Significant);
            Assert.Equal(a.IsStable, b.IsStable);
            Assert.Equal(Math.Round(a.EffectSize, 8), Math.Round(b.EffectSize, 8));
            Assert.Equal(Math.Round(a.PValue, 8), Math.Round(b.PValue, 8));
            Assert.Equal(Math.Round(a.QValue, 8), Math.Round(b.QValue, 8));
            Assert.Equal(Math.Round(a.StabilityConsistency, 8), Math.Round(b.StabilityConsistency, 8));
        }
    }

    [Fact]
    public async Task T043_Vif_Excludes_Collinear_Duplicate_Without_Losing_TrueRepresentative()
    {
        var result = await Service(GoldenDataset()).ComputeAsync(Request(), CancellationToken.None);

        Assert.True(result.CanRun);

        var temperatureRepresentatives = result.Findings
            .Where(f =>
                f.FeatureKey == "param_true_temperature_driver" ||
                f.FeatureKey == "param_collinear_temperature_duplicate")
            .Select(f => f.FeatureKey)
            .ToList();

        Assert.Single(temperatureRepresentatives);

        var excluded = Assert.Single(result.Excluded, e =>
            e.FeatureKey == "param_true_temperature_driver" ||
            e.FeatureKey == "param_collinear_temperature_duplicate");

        Assert.Contains("VIF", excluded.Reason);
    }
}