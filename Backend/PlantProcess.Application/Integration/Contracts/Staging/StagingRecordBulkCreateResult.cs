namespace PlantProcess.Application.Integration.Contracts.Staging;

public sealed record StagingRecordBulkCreateResult(
    Guid ImportBatchId,
    string SourceObjectName,
    int Accepted,
    int Rejected,
    IReadOnlyCollection<StagingRecordBulkRejectedRow> RejectedRows);

public sealed record StagingRecordBulkRejectedRow(
    int RowNumber,
    string Reason);




