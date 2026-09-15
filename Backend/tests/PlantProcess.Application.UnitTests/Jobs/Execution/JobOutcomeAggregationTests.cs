// T-106 Phase A tests: a failed unit can never hide inside a green run.
using System;
using PlantProcess.Application.Jobs.Execution;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Execution;

[Trait("BacklogTask", "T-106")]
public sealed class JobOutcomeAggregationTests
{
    [Fact]
    public void All_succeeded_is_the_only_green_shape_with_work()
    {
        var result = JobOutcomeAggregation.Evaluate(new JobUnitCounts(4, 4, 0, 0, false));
        Assert.Equal(JobAggregateOutcome.AllSucceeded, result.Outcome);
        Assert.True(result.IsSuccessfulTerminal);
    }

    [Fact]
    public void One_failed_unit_makes_the_run_not_successful()
    {
        var result = JobOutcomeAggregation.Evaluate(new JobUnitCounts(4, 3, 1, 0, false));
        Assert.Equal(JobAggregateOutcome.Mixed, result.Outcome);
        Assert.False(result.IsSuccessfulTerminal);
        Assert.Contains("Failed=1", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_unit_failing_is_reported_as_such()
    {
        var result = JobOutcomeAggregation.Evaluate(new JobUnitCounts(3, 0, 3, 0, false));
        Assert.Equal(JobAggregateOutcome.AllFailed, result.Outcome);
        Assert.False(result.IsSuccessfulTerminal);
    }

    [Fact]
    public void No_work_is_distinct_from_success()
    {
        var result = JobOutcomeAggregation.Evaluate(new JobUnitCounts(0, 0, 0, 0, false));
        Assert.Equal(JobAggregateOutcome.NoWork, result.Outcome);
        Assert.True(result.IsSuccessfulTerminal);
    }

    [Fact]
    public void Acknowledged_cancellation_outranks_the_unit_counts()
    {
        var result = JobOutcomeAggregation.Evaluate(new JobUnitCounts(4, 4, 0, 0, true));
        Assert.Equal(JobAggregateOutcome.Cancelled, result.Outcome);
        Assert.False(result.IsSuccessfulTerminal);
    }

    [Fact]
    public void Skipped_units_do_not_turn_a_failure_green()
    {
        var result = JobOutcomeAggregation.Evaluate(new JobUnitCounts(5, 0, 2, 3, false));
        Assert.Equal(JobAggregateOutcome.AllFailed, result.Outcome);
    }

    [Fact]
    public void Negative_counts_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => JobOutcomeAggregation.Evaluate(new JobUnitCounts(1, -1, 0, 0, false)));
    }
}
