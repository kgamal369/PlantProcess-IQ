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
        string? sourceRecordId)
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