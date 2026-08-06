namespace PlantProcess.Domain.Entities.Definitions;

/// <summary>
/// PPIQ T-039. ONE IMMUTABLE VERSION OF ONE DEFINITION.
///
/// Immutable is the whole point and it is enforced by what this class does NOT
/// expose: there is no setter and no update method for the payload, the kind,
/// the identity or the number. The only thing that can change after the row is
/// written is which version is the published one, because that is a statement
/// about the definition rather than about this version's content.
///
/// The kind is stored as text rather than an integer so that a person reading
/// the table can see what a row is without a lookup, and so that renumbering
/// the enum could never silently reinterpret existing history.
/// </summary>
public class DefinitionVersion
{
    public Guid Id { get; private set; }

    public string DefinitionKind { get; private set; } = null!;

    public Guid DefinitionId { get; private set; }

    public int VersionNumber { get; private set; }

    public string PayloadJson { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public string? CreatedBy { get; private set; }

    public bool IsPublished { get; private set; }

    private DefinitionVersion()
    {
    }

    public DefinitionVersion(
        string definitionKind,
        Guid definitionId,
        int versionNumber,
        string payloadJson,
        string? createdBy)
    {
        if (string.IsNullOrWhiteSpace(definitionKind))
            throw new ArgumentException("Definition kind is required.", nameof(definitionKind));
        if (definitionId == Guid.Empty)
            throw new ArgumentException("Definition ID is required.", nameof(definitionId));
        if (versionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be greater than zero.");
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("Payload is required.", nameof(payloadJson));

        Id = Guid.NewGuid();
        DefinitionKind = definitionKind.Trim();
        DefinitionId = definitionId;
        VersionNumber = versionNumber;
        PayloadJson = payloadJson;
        CreatedAtUtc = DateTime.UtcNow;
        CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy.Trim();
        IsPublished = false;
    }

    /// <summary>The one permitted change: this version becomes the published one.</summary>
    public void MarkPublished(bool published)
    {
        IsPublished = published;
    }
}