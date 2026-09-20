using PlantProcess.Domain.Common;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// RUNTIME EVIDENCE FOR ONE AUTHORED BLOCK OF ONE REAL RUN.
///
/// BlockId is the authored board node id and nothing else. A display label changes when
/// somebody renames a block, an array index changes when somebody reorders one, and a
/// runtime GUID answers no question at all - each would silently break the link between
/// what a person authored and what the runtime reports back to them.
///
/// ExecutionOrdinal is persisted rather than derived. The order is decided by the
/// governed topology at execution time, and a reader a month later cannot recompute it
/// without the version that produced it, so the run records the order it actually used.
///
/// Measured facts are nullable on purpose. A block that never ran has no row count, and
/// writing a zero would be a measurement that was never taken.
/// </summary>
public class JobRunBlockEvidence : BaseEntity
{
    public Guid JobRunHistoryId { get; private set; }

    public string BlockId { get; private set; } = null!;

    public int ExecutionOrdinal { get; private set; }

    public JobRunBlockStatus Status { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? FinishedAtUtc { get; private set; }

    public int? InputRows { get; private set; }

    public int? OutputRows { get; private set; }

    public string? DiagnosticCode { get; private set; }

    public string? DiagnosticDetail { get; private set; }

    private JobRunBlockEvidence()
    {
    }

    public JobRunBlockEvidence(Guid jobRunHistoryId, string blockId, int executionOrdinal)
    {
        if (jobRunHistoryId == Guid.Empty)
        {
            throw new ArgumentException(
                "Block evidence belongs to a genuine job run. There is no evidence without one.",
                nameof(jobRunHistoryId));
        }

        if (string.IsNullOrWhiteSpace(blockId))
        {
            throw new ArgumentException(
                "A stable authored block id is required. Evidence that cannot name its block "
                + "cannot be read back against the definition that produced it.",
                nameof(blockId));
        }

        if (executionOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(executionOrdinal), "An execution ordinal is never negative.");
        }

        JobRunHistoryId = jobRunHistoryId;
        BlockId = blockId.Trim();
        ExecutionOrdinal = executionOrdinal;
        Status = JobRunBlockStatus.Pending;
    }

    public void MarkRunning()
    {
        RefuseTerminalTransition();
        Status = JobRunBlockStatus.Running;
        StartedAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }

    public void MarkSucceeded(int? inputRows, int? outputRows)
    {
        RefuseTerminalTransition();
        Status = JobRunBlockStatus.Succeeded;
        FinishedAtUtc = DateTime.UtcNow;
        InputRows = inputRows;
        OutputRows = outputRows;
        MarkAsUpdated();
    }

    public void MarkFailed(string diagnosticCode, string? detail)
    {
        RefuseTerminalTransition();

        if (string.IsNullOrWhiteSpace(diagnosticCode))
        {
            throw new ArgumentException(
                "A failed block carries a typed diagnostic. An untyped failure cannot be "
                + "told apart from an unfinished one.", nameof(diagnosticCode));
        }

        Status = JobRunBlockStatus.Failed;
        FinishedAtUtc = DateTime.UtcNow;
        DiagnosticCode = diagnosticCode.Trim();
        DiagnosticDetail = detail;
        MarkAsUpdated();
    }

    /// <summary>A dependency did not succeed, so this block never executed.</summary>
    public void MarkBlocked(string reason)
    {
        RefuseTerminalTransition();
        Status = JobRunBlockStatus.Blocked;
        FinishedAtUtc = DateTime.UtcNow;
        DiagnosticCode = "JOB_EXEC_BLOCK_FAILED";
        DiagnosticDetail = reason;
        MarkAsUpdated();
    }

    public void MarkCancelled()
    {
        RefuseTerminalTransition();
        Status = JobRunBlockStatus.Cancelled;
        FinishedAtUtc = DateTime.UtcNow;
        DiagnosticCode = "JOB_EXEC_CANCELLED";
        MarkAsUpdated();
    }

    /// <summary>
    /// ONCE TERMINAL, NEVER AGAIN.
    ///
    /// A late continuation finishing after cancellation must not be able to report the
    /// block as succeeded. The rule lives on the entity because that is the only place
    /// every writer has to pass through.
    /// </summary>
    private void RefuseTerminalTransition()
    {
        if (Status == JobRunBlockStatus.Succeeded
            || Status == JobRunBlockStatus.Failed
            || Status == JobRunBlockStatus.Blocked
            || Status == JobRunBlockStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Block " + BlockId + " already reached terminal state " + Status
                + ". Runtime evidence is not rewritten after the fact.");
        }
    }
}