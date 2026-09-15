using PlantProcess.Domain.Common;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// Immutable-ish operational history for a JobDefinition execution.
/// 
/// A JobDefinition stores the latest/current state.
/// JobRunHistory stores every execution attempt for monitoring and diagnosis.
/// </summary>
public class JobRunHistory : BaseEntity
{
    public Guid JobDefinitionId { get; private set; }

    public string JobCode { get; private set; } = null!;

    public string JobName { get; private set; } = null!;

    public JobDefinitionType JobType { get; private set; }

    public JobRunStatus Status { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public long? DurationMs { get; private set; }

    public string TriggerSource { get; private set; } = null!;

    public string? TriggeredBy { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? FailureReason { get; private set; }

    public string? RunMessage { get; private set; }

    public string? ResultSummaryJson { get; private set; }

    /// <summary>T-064. The governed definition this run executed, if any.</summary>
    public Guid? TargetDefinitionId { get; private set; }

    /// <summary>T-064. The kind of the definition this run executed.</summary>
    public string? TargetDefinitionKind { get; private set; }

    /// <summary>
    /// T-064. THE VERSION THAT ACTUALLY RAN.
    ///
    /// Not the version the job was configured with - the one resolution returned.
    /// Under the published-version policy those are different facts, and this is
    /// the one that answers "what did it do" a month later.
    /// </summary>
    public int? TargetDefinitionVersion { get; private set; }

    /// <summary>T-064. The policy that produced the resolved version.</summary>
    public JobTargetVersionPolicy? TargetVersionPolicy { get; private set; }

    /// <summary>
    /// T-064. THE PARAMETERS THIS RUN ACTUALLY USED.
    ///
    /// Snapshotted, not referenced. Editing the job definition afterwards changes
    /// what the NEXT run will use and cannot touch this. A history that followed
    /// the current configuration would answer "what did it do" with what the job
    /// would do today, which is a different question and a false answer.
    /// </summary>
    public string? TargetParametersJson { get; private set; }


    private JobRunHistory()
    {
    }

    /// <summary>
    /// T-106 B2.3c. The identity of the scheduled occurrence this run belongs to, or NULL
    /// for a manual run. Migration 843 puts a partial unique index on this column, and that
    /// index is the concurrency authority: two dispatchers racing on the same occurrence
    /// race on the database, not on a read-then-write in application code.
    /// </summary>
    public string? OccurrenceKey { get; private set; }

    /// <summary>The nominal scheduled instant, never the poll instant that noticed it.</summary>
    public DateTime? NominalAtUtc { get; private set; }

    /// <summary>
    /// T-106 B2.4. When an operator asked this run to stop. A request is not a terminal
    /// state and never becomes one on its own: the executor has to answer it first.
    /// </summary>
    public DateTime? CancellationRequestedAtUtc { get; private set; }

    public string? CancellationRequestedBy { get; private set; }

    public string? CancellationReason { get; private set; }

    /// <summary>When the executor acknowledged and stopped cooperatively.</summary>
    public DateTime? CancellationAcknowledgedAtUtc { get; private set; }

    /// <summary>The run is running and has an unanswered cancellation request.</summary>
    public bool HasPendingCancellation
        => CancellationRequestedAtUtc.HasValue && !CancellationAcknowledgedAtUtc.HasValue;

    public JobRunHistory(
        Guid jobDefinitionId,
        string jobCode,
        string jobName,
        JobDefinitionType jobType,
        string triggerSource,
        string? triggeredBy,
        string? correlationId,
        bool isSynthetic,
        string? sourceSystem,
        string? sourceRecordId,
        string? occurrenceKey = null,
        DateTime? nominalAtUtc = null)
    {
        if (jobDefinitionId == Guid.Empty)
            throw new ArgumentException("Job definition ID is required.", nameof(jobDefinitionId));

        if (string.IsNullOrWhiteSpace(jobCode))
            throw new ArgumentException("Job code is required.", nameof(jobCode));

        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentException("Job name is required.", nameof(jobName));

        JobDefinitionId = jobDefinitionId;
        JobCode = jobCode.Trim();
        JobName = jobName.Trim();
        JobType = jobType;
        Status = JobRunStatus.Running;
        StartedAtUtc = DateTime.UtcNow;
        TriggerSource = string.IsNullOrWhiteSpace(triggerSource)
            ? "Unknown"
            : triggerSource.Trim();
        TriggeredBy = Clean(triggeredBy);
        CorrelationId = Clean(correlationId);

        IsSynthetic = isSynthetic;
        SourceSystem = Clean(sourceSystem);
        SourceRecordId = Clean(sourceRecordId);

        // An occurrence is either a real scheduled identity with its nominal instant, or it
        // is absent. Half of one would make the unique index meaningless.
        var occurrence = Clean(occurrenceKey);
        if (occurrence is null != nominalAtUtc is null)
        {
            throw new ArgumentException(
                "A scheduled run carries both an occurrence key and its nominal instant; a manual run carries neither.",
                nameof(occurrenceKey));
        }

        OccurrenceKey = occurrence;
        NominalAtUtc = nominalAtUtc;
    }

    public void MarkSucceeded(string? message = null, string? resultSummaryJson = null)
    {
        CompletedAtUtc = DateTime.UtcNow;
        DurationMs = CalculateDurationMs(CompletedAtUtc.Value);
        Status = JobRunStatus.Ok;
        FailureReason = null;
        RunMessage = Clean(message) ?? "Job completed successfully.";
        ResultSummaryJson = CleanJson(resultSummaryJson);
        MarkAsUpdated();
    }

    /// <summary>
    /// T-106 B2.4. Records the operator's request. It does not stop anything and it does
    /// not change Status: only the executor can answer it. Repeating the request keeps the
    /// first one, so a nervous operator cannot rewrite who asked and when.
    /// </summary>
    public void RequestCancellation(string? requestedBy, string? reason)
    {
        if (Status != JobRunStatus.Running)
        {
            throw new InvalidOperationException(
                "Only a running run can be asked to cancel; this run is " + Status + ".");
        }

        if (CancellationRequestedAtUtc.HasValue)
        {
            return;
        }

        CancellationRequestedAtUtc = DateTime.UtcNow;
        CancellationRequestedBy = Clean(requestedBy);
        CancellationReason = Clean(reason);
        MarkAsUpdated();
    }

    /// <summary>
    /// T-106 B2.4. The executor answering the request. Still not a terminal state: it
    /// records that the work actually stopped, which MarkCancelled then makes terminal.
    /// </summary>
    public void AcknowledgeCancellation()
    {
        if (!CancellationRequestedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "There is no cancellation request on this run to acknowledge.");
        }

        if (CancellationAcknowledgedAtUtc.HasValue)
        {
            return;
        }

        CancellationAcknowledgedAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }

    /// <summary>
    /// T-106 B2.4. Terminal Cancelled, and only after an acknowledged request. A run that
    /// nobody asked to stop, or that the executor never acknowledged, cannot claim it.
    /// </summary>
    public void MarkCancelled(string? message = null)
    {
        if (!CancellationRequestedAtUtc.HasValue || !CancellationAcknowledgedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "A run becomes Cancelled only after a cancellation request the executor acknowledged.");
        }

        CompletedAtUtc = DateTime.UtcNow;
        DurationMs = CalculateDurationMs(CompletedAtUtc.Value);
        Status = JobRunStatus.Cancelled;
        FailureReason = null;
        RunMessage = Clean(message) ?? "Run cancelled at the operator's request.";
        MarkAsUpdated();
    }

