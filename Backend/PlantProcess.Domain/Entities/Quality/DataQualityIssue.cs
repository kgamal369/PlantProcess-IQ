using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Quality;

public class DataQualityIssue : BaseEntity
{
    private static readonly HashSet<string> AllowedSeveritiesSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "Info",
        "Warning",
        "Error",
        "Critical"
    };

    public static IReadOnlyCollection<string> AllowedSeverities => AllowedSeveritiesSet.ToArray();

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
        Severity = NormalizeSeverity(severity);
        AffectedEntityName = affectedEntityName?.Trim();
        AffectedEntityId = affectedEntityId;
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    private static string NormalizeSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
            return "Warning";

        var trimmed = severity.Trim();
        if (!AllowedSeveritiesSet.Contains(trimmed))
            throw new ArgumentException(
                $"Invalid data-quality severity '{severity}'. Allowed values: {string.Join(", ", AllowedSeveritiesSet.OrderBy(x => x))}.",
                nameof(severity));

        return AllowedSeveritiesSet.First(x => x.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
