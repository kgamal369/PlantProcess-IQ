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
    public void A_prior_success_inside_tolerance_is_satisfied_without_any_permission()
    {
        // CENTRAL ruling: allow_stale_reuse permits going BEYOND the declared ceiling.
        // It is not permission to reuse a prior cycle at all.
        var outcome = DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 5, 60, false));
        Assert.Equal(FreshnessResolution.Satisfied, outcome.Resolution);
        Assert.Equal(5, outcome.UpstreamAgeMinutes);
        Assert.Equal(60, outcome.ToleranceMinutes);
    }

    [Fact]
    public void Permission_does_not_change_a_result_that_is_already_fresh_enough()
    {
        var outcome = DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 45, 60, true));
        Assert.Equal(FreshnessResolution.Satisfied, outcome.Resolution);
    }

    [Fact]
    public void Beyond_the_ceiling_permission_is_what_produces_stale_accepted()
    {
        Assert.Equal(FreshnessResolution.StaleAccepted,
            DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 900, 60, true)).Resolution);
        Assert.Equal(FreshnessResolution.Blocked,
            DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 900, 60, false)).Resolution);
    }

    [Fact]
    public void The_tolerance_boundary_is_inclusive_and_satisfied()
    {
        Assert.Equal(FreshnessResolution.Satisfied,
            DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 60, 60, false)).Resolution);
        Assert.Equal(FreshnessResolution.StaleAccepted,
            DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 60.5, 60, true)).Resolution);
        Assert.Equal(FreshnessResolution.Blocked,
            DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 60.5, 60, false)).Resolution);
    }

    [Fact]
    public void No_declared_tolerance_is_no_declared_ceiling()
    {
        Assert.Equal(FreshnessResolution.Satisfied,
            DependencyFreshnessPolicy.Evaluate(new FreshnessInput(true, false, 100000, null, false)).Resolution);
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
        var outcome = DependencyFreshnessPolicy.Evaluate(new FreshnessInput(false, false, 900, 60, false));
        Assert.Equal(FreshnessResolution.SkippedOptional, outcome.Resolution);
    }
}
