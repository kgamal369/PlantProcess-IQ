// T-106 B2.2b tests: the measured age and the tolerance travel with the decision.
using System;
using PlantProcess.Application.Jobs.Dependencies;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Dependencies;

[Trait("BacklogTask", "T-106")]
public sealed class JobDependencyFreshnessEvidenceTests
{
    private static readonly Guid Upstream = new Guid("eeeeeeee-1111-2222-3333-444444444444");
    private static readonly Guid Run = new Guid("ffffffff-1111-2222-3333-444444444444");
    private static readonly Guid Downstream = new Guid("99999999-1111-2222-3333-444444444444");
    private static readonly Guid Job = new Guid("88888888-1111-2222-3333-444444444444");
    private static readonly DateTime Now = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    private static JobDependencyOutcome Evaluate(bool currentCycle, double ageMinutes, int? tolerance, bool allowStale)
        => JobDependencyEvaluator.Evaluate(
            Upstream, true, null, Run, JobRunStatus.Ok, 7,
            upstreamRanInCurrentCycle: currentCycle,
            upstreamCompletedAtUtc: Now.AddMinutes(-ageMinutes),
            evaluatedAtUtc: Now,
            stalenessToleranceMinutes: tolerance,
            allowStaleReuse: allowStale);

    [Fact]
    public void A_snapshot_still_builds_from_three_values_and_carries_none()
    {
        var legacy = new JobRunSnapshot(Run, JobRunStatus.Ok, 7);
        Assert.Null(legacy.CompletedAtUtc);

        var measured = new JobRunSnapshot(Run, JobRunStatus.Ok, 7, Now);
        Assert.Equal(Now, measured.CompletedAtUtc);

        var recovered = legacy with { TargetDefinitionVersion = 9, CompletedAtUtc = Now };
        Assert.Equal(9, recovered.TargetDefinitionVersion);
        Assert.Equal(Now, recovered.CompletedAtUtc);
        Assert.Equal(Run, recovered.RunId);
    }

    [Fact]
    public void An_accepted_reuse_reports_the_age_it_measured_and_the_tolerance_it_used()
    {
        var outcome = Evaluate(false, 900, 60, true);
        Assert.Equal(JobDependencyResolution.StaleAccepted, outcome.Resolution);
        Assert.Equal(900, outcome.UpstreamAgeMinutes);
        Assert.Equal(60, outcome.ToleranceMinutes);
    }

    [Fact]
    public void A_refusal_also_reports_what_it_measured()
    {
        var outcome = Evaluate(false, 900, 60, false);
        Assert.Equal(JobDependencyResolution.Blocked, outcome.Resolution);
        Assert.Equal(900, outcome.UpstreamAgeMinutes);
        Assert.Equal(60, outcome.ToleranceMinutes);
    }

    [Fact]
    public void An_edge_with_no_declared_tolerance_keeps_the_accepted_behaviour()
    {
        // This is how JobDependencyService treats an edge that declares no tolerance:
        // no freshness requirement was declared, so a successful upstream satisfies it.
        var outcome = Evaluate(true, 100000, null, false);
        Assert.Equal(JobDependencyResolution.Satisfied, outcome.Resolution);
        Assert.False(outcome.BlocksDownstream);
    }

    [Fact]
    public void The_evidence_row_stores_exactly_what_the_outcome_measured()
    {
        var outcome = Evaluate(false, 900, 60, true);

        var row = new JobRunDependency(
            Downstream,
            outcome.DependsOnRunId,
            Job,
            outcome.DependsOnJobDefinitionId,
            outcome.Resolution,
            outcome.ExpectedVersion,
            outcome.ActualVersion,
            outcome.Reason,
            outcome.UpstreamAgeMinutes,
            outcome.ToleranceMinutes);

        Assert.Equal(JobDependencyResolution.StaleAccepted, row.Resolution);
        Assert.Equal(900, row.UpstreamAgeMinutes);
        Assert.Equal(60, row.ToleranceMinutes);
        Assert.Equal(Run, row.DependsOnRunId);
    }

    [Fact]
    public void A_never_run_upstream_still_persists_a_null_upstream_identity_and_no_age()
    {
        var outcome = JobDependencyEvaluator.Evaluate(Upstream, true, null, null, null, null);
        Assert.Null(outcome.DependsOnRunId);
        Assert.Null(outcome.UpstreamAgeMinutes);

        var row = new JobRunDependency(
            Downstream, outcome.DependsOnRunId, Job, outcome.DependsOnJobDefinitionId,
            outcome.Resolution, outcome.ExpectedVersion, outcome.ActualVersion, outcome.Reason,
            outcome.UpstreamAgeMinutes, outcome.ToleranceMinutes);

        Assert.Null(row.DependsOnRunId);
        Assert.Null(row.UpstreamAgeMinutes);
    }

    [Fact]
    public void One_evaluation_instant_makes_the_boundary_reproducible()
    {
        Assert.Equal(JobDependencyResolution.Satisfied, Evaluate(false, 60, 60, false).Resolution);
        Assert.Equal(JobDependencyResolution.StaleAccepted, Evaluate(false, 60.0001, 60, true).Resolution);
    }
}
