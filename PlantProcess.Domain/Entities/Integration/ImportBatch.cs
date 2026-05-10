using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

public class ImportBatch : BaseEntity
{
    public Guid SourceSystemDefinitionId { get; private set; }

    public string ImportBatchCode { get; private set; } = null!;

    public string ImportType { get; private set; } = null!;
    // Examples: CSV, Excel, ReadOnlySql, ApiSnapshot, HistorianExport, SyntheticSeed

    public string Status { get; private set; } = "Created";
    // Created, Running, Completed, Failed, Cancelled

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public string? SourceObjectName { get; private set; }

    public string? FileName { get; private set; }

    public string? Checksum { get; private set; }

    public int? RowCount { get; private set; }

    public string? ErrorMessage { get; private set; }

    private ImportBatch()
    {
    }

    public ImportBatch(
        Guid sourceSystemDefinitionId,
        string importBatchCode,
        string importType,
        bool isSynthetic,
        string? sourceObjectName = null,
        string? fileName = null,
        string? checksum = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (sourceSystemDefinitionId == Guid.Empty)
            throw new ArgumentException("Source system definition ID is required.", nameof(sourceSystemDefinitionId));

        if (string.IsNullOrWhiteSpace(importBatchCode))
            throw new ArgumentException("Import batch code is required.", nameof(importBatchCode));

        if (string.IsNullOrWhiteSpace(importType))
            throw new ArgumentException("Import type is required.", nameof(importType));

        SourceSystemDefinitionId = sourceSystemDefinitionId;
        ImportBatchCode = importBatchCode.Trim();
        ImportType = importType.Trim();
        SourceObjectName = sourceObjectName?.Trim();
        FileName = fileName?.Trim();
        Checksum = checksum?.Trim();

        StartedAtUtc = DateTime.UtcNow;
        Status = "Created";

        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    public void MarkRunning()
    {
        Status = "Running";
        MarkAsUpdated();
    }

    public void MarkCompleted(int rowCount)
    {
        Status = "Completed";
        RowCount = rowCount;
        CompletedAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }

    public void MarkFailed(string errorMessage)
    {
        Status = "Failed";
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "Unknown import error."
            : errorMessage.Trim();

        CompletedAtUtc = DateTime.UtcNow;
        MarkAsUpdated();
    }
}