namespace PlantProcess.Application.Contracts.Integration.Dtos;

public sealed record MappingExecutionResult(
    Guid MappingDefinitionId,
    Guid ImportBatchId,
    string MappingCode,
    string TargetEntityName,
    bool PreviewOnly,
    int RequestedRows,
    int ProcessedRows,
    int MappedRows,
    int SkippedRows,
    int FailedRows,
    IReadOnlyCollection<MappingExecutionRowResult> Rows);

public sealed record MappingExecutionRowResult(
    Guid StagingRecordId,
    int RowNumber,
    string Status,
    Guid? CanonicalEntityId,
    string? CanonicalEntityName,
    string? Message);
