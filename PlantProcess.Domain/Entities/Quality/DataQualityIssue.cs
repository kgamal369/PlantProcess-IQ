using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Quality;

public class DataQualityIssue : BaseEntity
{
    public Guid? MaterialUnitId { get; private set; }

    public string IssueType { get; private set; } = null!;
    // MissingGenealogy, DuplicateRecord, TimestampGap, MissingEquipmentId, InvalidValue

    public string Severity { get; private set; } = "Warning";
    // Info, Warning, Error, Critical

    public string Description { get; private set; } = null!;

    public string? AffectedEntityName { get; private set; }

    public Guid? AffectedEntityId { get; private set; }

    private DataQualityIssue()
    {
    }

    public DataQualityIssue(
        string issueType,
        string description,
        bool isSynthetic,
        Guid? materialUnitId = null,
        string severity = "Warning",
        string? affectedEntityName = null,
        Guid? affectedEntityId = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(issueType))
            throw new ArgumentException("Issue type is required.", nameof(issueType));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        IssueType = issueType.Trim();
        Description = description.Trim();
        MaterialUnitId = materialUnitId;
        Severity = string.IsNullOrWhiteSpace(severity) ? "Warning" : severity.Trim();
        AffectedEntityName = affectedEntityName?.Trim();
        AffectedEntityId = affectedEntityId;
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }
}