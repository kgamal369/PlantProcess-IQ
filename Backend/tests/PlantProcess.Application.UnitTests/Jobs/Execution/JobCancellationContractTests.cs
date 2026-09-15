// T-106 Phase A tests: requesting cancellation is not the same as being cancelled.
using PlantProcess.Application.Jobs.Execution;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Execution;

[Trait("BacklogTask", "T-106")]
public sealed class JobCancellationContractTests
{
    [Fact]
    public void A_running_run_accepts_one_cancellation_request()
    {
        var first = JobCancellationContract.Request(CancellationState.None, runIsRunning: true, runIsTerminal: false);
        Assert.True(first.Accepted);
        Assert.Equal(CancellationState.Requested, first.State);

        var second = JobCancellationContract.Request(first.State, runIsRunning: true, runIsTerminal: false);
        Assert.False(second.Accepted);
        Assert.Equal(CancellationRefusalReason.AlreadyRequested, second.Reason);
        Assert.Equal(CancellationState.Requested, second.State);
    }

    [Fact]
    public void A_run_that_is_not_running_cannot_be_cancelled()
    {
        var queued = JobCancellationContract.Request(CancellationState.None, runIsRunning: false, runIsTerminal: false);
        Assert.False(queued.Accepted);
        Assert.Equal(CancellationRefusalReason.RunNotRunning, queued.Reason);

        var finished = JobCancellationContract.Request(CancellationState.None, runIsRunning: false, runIsTerminal: true);
        Assert.False(finished.Accepted);
        Assert.Equal(CancellationRefusalReason.RunAlreadyTerminal, finished.Reason);
    }

    [Fact]
    public void Acknowledgement_requires_a_request()
    {
        var orphan = JobCancellationContract.Acknowledge(CancellationState.None);
        Assert.False(orphan.Accepted);
        Assert.Equal(CancellationRefusalReason.NoRequestToAcknowledge, orphan.Reason);
    }

    [Fact]
    public void Terminal_cancelled_requires_an_acknowledged_request()
    {
        var tooEarly = JobCancellationContract.Complete(CancellationState.Requested);
        Assert.False(tooEarly.Accepted);
        Assert.Equal(CancellationRefusalReason.NotAcknowledged, tooEarly.Reason);

        var acknowledged = JobCancellationContract.Acknowledge(CancellationState.Requested);
        Assert.True(acknowledged.Accepted);

        var terminal = JobCancellationContract.Complete(acknowledged.State);
        Assert.True(terminal.Accepted);
        Assert.Equal(CancellationState.TerminalCancelled, terminal.State);
    }

    [Fact]
    public void Acknowledgement_and_completion_are_idempotent()
    {
        Assert.True(JobCancellationContract.Acknowledge(CancellationState.Acknowledged).Accepted);
        Assert.Equal(CancellationState.TerminalCancelled,
            JobCancellationContract.Complete(CancellationState.TerminalCancelled).State);
    }
}
