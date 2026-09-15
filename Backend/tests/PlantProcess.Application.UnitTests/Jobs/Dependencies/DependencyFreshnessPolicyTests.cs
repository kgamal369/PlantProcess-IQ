// T-106 Phase A tests: freshness is decided by declared data, never by a comment.
using PlantProcess.Application.Jobs.Dependencies;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Dependencies;

[Trait("BacklogTask", "T-106")]
public sealed class DependencyFreshnessPolicyTests
{
    [Fact]
    public void Current_cycle_success_satisfies()
    {
        var outcome = DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, true, null, 60, false));
        Assert.Equal(FreshnessResolution.Satisfied, outcome.Resolution);
    }

    [Fact]
    public void Required_edge_blocks_when_no_prior_success_exists()
    {
        var outcome = DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, null, 60, true));
        Assert.Equal(FreshnessResolution.Blocked, outcome.Resolution);
    }

    [Fact]
    public void Stale_reuse_is_refused_unless_the_edge_opts_in()
    {
        var outcome = DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 5, 60, false));
        Assert.Equal(FreshnessResolution.Blocked, outcome.Resolution);
        Assert.Equal(5, outcome.UpstreamAgeMinutes);
        Assert.Equal(60, outcome.ToleranceMinutes);
    }

    [Fact]
    public void Opted_in_edge_reuses_a_prior_success_inside_tolerance()
    {
        var outcome = DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 45, 60, true));
        Assert.Equal(FreshnessResolution.StaleAccepted, outcome.Resolution);
    }

    [Fact]
    public void The_tolerance_boundary_is_inclusive()
    {
        Assert.Equal(FreshnessResolution.StaleAccepted,
            DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 60, 60, true)).Resolution);
        Assert.Equal(FreshnessResolution.Blocked,
            DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 60.5, 60, true)).Resolution);
    }

    [Fact]
    public void Opt_in_without_a_declared_tolerance_accepts_no_age()
    {
        var outcome = DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 1, null, true));
        Assert.Equal(FreshnessResolution.Blocked, outcome.Resolution);
    }

    [Fact]
    public void A_future_completion_instant_is_not_usable()
    {
        var outcome = DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, -10, 60, true));
        Assert.Equal(FreshnessResolution.Blocked, outcome.Resolution);
    }

    [Fact]
    public void An_optional_edge_is_skipped_where_a_required_edge_blocks()
    {
        var outcome = DependencyFreshnessPolicy.Evaluate(new FreshnessInput(false, false, 900, 60, true));
        Assert.Equal(FreshnessResolution.SkippedOptional, outcome.Resolution);
    }
}
