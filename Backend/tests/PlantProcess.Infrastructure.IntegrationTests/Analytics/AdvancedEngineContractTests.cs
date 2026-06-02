using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Analytics.Core.Contracts;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Advanced;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Infrastructure.Analytics;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Analytics;

public sealed class AdvancedEngineContractTests
{
    private sealed class FakeService : IAdvancedCorrelationService
    {
        private readonly AdvancedAnalysisRunResult _r;
        public FakeService(AdvancedAnalysisRunResult r) => _r = r;
        public Task<AdvancedAnalysisRunResult> ComputeAsync(AdvancedAnalysisRequest request, CancellationToken ct) => Task.FromResult(_r);
    }

    [Fact]
    public async Task Engine_maps_method_aware_run_to_correlation_result()
    {
        var runId = Guid.NewGuid();
        var findings = new List<AdvancedFinding>
        {
            new("param_a", AnalysisMethod.Spearman, "rationale", 0.62, 0.01, 0.02, true, 90, 0.99, 0.4, 0.8, true, true, "ok", 0,
                $"finding:{runId:N}:param_a", AdvancedAnalysisResult.DefaultCaveat),
            new("param_b", AnalysisMethod.CramersV, "rationale", 0.41, 0.03, 0.04, true, 90, 0.96, 0.2, 0.6, true, true, "ok", 0,
                $"finding:{runId:N}:param_b", AdvancedAnalysisResult.DefaultCaveat)
        };
        var run = new AdvancedAnalysisRunResult(runId, "defect.edge_crack_rate", VariableType.Numeric, "coil", 30,
            PlantProcess.Analytics.Core.Readiness.ReadinessState.Ready, new List<string>(), true,
            findings, new List<ExcludedFeature>(), "dotnet-analytics-core-v1", "ok");

        var engine = new DotNetAdvancedCorrelationEngine(new FakeService(run));
        var result = await engine.ComputeAsync(new CorrelationComputeRequest("defect.edge_crack_rate", "coil", 30), CancellationToken.None);

        Assert.Equal("dotnet-analytics-core-v1", result.EngineKey);
        Assert.Equal(runId, result.ComputeRunId);
        Assert.Equal(2, result.ResultCount);
        Assert.Equal("Ok", result.Status);

        // contract: the engine surfaces real method-aware findings (variety + q + stability)
        Assert.Contains(findings, f => f.Method == AnalysisMethod.Spearman);
        Assert.Contains(findings, f => f.Method == AnalysisMethod.CramersV);
        Assert.All(findings, f => Assert.InRange(f.QValue, 0.0, 1.0));
        Assert.All(findings, f => Assert.InRange(f.StabilityConsistency, 0.0, 1.0));
    }
}
