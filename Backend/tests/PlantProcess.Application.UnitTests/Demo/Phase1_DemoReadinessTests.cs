using System.Linq;
using PlantProcess.Application.Demo.Readiness;
using Xunit;

namespace PlantProcess.Application.UnitTests.Demo;

/// <summary>PPIQ-103: demo readiness returns green when complete and blocked-with-blocker otherwise.</summary>
public sealed class Phase1_DemoReadinessTests
{
    private static DemoReadinessInputs FullyLoaded() => new(
        SourcesLinked: 8, SourcesExpected: 8,
        StagingPopulated: true, MappingsPublished: true,
        JobsRunnable: 4, JobsExpected: 4, DemoPagesPresent: true);

    [Fact]
    public void PPIQ_103_Fully_loaded_demo_is_green_with_no_blockers()
    {
        var r = DemoReadinessEvaluator.Evaluate(FullyLoaded());
        Assert.True(r.IsReady);
        Assert.Equal("green", r.Status);
        Assert.Empty(r.Blockers);
    }

    [Fact]
    public void PPIQ_103_Removing_a_mapping_flips_to_blocked_and_names_it()
    {
        var r = DemoReadinessEvaluator.Evaluate(FullyLoaded() with { MappingsPublished = false });
        Assert.False(r.IsReady);
        Assert.Equal("blocked", r.Status);
        Assert.Contains(r.Blockers, b => b.Contains("mappings", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PPIQ_103_Missing_source_names_the_precise_gap()
    {
        var r = DemoReadinessEvaluator.Evaluate(FullyLoaded() with { SourcesLinked = 7 });
        Assert.False(r.IsReady);
        Assert.Contains(r.Blockers, b => b.Contains("1 of 8 demo sources"));
    }

    [Fact]
    public void PPIQ_103_Multiple_gaps_are_all_named()
    {
        var r = DemoReadinessEvaluator.Evaluate(FullyLoaded() with
        {
            StagingPopulated = false,
            JobsRunnable = 2
        });
        Assert.False(r.IsReady);
        Assert.Equal(2, r.Blockers.Count);
    }
}