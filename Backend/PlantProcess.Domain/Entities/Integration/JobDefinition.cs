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