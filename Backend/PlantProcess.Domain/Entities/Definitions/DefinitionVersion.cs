namespace PlantProcess.Domain.Entities.Definitions;

/// <summary>
/// PPIQ T-090. One immutable version of a definition.
///
/// THIS TYPE MOVED, IT WAS NOT REPLACED. It previously mapped
/// ppiq_meta.ppiq_definition_versions, an M1 compatibility store that carried
/// widget history beside the operational widget row. It now maps the canonical
/// ppiq_meta.definition_versions created by script 831. Keeping the type and
/// changing where it lives is what lets the frozen T-039 validation keep
/// compiling: it holds db.DefinitionVersions and must not learn that storage
/// moved. That unchanged test is the proof the visible contract did not move.
///
/// THERE IS NO SECOND WRITE PATH. This entity is read and cleanup only. Every
/// version is created by CanonicalDefinitionWriter inside the caller's
/// transaction, because a version written through EF change tracking would
/// bypass the parent row lock, the semantic hash and the detail contract - it
/// would be a second version authority wearing a familiar name.
///
/// EXCLUDED FROM MIGRATIONS. Script 831 owns this table. An EF migration that
/// also created it would be a second schema authority for one set of columns.
///
/// NO APPLICATION USING. Domain is the innermost project and references nothing
/// above it. Status is held as its stored literal rather than as the
/// Application-layer enum precisely so this file needs no such reference; an
/// earlier draft carried one and inverted the dependency direction.
/// </summary>
public class DefinitionVersion
{
    private DefinitionVersion()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>The canonical parent identity - definition_store.id.</summary>
    public Guid DefinitionId { get; private set; }

    public int VersionNumber { get; private set; }

    /// <summary>
    /// draft, validated, published, paused_by_drift, rolled_back or superseded,
    /// as script 831 declares. Stored as its literal so the database CHECK and
    /// this type cannot disagree about spelling.
    /// </summary>
    public string Status { get; private set; } = null!;

    public string Mode { get; private set; } = null!;

    /// <summary>The canonicalised semantic payload.</summary>
    public string? GraphJson { get; private set; }

    /// <summary>
    /// Hash of the canonical representation. Equal hashes mean one semantic
    /// declaration, which is how a redeclaration avoids forking a version.
    /// </summary>
    public string DefinitionHash { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTime? PublishedAtUtc { get; private set; }

    public bool IsDeleted { get; private set; }

    /// <summary>
    /// True only for the published version. Runtime truth is resolved by this,
    /// never by definition_store.current_version, which records the newest
    /// version number - a number a draft raises without becoming truth.
    /// </summary>
    public bool IsPublished => string.Equals(Status, "published", StringComparison.Ordinal);
}
