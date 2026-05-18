using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Integration.Contracts.Jobs;

public sealed record JobDefinitionDto(
    Guid Id,
    string JobCode,
    string JobName,
    JobDefinitionType JobType,
    Guid? TargetId,
    string? TargetType,
    string ScheduleExpression,
    bool IsEnabled,
    DateTime? LastRunStartedAtUtc,
    DateTime? LastRunCompletedAtUtc,
    long? LastRunDurationMs,
    JobRunStatus LastRunStatus,
    string? LastFailureReason,
    DateTime? NextRunAtUtc,
    string? Description,
    bool IsSynthetic,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateJobDefinitionRequest(
    string JobCode,
    string JobName,
    JobDefinitionType JobType,
    Guid? TargetId,
    string? TargetType,
    string ScheduleExpression,
    bool IsEnabled,
    string? Description,
    bool IsSynthetic,
    string? SourceSystem,
    string? SourceRecordId);

public sealed record UpdateJobDefinitionRequest(
    string JobName,
    JobDefinitionType JobType,
    Guid? TargetId,
    string? TargetType,
    string ScheduleExpression,
    bool IsEnabled,
    string? Description);

public sealed record UpdateJobRunStatusRequest(
    JobRunStatus Status,
    string? FailureReason,
    long? DurationMs,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc);


