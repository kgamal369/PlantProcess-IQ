using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Integration.Contracts.Jobs;

public sealed record JobRunHistoryDto(
    Guid Id,
    Guid JobDefinitionId,
    string JobCode,
    string JobName,
    JobDefinitionType JobType,
    JobRunStatus Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    long? DurationMs,
    string TriggerSource,
    string? TriggeredBy,
    string? CorrelationId,
    string? FailureReason,
    string? RunMessage,
    string? ResultSummaryJson);

public sealed record JobActionResponseDto(
    Guid JobDefinitionId,
    string JobCode,
    string JobName,
    JobDefinitionType JobType,
    JobRunStatus Status,
    string Message,
    Guid? JobRunHistoryId,
    DateTime ActionedAtUtc);

public sealed record UpsertJobDefinitionRequest(
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

public sealed record UpdateConnectionImportScheduleRequest(
    string ScheduleExpression,
    int ImportIntervalMinutes);

public sealed record UpdateMappingRefreshScheduleRequest(
    string ScheduleExpression,
    int RefreshIntervalMinutes);

public sealed record RunJobNowRequest(
    string? RequestedBy,
    string? CorrelationId);



