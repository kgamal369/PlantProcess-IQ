using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Application.Analytics.Advanced;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics.Advanced;

public sealed class AnalysisReadinessServiceTests
{
    private sealed class FakeLoader : IFeatureVectorLoader
    {
        private readonly AdvancedDataset _ds;
        public FakeLoader(AdvancedDataset ds) => _ds = ds;
        public Task<AdvancedDataset> LoadAsync(AdvancedAnalysisRequest request, CancellationToken ct) => Task.FromResult(_ds);
    }

    private static AdvancedDataset Ds(int heats, int events, double completeness, double freshness)
    {
        var outcomes = Enumerable.Range(0, events)
            .Select(i => new OutcomeSample($"k{i}", i, null, $"h{i % Math.Max(1, heats)}")).ToList();
        return new AdvancedDataset("defect.edge_crack_rate", VariableType.Numeric, outcomes,
            new List<FeatureSeries>(), new Dictionary<string, string>(), heats, freshness, completeness);
    }

    private static async Task<AnalysisReadinessDto> Run(AdvancedDataset ds) =>
        await new AnalysisReadinessService(new FakeLoader(ds))
            .EvaluateAsync(new AdvancedAnalysisRequest("defect.edge_crack_rate", "coil", 30, Guid.NewGuid()), CancellationToken.None);

    [Fact]
    public async Task Ready_when_all_dimensions_meet_thresholds()
    {
        var dto = await Run(Ds(heats: 60, events: 40, completeness: 0.95, freshness: 0.0));
        Assert.Equal("Ready", dto.Overall);
        Assert.True(dto.CanRun);
        Assert.All(dto.Dimensions, d => Assert.False(string.IsNullOrWhiteSpace(d.Reason)));
    }

    [Fact]
    public async Task Partial_when_heats_in_partial_band()
    {
        var dto = await Run(Ds(heats: 45, events: 40, completeness: 0.95, freshness: 0.0));
        Assert.Equal("Partial", dto.Overall);
        Assert.True(dto.CanRun);
    }

    [Fact]
    public async Task Blocked_when_heats_below_partial_threshold()
    {
        var dto = await Run(Ds(heats: 10, events: 40, completeness: 0.95, freshness: 0.0));
        Assert.Equal("Blocked", dto.Overall);
        Assert.False(dto.CanRun);
    }
}
