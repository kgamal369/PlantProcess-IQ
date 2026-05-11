namespace PlantProcess.Application.Contracts.Integration;

public sealed record StagingRecordBulkCreateResult(
    Guid ImportBatchId,
    string SourceObjectName,
    int Accepted,
    int Rejected,
    IReadOnlyCollection<StagingRecordBulkRejectedRow> RejectedRows);

public sealed record StagingRecordBulkRejectedRow(
    int RowNumber,
    string Reason);
