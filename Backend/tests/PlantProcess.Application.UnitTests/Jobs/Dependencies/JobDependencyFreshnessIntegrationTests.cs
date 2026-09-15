// T-106 B2.2 tests: freshness decided once, by the accepted authority, with evidence.
using System;
using PlantProcess.Application.Jobs.Dependencies;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Dependencies;

[Trait("BacklogTask", "T-106")]
public sealed class JobDependencyFreshnessIntegrationTests
{
    private static readonly Guid Upstream = new Guid("aaaaaaaa-1111-2222-3333-444444444444");
    private static readonly Guid Run = new Guid("bbbbbbbb-1111-2222-3333-444444444444");
    private static readonly Guid DownstreamRun = new Guid("cccccccc-1111-2222-3333-444444444444");
    private static readonly Guid Job = new Guid("dddddddd-1111-2222-3333-444444444444");
    private static readonly DateTime Now = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    private static JobDependencyOutcome Evaluate(
        bool currentCycle, double? ageMinutes, int? tolerance, bool allowStale, bool required = true)
        => JobDependencyEvaluator.Evaluate(
            Upstream, required, null, Run, JobRunStatus.Ok, 7,
            upstreamRanInCurrentCycle: currentCycle,
            upstreamCompletedAtUtc: ageMinutes.HasValue ? Now.AddMinutes(-ageMinutes.Value) : (DateTime?)null,
            evaluatedAtUtc: Now,
            stalenessToleranceMinutes: tolerance,
            allowStaleReuse: allowStale);

    [Fact]
    public void A_current_cycle_success_is_satisfied()
    {
        var outcome = Evaluate(true, 0, 60, false);
        Assert.Equal(JobDependencyResolution.Satisfied, outcome.Resolution);
        Assert.False(outcome.BlocksDownstream);
        Assert.Equal(Run, outcome.DependsOnRunId);
    }

    [Fact]
    public void The_old_six_argument_call_keeps_its_accepted_behaviour()
    {
        var outcome = JobDependencyEvaluator.Evaluate(Upstream, true, null, Run, JobRunStatus.Ok, 7);
        Assert.Equal(JobDependencyResolution.Satisfied, outcome.Resolution);
        Assert.False(outcome.BlocksDownstream);
    }

    [Fact]
    public void A_prior_result_inside_tolerance_is_satisfied_without_permission()
    {
        var outcome = Evaluate(false, 10, 60, false);
        Assert.Equal(JobDependencyResolution.Satisfied, outcome.Resolution);
        Assert.False(outcome.BlocksDownstream);
    }

    [Fact]
    public void A_result_beyond_tolerance_without_permission_blocks()
    {
        var outcome = Evaluate(false, 900, 60, false);
        Assert.Equal(JobDependencyResolution.Blocked, outcome.Resolution);
        Assert.True(outcome.BlocksDownstream);
    }

    [Fact]
    public void A_result_beyond_tolerance_with_permission_is_stale_accepted()
    {
        var outcome = Evaluate(false, 900, 60, true);
        Assert.Equal(JobDependencyResolution.StaleAccepted, outcome.Resolution);
        Assert.False(outcome.BlocksDownstream);
    }

    [Fact]
    public void The_tolerance_boundary_is_deterministic()
    {
        Assert.Equal(JobDependencyResolution.Satisfied, Evaluate(false, 60, 60, false).Resolution);
        Assert.Equal(JobDependencyResolution.StaleAccepted, Evaluate(false, 60.5, 60, true).Resolution);
        Assert.Equal(JobDependencyResolution.Blocked, Evaluate(false, 60.5, 60, false).Resolution);
    }

    [Fact]
    public void No_declared_tolerance_means_no_freshness_ceiling()
    {
        Assert.Equal(JobDependencyResolution.Satisfied, Evaluate(false, 100000, null, false).Resolution);
    }

    [Fact]
    public void An_optional_stale_edge_is_skipped_not_blocked()
    {
        var outcome = Evaluate(false, 900, 60, false, required: false);
        Assert.Equal(JobDependencyResolution.SkippedOptional, outcome.Resolution);
        Assert.False(outcome.BlocksDownstream);
    }

