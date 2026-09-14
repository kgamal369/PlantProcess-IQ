namespace PlantProcess.Application.Integration.Contracts.Mapping;

/// <summary>
/// PPIQ T-099. QuarantinedRows is separate from FailedRows on purpose.
///
/// Before T-099 a refused row and a broken system were the same number, so the
/// import services marked a whole batch Failed because one source cell held a
/// malformed date. A completed import that says 100 processed, 97 mapped, 2
/// skipped, 1 quarantined, 0 execution failures is a SUCCESSFUL import with one
/// piece of typed evidence attached.
///
/// FailedRows is now reserved for genuine execution failure that still leaves a
/// meaningful result. Most infrastructure faults fail the operation instead.
/// </summary>
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
    int QuarantinedRows,
    IReadOnlyCollection<MappingExecutionRowResult> Rows);

public sealed record MappingExecutionRowResult(
    Guid StagingRecordId,
    int RowNumber,
    string Status,
    Guid? CanonicalEntityId,
    string? CanonicalEntityName,
    string? Message,
    string? ValidationCode = null,
    string? OffendingValue = null,
    string? SuggestedCorrection = null);
