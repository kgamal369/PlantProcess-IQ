using System;
using System.Linq;
using System.Threading;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Readiness;
using PlantProcess.Analytics.Engine;
using PlantProcess.Application.Analytics.Contracts;
using Xunit;

namespace PlantProcess.Analytics.Engine.Tests;

public sealed class ManagedStatisticalComputeEngineTests
{
    private static readonly ReadinessInput ReadyInputs = new(IndependentHeats: 80, OutcomeEvents: 60, MinorityClassFraction: 0.3, FreshnessFactor: 1.0, RequiredFieldCompleteness: 0.98);
    private static CorrelationComputeRequest Req() => new("defect_rate", "coil", 730);

    [Fact]
    public async System.Threading.Tasks.Task Recovers_true_drivers_rejects_noise_and_persists_disciplined_findings()
    {
        var matrix = FakeFeatureSource.PlantedSignalsAndDecoys(ReadyInputs);
        var sink = new InMemoryFindingSink();
        var engine = new ManagedStatisticalComputeEngine(new FakeFeatureSource(matrix), sink);

        var result = await engine.ComputeAsync(Req(), CancellationToken.None);

        Assert.Equal("Ok", result.Status);
        Assert.Equal("managed-stat-v1", result.EngineKey);
        Assert.Equal(6, result.ResultCount);                 // speed + binary + 4 noise (categorical skipped)
        Assert.Equal(6, sink.Findings.Count);
        Assert.NotEqual(Guid.Empty, result.ComputeRunId);

        var speed = sink.Findings.Single(f => f.ParameterCode == "casting_speed");
        Assert.Equal(AnalysisMethod.Spearman, speed.Method);
        Assert.True(speed.Significant);
        Assert.True(Math.Abs(speed.EffectSize) > 0.5);
        Assert.True(speed.Stable);
        Assert.InRange(speed.QValue, 0.0, 1.0);

        var grade = sink.Findings.Single(f => f.ParameterCode == "is_grade_dx51d");
        Assert.Equal(AnalysisMethod.PointBiserial, grade.Method);
        Assert.True(grade.Significant);
        Assert.True(Math.Abs(grade.EffectSize) > 0.3);

        foreach (var noise in sink.Findings.Where(f => f.ParameterCode.StartsWith("noise_")))
            Assert.True(Math.Abs(noise.EffectSize) < 0.35, $"{noise.ParameterCode} should be a weak/null association.");

        Assert.All(sink.Findings, f => Assert.InRange(f.QValue, 0.0, 1.0)); // FDR q reported for all
        Assert.DoesNotContain(sink.Findings, f => f.ParameterCode == "route_code"); // numeric-vs-categorical skipped
    }

    [Fact]
    public async System.Threading.Tasks.Task Blocked_readiness_prevents_the_run()
    {
        var blockedInputs = ReadyInputs with { IndependentHeats = 10 };
        var matrix = FakeFeatureSource.PlantedSignalsAndDecoys(blockedInputs);
        var sink = new InMemoryFindingSink();
        var engine = new ManagedStatisticalComputeEngine(new FakeFeatureSource(matrix), sink);

        var result = await engine.ComputeAsync(Req(), CancellationToken.None);

        Assert.Equal("Blocked", result.Status);
        Assert.Equal(0, result.ResultCount);
        Assert.Empty(sink.Findings);
    }
}