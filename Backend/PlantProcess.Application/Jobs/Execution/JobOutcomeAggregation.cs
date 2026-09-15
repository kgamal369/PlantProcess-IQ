// T-106 Phase A: truthful aggregate terminal outcome.
// Pure kernel. The call sites that must consume it are a later, externally gated slice.
using System;
using System.Globalization;

namespace PlantProcess.Application.Jobs.Execution;

/// <summary>The five terminal shapes a run can have. There is no sixth, and no silent success.</summary>
public enum JobAggregateOutcome
{
    NoWork = 0,
    AllSucceeded = 1,
    Mixed = 2,
    AllFailed = 3,
    Cancelled = 4
}

public sealed record JobUnitCounts(int Processed, int Succeeded, int Failed, int Skipped, bool CancellationAcknowledged);

public sealed record JobAggregateResult(JobAggregateOutcome Outcome, bool IsSuccessfulTerminal, string Message);

public static class JobOutcomeAggregation
{
    /// <summary>
    /// A run is successful only when nothing failed. A failed unit can never be hidden inside a
    /// green run, and cancellation outranks the unit counts because the operator stopped the work.
    /// </summary>
    public static JobAggregateResult Evaluate(JobUnitCounts counts)
    {
        if (counts is null) { throw new ArgumentNullException(nameof(counts)); }
        if (counts.Succeeded < 0 || counts.Failed < 0 || counts.Skipped < 0 || counts.Processed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(counts), "Unit counts cannot be negative.");
        }

        var suffix = " Processed=" + counts.Processed.ToString(CultureInfo.InvariantCulture)
            + ", Succeeded=" + counts.Succeeded.ToString(CultureInfo.InvariantCulture)
            + ", Failed=" + counts.Failed.ToString(CultureInfo.InvariantCulture)
            + ", Skipped=" + counts.Skipped.ToString(CultureInfo.InvariantCulture) + ".";

        if (counts.CancellationAcknowledged)
        {
            return new JobAggregateResult(JobAggregateOutcome.Cancelled, false, "Run cancelled." + suffix);
        }

        if (counts.Failed > 0)
        {
            return counts.Succeeded > 0
                ? new JobAggregateResult(JobAggregateOutcome.Mixed, false, "Run completed with failed units." + suffix)
                : new JobAggregateResult(JobAggregateOutcome.AllFailed, false, "Every processed unit failed." + suffix);
        }

        if (counts.Succeeded == 0)
        {
            return new JobAggregateResult(JobAggregateOutcome.NoWork, true, "No work was available." + suffix);
        }

        return new JobAggregateResult(JobAggregateOutcome.AllSucceeded, true, "Run completed successfully." + suffix);
    }
}
