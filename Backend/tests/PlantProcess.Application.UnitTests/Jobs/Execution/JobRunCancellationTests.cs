// T-106 B2.4b tests: request, acknowledgement and terminal state are three facts, in order.
using System;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Jobs.Execution;

[Trait("BacklogTask", "T-106")]
public sealed class JobRunCancellationTests
{
    private static JobRunHistory Running()
        => new JobRunHistory(
            Guid.NewGuid(), "J1", "J1 name", JobDefinitionType.DbLinkImport,
            "ManualRunNow", "tester", null, false, "test", null);

    [Fact]
    public void A_request_does_not_stop_the_run()
    {
        var run = Running();
        run.RequestCancellation("operator", "taking too long");

        Assert.Equal(JobRunStatus.Running, run.Status);
        Assert.True(run.HasPendingCancellation);
        Assert.Equal("operator", run.CancellationRequestedBy);
        Assert.Equal("taking too long", run.CancellationReason);
        Assert.Null(run.CancellationAcknowledgedAtUtc);
        Assert.Null(run.CompletedAtUtc);
    }

    [Fact]
    public void Repeating_the_request_keeps_the_first_one()
    {
        var run = Running();
        run.RequestCancellation("first", "first reason");
        DateTime? firstAt = run.CancellationRequestedAtUtc;

        run.RequestCancellation("second", "second reason");

        Assert.Equal(firstAt, run.CancellationRequestedAtUtc);
        Assert.Equal("first", run.CancellationRequestedBy);
        Assert.Equal("first reason", run.CancellationReason);
        Assert.Equal(JobRunStatus.Running, run.Status);
    }

    [Fact]
    public void A_run_that_is_not_running_cannot_be_asked_to_cancel()
    {
        var run = Running();
        run.MarkSucceeded("done");

        Assert.Throws<InvalidOperationException>(() => run.RequestCancellation("operator", null));
    }

    [Fact]
    public void An_acknowledgement_without_a_request_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => Running().AcknowledgeCancellation());
    }

    [Fact]
    public void Acknowledgement_is_idempotent_and_still_not_terminal()
    {
        var run = Running();
        run.RequestCancellation("operator", null);
        run.AcknowledgeCancellation();
        DateTime? firstAck = run.CancellationAcknowledgedAtUtc;

        run.AcknowledgeCancellation();

        Assert.Equal(firstAck, run.CancellationAcknowledgedAtUtc);
        Assert.Equal(JobRunStatus.Running, run.Status);
        Assert.False(run.HasPendingCancellation);
    }

    [Fact]
    public void Cancelled_requires_a_request_that_was_acknowledged()
    {
        var never = Running();
        Assert.Throws<InvalidOperationException>(() => never.MarkCancelled());

        var requestedOnly = Running();
        requestedOnly.RequestCancellation("operator", null);
        Assert.Throws<InvalidOperationException>(() => requestedOnly.MarkCancelled());
    }

    [Fact]
    public void The_full_path_ends_in_cancelled_with_its_evidence_intact()
    {
        var run = Running();
        run.RequestCancellation("operator", "shift change");
        run.AcknowledgeCancellation();
        run.MarkCancelled();

        Assert.Equal(JobRunStatus.Cancelled, run.Status);
        Assert.NotNull(run.CompletedAtUtc);
        Assert.NotNull(run.DurationMs);
        Assert.Null(run.FailureReason);
        Assert.Equal("operator", run.CancellationRequestedBy);
        Assert.Equal("shift change", run.CancellationReason);
        Assert.NotNull(run.CancellationAcknowledgedAtUtc);
    }

    [Fact]
    public void A_cancelled_run_refuses_every_late_terminal_state()
    {
        foreach (var late in new[] { JobRunStatus.Ok, JobRunStatus.Failed, JobRunStatus.Timeout })
        {
            Assert.Throws<InvalidOperationException>(
                () => JobRunStatusConvergence.EnsureNoLateSuccess(JobRunStatus.Cancelled, late));
        }

        Assert.Equal(
            JobRunStatus.Cancelled,
            JobRunStatusConvergence.EnsureNoLateSuccess(JobRunStatus.Cancelled, JobRunStatus.Cancelled));
    }

    [Fact]
    public void The_contract_and_the_entity_agree_step_for_step()
    {
        var run = Running();
        var requested = JobCancellationContract.Request(CancellationState.None, runIsRunning: true, runIsTerminal: false);
        Assert.True(requested.Accepted);
        run.RequestCancellation("operator", null);

        var acknowledged = JobCancellationContract.Acknowledge(requested.State);
        Assert.True(acknowledged.Accepted);
        run.AcknowledgeCancellation();

        var terminal = JobCancellationContract.Complete(acknowledged.State);
        Assert.True(terminal.Accepted);
        run.MarkCancelled();

        Assert.Equal(CancellationState.TerminalCancelled, terminal.State);
        Assert.Equal(JobRunStatus.Cancelled, run.Status);
    }

    [Fact]
    public void The_monitor_reports_a_cancelled_run_as_cancelled_not_failed()
    {
        var job = new JobDefinition("J1", "J1 name", JobDefinitionType.DbLinkImport, "Every 15 minutes", false);
        job.MarkRunning(DateTime.UtcNow);

        job.MarkCancelled(1200, DateTime.UtcNow);

        Assert.Equal(JobRunStatus.Cancelled, job.LastRunStatus);
        Assert.Null(job.LastFailureReason);
        Assert.Equal(1200, job.LastRunDurationMs);
    }
}
