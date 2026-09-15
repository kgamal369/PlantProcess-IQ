// T-106 B2.1 + B2.5: one place where an aggregate outcome becomes a terminal run status.
// It consumes the frozen JobOutcomeAggregation kernel and adds nothing to it. A run that
// was cancelled, or that contains a failed unit, can never be reported as Ok.
using System;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Execution;

public sealed record JobTerminalState(JobRunStatus Status, bool IsSuccessfulTerminal, JobAggregateOutcome Outcome, string Message);

public static class JobRunStatusConvergence
{
    /// <summary>Counts in, terminal truth out. No caller decides this for itself.</summary>
    public static JobTerminalState Resolve(JobUnitCounts counts)
        => Resolve(JobOutcomeAggregation.Evaluate(counts));

    public static JobTerminalState Resolve(JobAggregateResult aggregate)
    {
        if (aggregate is null) { throw new ArgumentNullException(nameof(aggregate)); }

        var status = aggregate.Outcome switch
        {
            // No work is a truthful completion: nothing was available, nothing failed.
            JobAggregateOutcome.NoWork => JobRunStatus.Ok,
            JobAggregateOutcome.AllSucceeded => JobRunStatus.Ok,
            JobAggregateOutcome.Mixed => JobRunStatus.Failed,
            JobAggregateOutcome.AllFailed => JobRunStatus.Failed,
            JobAggregateOutcome.Cancelled => JobRunStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(aggregate), aggregate.Outcome, "Unknown aggregate outcome.")
        };

        return new JobTerminalState(status, aggregate.IsSuccessfulTerminal, aggregate.Outcome, aggregate.Message);
    }

    /// <summary>
    /// A cancelled run never converges to success afterwards. This is called wherever a
    /// terminal status is about to be written, so a late completion cannot overwrite it.
    /// </summary>
    public static JobRunStatus EnsureNoLateSuccess(JobRunStatus current, JobRunStatus proposed)
    {
        if (current == JobRunStatus.Cancelled && proposed != JobRunStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "A cancelled run cannot be re-reported as " + proposed
                    + ": cancellation was acknowledged, so the work did not complete.");
        }

        return proposed;
    }
}
