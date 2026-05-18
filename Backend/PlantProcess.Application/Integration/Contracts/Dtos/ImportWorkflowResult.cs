using PlantProcess.Application.Integration.Contracts.Mapping;

namespace PlantProcess.Application.Integration.Contracts.Dtos;

public sealed record ImportWorkflowResult(
    Guid ImportBatchId,
    Guid MappingDefinitionId,
    string ImportBatchCode,
    string SourceObjectName,
    string ImportBatchStatus,
    int InputRows,
    int StagingRowsCreated,
    int MappingProcessedRows,
    int MappingMappedRows,
    int MappingSkippedRows,
    int MappingFailedRows,
    int DataQualityCandidatesFound,
    int DataQualityNewIssuesPersisted,
    int DataQualityExistingIssuesSkipped,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    TimeSpan Duration,
    string? ErrorMessage,
    IReadOnlyCollection<MappingExecutionRowResult> MappingRows);

public sealed record ImportQueueProcessingSummary(
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    TimeSpan Duration,
    int BatchesScanned,
    int BatchesProcessed,
    int BatchesCompleted,
    int BatchesFailed,
    int BatchesSkipped,
    IReadOnlyCollection<ImportQueueProcessingItem> Items);

public sealed record ImportQueueProcessingItem(
    Guid ImportBatchId,
    string ImportBatchCode,
    string SourceObjectName,
    string Status,
    Guid? MappingDefinitionId,
    string? MappingCode,
    int ProcessedRows,
    int MappedRows,
    int FailedRows,
    string? Message);



