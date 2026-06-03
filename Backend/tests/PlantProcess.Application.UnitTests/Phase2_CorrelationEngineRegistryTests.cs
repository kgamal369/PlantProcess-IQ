// PPIQ-GENERATED (T010) - canonical correlation engine is the registry default
using System;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Analytics.Advanced;
using PlantProcess.Application.Analytics.Engines;
using Xunit;

namespace PlantProcess.Phase2.Tests;

public class Phase2_CorrelationEngineRegistryTests
{
    private sealed class StubAdvanced : IAdvancedCorrelationService
    {
        public Task<AdvancedAnalysisRunResult> ComputeAsync(AdvancedAnalysisRequest request, CancellationToken ct)
            => Task.FromResult(new AdvancedAnalysisRunResult(
                Guid.NewGuid(), request.OutcomeKey, default, request.Grain, request.WindowDays,
                default, Array.Empty<string>(), false,
                Array.Empty<AdvancedFinding>(), Array.Empty<ExcludedFeature>(), "canonical", "stub"));
    }

    [Fact]
    public void Default_engine_is_canonical_and_unknown_keys_fall_back()
    {
        var registry = new CorrelationEngineRegistry(
            new ICorrelationEngine[] { new CanonicalCorrelationEngine(new StubAdvanced()) });

        Assert.Equal("canonical", registry.Default.Key);
        Assert.Same(registry.Default, registry.Resolve(null));
        Assert.Same(registry.Default, registry.Resolve("nonexistent"));
    }
}