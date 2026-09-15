// T-106 B2 tests: the terminal truth table, and cancellation that stays cancelled.
using System;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Execution;

[Trait("BacklogTask", "T-106")]
public sealed class JobRunStatusConvergenceTests
{
    [Fact]
    public void The_status_enum_carries_a_cancelled_terminal_state()
    {
        Assert.True(Enum.IsDefined(typeof(JobRunStatus), JobRunStatus.Cancelled));
        Assert.Equal(6, (int)JobRunStatus.Cancelled);
        Assert.Equal(5, (int)JobRunStatus.Blocked);
    }

    [Fact]
    public void All_success_is_ok()
    {
        var state = JobRunStatusConvergence.Resolve(new JobUnitCounts(4, 4, 0, 0, false));
        Assert.Equal(JobRunStatus.Ok, state.Status);
        Assert.True(state.IsSuccessfulTerminal);
    }

    [Fact]
    public void One_failed_unit_can_never_produce_a_green_run()
    {
        var state = JobRunStatusConvergence.Resolve(new JobUnitCounts(4, 3, 1, 0, false));
        Assert.Equal(JobRunStatus.Failed, state.Status);
        Assert.Equal(JobAggregateOutcome.Mixed, state.Outcome);
        Assert.False(state.IsSuccessfulTerminal);
    }

    [Fact]
    public void All_failed_is_failed()
    {
        var state = JobRunStatusConvergence.Resolve(new JobUnitCounts(3, 0, 3, 0, false));
        Assert.Equal(JobRunStatus.Failed, state.Status);
        Assert.Equal(JobAggregateOutcome.AllFailed, state.Outcome);
    }

    [Fact]
    public void No_work_completes_truthfully_and_stays_distinguishable()
    {
        var state = JobRunStatusConvergence.Resolve(new JobUnitCounts(0, 0, 0, 0, false));
        Assert.Equal(JobRunStatus.Ok, state.Status);
        Assert.Equal(JobAggregateOutcome.NoWork, state.Outcome);
    }

    [Fact]
    public void Acknowledged_cancellation_produces_the_cancelled_terminal_state()
    {
        var state = JobRunStatusConvergence.Resolve(new JobUnitCounts(4, 4, 0, 0, true));
        Assert.Equal(JobRunStatus.Cancelled, state.Status);
        Assert.False(state.IsSuccessfulTerminal);
    }

    [Fact]
    public void A_cancelled_run_cannot_later_be_reported_as_ok_or_failed()
    {
        Assert.Throws<InvalidOperationException>(
            () => JobRunStatusConvergence.EnsureNoLateSuccess(JobRunStatus.Cancelled, JobRunStatus.Ok));
        Assert.Throws<InvalidOperationException>(
            () => JobRunStatusConvergence.EnsureNoLateSuccess(JobRunStatus.Cancelled, JobRunStatus.Failed));
        Assert.Equal(JobRunStatus.Cancelled,
            JobRunStatusConvergence.EnsureNoLateSuccess(JobRunStatus.Cancelled, JobRunStatus.Cancelled));
    }

    [Fact]
    public void A_running_run_still_converges_normally()
    {
        Assert.Equal(JobRunStatus.Ok, JobRunStatusConvergence.EnsureNoLateSuccess(JobRunStatus.Running, JobRunStatus.Ok));
        Assert.Equal(JobRunStatus.Failed, JobRunStatusConvergence.EnsureNoLateSuccess(JobRunStatus.Running, JobRunStatus.Failed));
    }

    [Fact]
    public void The_cancellation_contract_and_the_terminal_state_agree()
    {
        var requested = JobCancellationContract.Request(CancellationState.None, runIsRunning: true, runIsTerminal: false);
        var acknowledged = JobCancellationContract.Acknowledge(requested.State);
        var terminal = JobCancellationContract.Complete(acknowledged.State);
        Assert.Equal(CancellationState.TerminalCancelled, terminal.State);

        var state = JobRunStatusConvergence.Resolve(new JobUnitCounts(2, 1, 0, 1, true));
        Assert.Equal(JobRunStatus.Cancelled, state.Status);
    }
}