    [Fact]
    public void A_failed_upstream_keeps_its_existing_semantics()
    {
        var required = JobDependencyEvaluator.Evaluate(Upstream, true, null, Run, JobRunStatus.Failed, 7);
        Assert.Equal(JobDependencyResolution.FailedUpstream, required.Resolution);

        var optional = JobDependencyEvaluator.Evaluate(Upstream, false, null, Run, JobRunStatus.Failed, 7);
        Assert.Equal(JobDependencyResolution.SkippedOptional, optional.Resolution);
    }

    [Fact]
    public void A_pinned_version_mismatch_still_blocks_before_freshness_is_considered()
    {
        var outcome = JobDependencyEvaluator.Evaluate(
            Upstream, true, 3, Run, JobRunStatus.Ok, 4,
            upstreamRanInCurrentCycle: true, upstreamCompletedAtUtc: Now, evaluatedAtUtc: Now,
            stalenessToleranceMinutes: 60, allowStaleReuse: true);
        Assert.Equal(JobDependencyResolution.Blocked, outcome.Resolution);
    }

    [Fact]
    public void A_never_run_upstream_keeps_a_null_run_identity()
    {
        var outcome = JobDependencyEvaluator.Evaluate(Upstream, true, null, null, null, null);
        Assert.Equal(JobDependencyResolution.Blocked, outcome.Resolution);
        Assert.Null(outcome.DependsOnRunId);
    }

    [Fact]
    public void Stale_accepted_evidence_must_carry_age_and_tolerance()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JobRunDependency(
            DownstreamRun, Run, Job, Upstream, JobDependencyResolution.StaleAccepted, null, null, "reused"));

        // An age inside the tolerance is Satisfied, never a stale claim.
        Assert.Throws<ArgumentOutOfRangeException>(() => new JobRunDependency(
            DownstreamRun, Run, Job, Upstream, JobDependencyResolution.StaleAccepted, null, null, "reused", 30, 60));
    }

    [Fact]
    public void Stale_accepted_persists_the_measured_age_and_tolerance()
    {
        var edge = new JobRunDependency(
            DownstreamRun, Run, Job, Upstream, JobDependencyResolution.StaleAccepted, null, null, "reused", 900, 60);
        Assert.Equal(JobDependencyResolution.StaleAccepted, edge.Resolution);
        Assert.Equal(900, edge.UpstreamAgeMinutes);
        Assert.Equal(60, edge.ToleranceMinutes);
    }

    [Fact]
    public void An_edge_permits_no_reuse_until_it_says_so()
    {
        var plain = new JobDependency(Job, Upstream);
        Assert.False(plain.AllowStaleReuse);

        var permitted = new JobDependency(Job, Upstream, JobDependencyKind.Data, true, null, 60, true);
        Assert.True(permitted.AllowStaleReuse);
        Assert.Equal(60, permitted.StalenessToleranceMinutes);
    }

    [Fact]
    public void The_accepted_baseline_refusals_still_hold_exactly_as_written()
    {
        // Replica of JobExecutionContractTests.The_evidence_row_refuses_to_claim_stale_accepted_or_a_missing_run.
        // B2.2 narrows the rule from "never stale_accepted" to "never unevidenced stale_accepted";
        // the accepted assertion, which supplies no evidence, must keep passing untouched.
        Assert.Throws<ArgumentOutOfRangeException>(() => new JobRunDependency(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Upstream,
            JobDependencyResolution.StaleAccepted, null, null, "no"));

        Assert.Throws<ArgumentException>(() => new JobRunDependency(
            Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), Upstream,
            JobDependencyResolution.Satisfied, null, null, "no"));
    }

    [Fact]
    public void No_six_argument_evaluation_path_produces_stale_accepted()
    {
        // Replica of JobExecutionContractTests.No_evaluation_path_produces_stale_accepted.
        var produced = new System.Collections.Generic.List<JobDependencyResolution>();
        foreach (var required in new[] { true, false })
        {
            foreach (var pinned in new int?[] { null, 5 })
            {
                foreach (var status in new JobRunStatus?[]
                    { null, JobRunStatus.Ok, JobRunStatus.Failed, JobRunStatus.Timeout, JobRunStatus.Blocked })
                {
                    var runId = status is null ? (Guid?)null : Guid.NewGuid();
                    produced.Add(JobDependencyEvaluator.Evaluate(Upstream, required, pinned, runId, status, 5).Resolution);
                }
            }
        }

        Assert.DoesNotContain(JobDependencyResolution.StaleAccepted, produced);
    }
}