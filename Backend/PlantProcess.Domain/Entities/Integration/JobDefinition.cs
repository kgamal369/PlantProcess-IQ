using PlantProcess.Domain.Common;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// DB-backed source of truth for every operational job in PlantProcess IQ.
///
/// Examples:
/// - DB link import job
/// - Canonical refresh job
/// - Data-quality scan job
/// - Risk scoring job
/// - ML learning job
/// - Customer custom job
///
/// This entity replaces the old Jobs Monitor behavior where job rows were
/// synthesized from ImportBatch records and hard-coded placeholders.
/// </summary>
public class JobDefinition : BaseEntity
{
    public string JobCode { get; private set; } = null!;

    public string JobName { get; private set; } = null!;

    public JobDefinitionType JobType { get; private set; }

    /// <summary>
    /// Optional business target.
    /// Examples:
    /// - ConnectionProfileId for DbLinkImport
    /// - MappingDefinitionId for CanonicalRefresh
    /// - DefectCatalogId for ML defect analysis
    /// - null for global jobs such as weekly full learning
    /// </summary>
    public Guid? TargetId { get; private set; }

    public string? TargetType { get; private set; }

    /// <summary>
    /// Generic schedule expression.
    /// MVP accepts readable interval strings such as:
    /// - Every 2 minutes
    /// - Every 15 minutes
    /// - Daily 02:00
    /// - Weekly Sunday 03:00
    ///
    /// Later this can be replaced by CRON while preserving this public field.
    /// </summary>
    public string ScheduleExpression { get; private set; } = null!;

    public bool IsEnabled { get; private set; } = true;

    public DateTime? LastRunStartedAtUtc { get; private set; }

    public DateTime? LastRunCompletedAtUtc { get; private set; }

    public long? LastRunDurationMs { get; private set; }

    public JobRunStatus LastRunStatus { get; private set; } = JobRunStatus.NeverRun;

    public string? LastFailureReason { get; private set; }

    public DateTime? NextRunAtUtc { get; private set; }

    public string? Description { get; private set; }

    /// <summary>
    /// T-064. WHICH GOVERNED DEFINITION THIS JOB EXECUTES.
    ///
    /// Distinct from TargetId above, which is an untyped business pointer with no
    /// version, no governance and no refusal behaviour. That field stays for
    /// backward compatibility and is not the target semantics; this one is.
    ///
    /// T-089/T-090 establish the canonical definition-store authority and T-106
    /// owns the physical convergence - the foreign key to definition_store(id) and
    /// its final constraints. Until then the identity is resolved through
    /// IDefinitionService, and no foreign key points at storage that is scheduled
    /// for replacement.
    /// </summary>
    public Guid? TargetDefinitionId { get; private set; }

    /// <summary>
    /// T-064. The kind of the targeted definition, held as text because the kind
    /// vocabulary lives in the application layer and because a renumbered enum must
    /// never silently reinterpret a job that was stored years earlier.
    /// </summary>
    public string? TargetDefinitionKind { get; private set; }

    /// <summary>T-064. Whether the job follows the published version or one pinned number.</summary>
    public JobTargetVersionPolicy? TargetVersionPolicy { get; private set; }

    /// <summary>T-064. The pinned version number. Present under Pinned and absent otherwise.</summary>
    public int? TargetDefinitionVersion { get; private set; }

    /// <summary>True when this job can say what it executes.</summary>
    /// <summary>
    /// T-064. The parameters configured for FUTURE executions of this job.
    ///
    /// Not what any past run used. JobRunHistory snapshots that separately, so
    /// editing this value cannot rewrite what a completed run did.
    ///
    /// null and "{}" are different statements and both survive persistence.
    /// </summary>
    public string? TargetParametersJson { get; private set; }

    /// <summary>True when this job can say what it executes.</summary>
    public bool HasTargetDefinition => TargetDefinitionId.HasValue;


    private JobDefinition()
    {
    }

    public JobDefinition(
        string jobCode,
        string jobName,
        JobDefinitionType jobType,
        string scheduleExpression,
        bool isSynthetic,
        Guid? targetId = null,
        string? targetType = null,
        bool isEnabled = true,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(jobCode))
            throw new ArgumentException("Job code is required.", nameof(jobCode));

        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentException("Job name is required.", nameof(jobName));

        if (string.IsNullOrWhiteSpace(scheduleExpression))
            throw new ArgumentException("Schedule expression is required.", nameof(scheduleExpression));

        JobCode = NormalizeCode(jobCode);
        JobName = jobName.Trim();
        JobType = jobType;
        TargetId = targetId;
        TargetType = Clean(targetType);
        ScheduleExpression = scheduleExpression.Trim();
        IsEnabled = isEnabled;
        Description = Clean(description);

        LastRunStatus = JobRunStatus.NeverRun;

