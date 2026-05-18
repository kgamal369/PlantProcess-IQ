using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Integration.Contracts.Commands;

/// <summary>
/// Runs one end-to-end import workflow:
/// source/import batch -> staging records -> mapping execution -> data-quality scan -> import batch status.
///
/// Two modes are supported:
/// 1) Existing batch mode: pass ImportBatchId and leave Rows empty to process already inserted staging rows.
/// 2) Inline rows mode: pass SourceSystemDefinitionId + MappingDefinitionId + Rows; the service creates the batch and staging rows.
/// </summary>
public sealed record RunImportWorkflowCommand(
    Guid? ImportBatchId,
    Guid SourceSystemDefinitionId,
    Guid MappingDefinitionId,
    string? ImportBatchCode,
    string ImportType,
    string SourceObjectName,
    string? FileName,
    string? Checksum,
    IReadOnlyCollection<RunImportWorkflowRawRow> Rows,
    int MappingTake,
    bool StopOnFirstError,
    bool RunDataQualityScan,
    int DataQualityMaxCandidatesPerRule,
    CommandMetadata Metadata);

public sealed record RunImportWorkflowRawRow(
    int RowNumber,
    string RawJson,
    string? SourceRecordId);




