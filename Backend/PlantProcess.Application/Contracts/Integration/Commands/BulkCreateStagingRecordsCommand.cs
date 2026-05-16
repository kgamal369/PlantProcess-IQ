using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Integration.Commands;

public sealed record BulkCreateStagingRecordsCommand(
    Guid ImportBatchId,
    string SourceObjectName,
    IReadOnlyCollection<CreateStagingRecordRow> Rows,
    CommandMetadata Metadata);

public sealed record CreateStagingRecordRow(
    int RowNumber,
    string RawJson,
    string? SourceRecordId = null);