        IsSynthetic = isSynthetic;
        SourceSystem = Clean(sourceSystem);
        SourceRecordId = Clean(sourceRecordId);
    }

    public void UpdateDefinition(
        string jobName,
        JobDefinitionType jobType,
        string scheduleExpression,
        Guid? targetId,
        string? targetType,
        bool isEnabled,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentException("Job name is required.", nameof(jobName));

        if (string.IsNullOrWhiteSpace(scheduleExpression))
            throw new ArgumentException("Schedule expression is required.", nameof(scheduleExpression));

        JobName = jobName.Trim();
        JobType = jobType;
        ScheduleExpression = scheduleExpression.Trim();
        TargetId = targetId;
        TargetType = Clean(targetType);
        IsEnabled = isEnabled;
        Description = Clean(description);

        MarkAsUpdated();
    }

    public void UpdateSchedule(string scheduleExpression, DateTime? nextRunAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(scheduleExpression))
            throw new ArgumentException("Schedule expression is required.", nameof(scheduleExpression));

        ScheduleExpression = scheduleExpression.Trim();
        NextRunAtUtc = nextRunAtUtc;
        MarkAsUpdated();
    }

    public void Enable(DateTime? nextRunAtUtc = null)
    {
        IsEnabled = true;
        NextRunAtUtc = nextRunAtUtc;
        MarkAsUpdated();
    }

    public void Disable()
    {
        IsEnabled = false;
        NextRunAtUtc = null;
        MarkAsUpdated();
    }

    public void MarkRunning(DateTime? startedAtUtc = null)
    {
        LastRunStartedAtUtc = startedAtUtc ?? DateTime.UtcNow;
        LastRunCompletedAtUtc = null;
        LastRunDurationMs = null;
        LastRunStatus = JobRunStatus.Running;
        LastFailureReason = null;
        MarkAsUpdated();
    }

    public void MarkSucceeded(long? durationMs = null, DateTime? completedAtUtc = null)
    {
        var completed = completedAtUtc ?? DateTime.UtcNow;

        LastRunCompletedAtUtc = completed;
        LastRunDurationMs = durationMs ?? CalculateDurationMs(completed);
        LastRunStatus = JobRunStatus.Ok;
        LastFailureReason = null;
        MarkAsUpdated();
    }

    public void MarkFailed(string failureReason, long? durationMs = null, DateTime? completedAtUtc = null)
    {
        var completed = completedAtUtc ?? DateTime.UtcNow;

        LastRunCompletedAtUtc = completed;
        LastRunDurationMs = durationMs ?? CalculateDurationMs(completed);
        LastRunStatus = JobRunStatus.Failed;
        LastFailureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "Unknown job failure."
            : failureReason.Trim();

        MarkAsUpdated();
    }

    public void MarkTimedOut(string failureReason, long? durationMs = null, DateTime? completedAtUtc = null)
    {
        var completed = completedAtUtc ?? DateTime.UtcNow;

        LastRunCompletedAtUtc = completed;
        LastRunDurationMs = durationMs ?? CalculateDurationMs(completed);
        LastRunStatus = JobRunStatus.Timeout;
        LastFailureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "Job timed out."
            : failureReason.Trim();

        MarkAsUpdated();
    }

    private long? CalculateDurationMs(DateTime completedAtUtc)
    {
        if (!LastRunStartedAtUtc.HasValue)
            return null;

        var duration = completedAtUtc - LastRunStartedAtUtc.Value;

        if (duration.TotalMilliseconds < 0)
            return null;

        return (long)duration.TotalMilliseconds;
    }

    /// <summary>
    /// T-064. Declares what this job executes.
    ///
    /// The guards here are structural, not policy: a pinned target without a
    /// version, or a published-version target carrying one, are states in which
    /// two fields disagree about what runs, and no caller downstream should have
    /// to decide which to believe. Whether a job CLASS requires a target at all,
    /// and which kinds it may run, are JB01 and JB02 - governed refusals that
    /// belong to the application layer, not to this entity.
    /// </summary>
    public void AssignTargetDefinition(
        string targetDefinitionKind,
        Guid targetDefinitionId,
        JobTargetVersionPolicy versionPolicy,
        int? pinnedVersion = null,
        string? targetParametersJson = null)
    {
        if (string.IsNullOrWhiteSpace(targetDefinitionKind))
            throw new ArgumentException("Target definition kind is required.", nameof(targetDefinitionKind));

        if (targetDefinitionId == Guid.Empty)
            throw new ArgumentException("Target definition ID is required.", nameof(targetDefinitionId));

        if (versionPolicy == JobTargetVersionPolicy.Pinned)
        {
            if (!pinnedVersion.HasValue)
                throw new ArgumentException(
                    "A pinned target requires a version number.", nameof(pinnedVersion));

            if (pinnedVersion.Value <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(pinnedVersion), "A pinned version number must be greater than zero.");
        }
        else if (pinnedVersion.HasValue)
        {
            throw new ArgumentException(
                "A job that follows the published version cannot also pin one.", nameof(pinnedVersion));
        }

        // Validated before ANY field is written.
        //
        // Anchored after the assignments, this refusal left the job carrying the
        // kind, the identity and the policy it had just rejected. A refusal that
        // half-applies is worse than no refusal, because the object looks
        // configured. Assert.False(job.HasTargetDefinition) found it; review did not.
        string? parameters = JobTargetParameters.Require(
            targetParametersJson, nameof(targetParametersJson));

        TargetDefinitionKind = targetDefinitionKind.Trim();
        TargetDefinitionId = targetDefinitionId;
        TargetVersionPolicy = versionPolicy;
        TargetDefinitionVersion = pinnedVersion;
        TargetParametersJson = parameters;

        MarkAsUpdated();
    }

    /// <summary>T-064. Removes the target. All four fields clear together or none do.</summary>
    public void ClearTargetDefinition()
    {
        TargetDefinitionId = null;
        TargetDefinitionKind = null;
        TargetVersionPolicy = null;
        TargetDefinitionVersion = null;
        TargetParametersJson = null;

        MarkAsUpdated();
    }

    private static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant().Replace(" ", "_");
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}