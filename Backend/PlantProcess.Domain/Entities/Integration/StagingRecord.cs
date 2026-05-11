using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// Raw/staging row retained exactly as received from a source extract.
/// This is the audit and replay layer before mapping into canonical entities.
/// </summary>
public class StagingRecord : BaseEntity
{
    public Guid ImportBatchId { get; private set; }

    public string SourceObjectName { get; private set; } = null!;

    public int RowNumber { get; private set; }

    public string RawJson { get; private set; } = null!;

    public bool IsProcessed { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public string ProcessingStatus { get; private set; } = "Pending";
    // Pending, Mapped, Failed, Skipped

    public string? ProcessingError { get; private set; }

    public Guid? CanonicalEntityId { get; private set; }

    public string? CanonicalEntityName { get; private set; }

    private StagingRecord()
    {
    }

    public StagingRecord(
        Guid importBatchId,
        string sourceObjectName,
        int rowNumber,
        string rawJson,
        bool isSynthetic,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (importBatchId == Guid.Empty)
            throw new ArgumentException("Import batch ID is required.", nameof(importBatchId));

        if (string.IsNullOrWhiteSpace(sourceObjectName))
            throw new ArgumentException("Source object name is required.", nameof(sourceObjectName));

        if (rowNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(rowNumber), "Row number must be greater than zero.");

        if (string.IsNullOrWhiteSpace(rawJson))
            throw new ArgumentException("Raw JSON is required.", nameof(rawJson));

        ImportBatchId = importBatchId;
        SourceObjectName = sourceObjectName.Trim();
        RowNumber = rowNumber;
        RawJson = rawJson.Trim();
        IsProcessed = false;
        ProcessingStatus = "Pending";
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    public void MarkMapped(Guid canonicalEntityId, string canonicalEntityName)
    {
        if (canonicalEntityId == Guid.Empty)
            throw new ArgumentException("Canonical entity ID is required.", nameof(canonicalEntityId));

        if (string.IsNullOrWhiteSpace(canonicalEntityName))
            throw new ArgumentException("Canonical entity name is required.", nameof(canonicalEntityName));

        IsProcessed = true;
        ProcessingStatus = "Mapped";
        ProcessedAtUtc = DateTime.UtcNow;
        ProcessingError = null;
        CanonicalEntityId = canonicalEntityId;
        CanonicalEntityName = canonicalEntityName.Trim();
        MarkAsUpdated();
    }

    public void MarkFailed(string error)
    {
        IsProcessed = true;
        ProcessingStatus = "Failed";
        ProcessedAtUtc = DateTime.UtcNow;
        ProcessingError = string.IsNullOrWhiteSpace(error) ? "Unknown mapping error." : error.Trim();
        CanonicalEntityId = null;
        CanonicalEntityName = null;
        MarkAsUpdated();
    }

    public void MarkSkipped(string reason)
    {
        IsProcessed = true;
        ProcessingStatus = "Skipped";
        ProcessedAtUtc = DateTime.UtcNow;
        ProcessingError = string.IsNullOrWhiteSpace(reason) ? "Skipped." : reason.Trim();
        CanonicalEntityId = null;
        CanonicalEntityName = null;
        MarkAsUpdated();
    }

    public void ResetProcessing()
    {
        IsProcessed = false;
        ProcessingStatus = "Pending";
        ProcessedAtUtc = null;
        ProcessingError = null;
        CanonicalEntityId = null;
        CanonicalEntityName = null;
        MarkAsUpdated();
    }
}