    public void MarkFailed(string failureReason, string? resultSummaryJson = null)
    {
        CompletedAtUtc = DateTime.UtcNow;
        DurationMs = CalculateDurationMs(CompletedAtUtc.Value);
        Status = JobRunStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "Unknown job failure."
            : failureReason.Trim();
        RunMessage = FailureReason;
        ResultSummaryJson = CleanJson(resultSummaryJson);
        MarkAsUpdated();
    }

    /// <summary>
    /// T-106. THE ATTEMPT EXISTS AND COMPUTE NEVER STARTED.
    ///
    /// Called before the row is first persisted, so the run is born terminal:
    /// the database never observes Running for a blocked attempt. Duration is
    /// deliberately zero rather than null - the attempt was evaluated and
    /// refused, which took no compute, and null would read as unknown.
    /// </summary>
    public void MarkBlocked(string reason)
    {
        CompletedAtUtc = DateTime.UtcNow;
        DurationMs = 0;
        Status = JobRunStatus.Blocked;
        FailureReason = string.IsNullOrWhiteSpace(reason)
            ? "The run was blocked before compute started."
            : reason.Trim();
        RunMessage = FailureReason;
        MarkAsUpdated();
    }

    public void MarkTimedOut(string failureReason, string? resultSummaryJson = null)
    {
        CompletedAtUtc = DateTime.UtcNow;
        DurationMs = CalculateDurationMs(CompletedAtUtc.Value);
        Status = JobRunStatus.Timeout;
        FailureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "Job timed out."
            : failureReason.Trim();
        RunMessage = FailureReason;
        ResultSummaryJson = CleanJson(resultSummaryJson);
        MarkAsUpdated();
    }

    /// <summary>
    /// T-064. Records the target and the version resolution actually returned.
    /// Called once, before the work runs, so a run that fails still says which
    /// version it was attempting.
    /// </summary>
    public void RecordResolvedTarget(
        string targetDefinitionKind,
        Guid targetDefinitionId,
        int resolvedVersion,
        JobTargetVersionPolicy policyApplied,
        string? targetParametersJson = null)
    {
        if (string.IsNullOrWhiteSpace(targetDefinitionKind))
            throw new ArgumentException("Target definition kind is required.", nameof(targetDefinitionKind));

        if (targetDefinitionId == Guid.Empty)
            throw new ArgumentException("Target definition ID is required.", nameof(targetDefinitionId));

        if (resolvedVersion <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(resolvedVersion), "A resolved version number must be greater than zero.");

        // Same rule as the definition: nothing is recorded until the payload is
        // accepted, so a refused snapshot records no target at all.
        string? parameters = JobTargetParameters.Require(
            targetParametersJson, nameof(targetParametersJson));

        TargetDefinitionKind = targetDefinitionKind.Trim();
        TargetDefinitionId = targetDefinitionId;
        TargetDefinitionVersion = resolvedVersion;
        TargetVersionPolicy = policyApplied;
        TargetParametersJson = parameters;

        MarkAsUpdated();
    }

    private long CalculateDurationMs(DateTime completedAtUtc)
    {
        var duration = completedAtUtc - StartedAtUtc;
        return Math.Max(0, (long)duration.TotalMilliseconds);
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? CleanJson(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}