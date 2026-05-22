public interface IDeltaImportExecutionService
{
    Task<DeltaImportSummary> ExecuteAllAsync(
        int maxDatasetsPerRun,
        int maxRowsPerDataset,
        CancellationToken cancellationToken);
}

public sealed record DeltaImportSummary
{
    public int DatasetsProcessed { get; set; }
    public int TotalRowsImported { get; set; }
    public int DatasetsFailedCount { get; set; }
    public List<DeltaDatasetResult> DatasetResults { get; set; } = new();
}

public sealed record DeltaDatasetResult(
    string DatasetId,
    string DatasetCode,
    int RowsImported,
    string? ErrorMessage);
