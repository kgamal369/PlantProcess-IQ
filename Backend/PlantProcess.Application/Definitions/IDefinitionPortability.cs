using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Definitions;

/// <summary>
/// PPIQ T-091. PORTABLE DEFINITION EXPORT AND IMPORT.
///
/// WHAT A PORTABLE ARTIFACT IS. One root definition plus exactly the dependency
/// closure needed to reproduce the root's semantics, expressed in semantic
/// identity rather than environment-local identity. It is not a database dump:
/// a dump carries row ids, audit history and secrets, none of which mean
/// anything in another installation.
///
/// WHY SEMANTIC IDENTITY. A definition in a customer database and its twin in
/// a clean database are the same definition when their code, kind, surface,
/// content and typed detail agree, and they are not the same row. Import into
/// a fresh database must not have to reproduce a PostgreSQL uuid in order to
/// preserve meaning. Internal uuids travel as provenance only and are never
/// used to resolve a reference inside the package.
///
/// THE IMPORTER IS AN ORCHESTRATOR. Every semantic write goes through
/// ICanonicalDefinitionWriter. Import does not insert canonical rows with its
/// own SQL merely because that would be shorter: the canonical writer owns
/// validation, semantic hashing and idempotence, and a second write path would
/// be a second authority.
/// </summary>
public interface IDefinitionPortability
{
    /// <summary>
    /// Exports the root definition and its forward requirement closure as a
    /// deterministic, environment-independent artifact.
    /// </summary>
    /// <param name="versionNumber">
    /// The exact immutable version to export. Omitted means the PUBLISHED
    /// version, never the highest draft and never definition_store.current_version:
    /// an artifact whose root was "whatever was newest at export time" is not
    /// reproducible. A definition with no published version and no explicit
    /// version requested is refused rather than guessed.
    /// </param>
    Task<ApplicationResult<DefinitionArtifact>> ExportAsync(
        Guid tenantId,
        Guid definitionId,
        int? versionNumber,
        CancellationToken cancellationToken);

    /// <summary>
    /// Imports one artifact as ONE unit of work. Either every definition,
    /// version, typed detail and dependency edge in the package lands, or none
    /// of it does. A failure at dependency seven of eight leaves no orphan
    /// parent and no half-written graph.
    /// </summary>
    Task<ApplicationResult<DefinitionImportResult>> ImportAsync(
        Guid tenantId,
        Guid ownerId,
        DefinitionArtifact artifact,
        CancellationToken cancellationToken);
}

/// <summary>
/// The portable package.
///
/// FormatVersion versions THIS CONTRACT, not a DefinitionKind. An artifact
/// written by a future release carries a version this build does not know, and
/// the importer refuses it explicitly rather than guessing which half it still
/// understands.
///
/// Metadata is excluded from semantic equality on purpose: an export timestamp
/// and a source environment name are useful provenance and would otherwise
/// make two semantically identical exports compare unequal.
/// </summary>
public sealed record DefinitionArtifact(
    int FormatVersion,
    string RootRef,
    IReadOnlyList<ArtifactDefinition> Definitions,
    IReadOnlyList<ArtifactDependency> Dependencies,
    ArtifactMetadata? Metadata)
{
    public const int CurrentFormatVersion = 1;
}

/// <summary>
/// One definition inside the package.
///
/// Ref is package-local semantic identity - the stable handle every reference
/// in the package resolves through. SourceDefinitionId is provenance from the
/// exporting environment and is never used to resolve anything.
/// </summary>
public sealed record ArtifactDefinition(
    string Ref,
    string DefinitionCode,
    string Kind,
    string Surface,
    string Name,
    int VersionNumber,
    string Status,
    string ContentJson,
    string DefinitionHash,
    IReadOnlyDictionary<string, string?>? Detail,
    IReadOnlyList<ArtifactOutcome>? Outcomes,
    Guid? SourceDefinitionId,
    Guid? SourceVersionId);

public sealed record ArtifactOutcome(
    string OutcomeCode,
    string OutcomeType,
    string? ClassTaxonomyRef,
    string? OrdinalRankMapJson,
    string GrainCode,
    string DetectionPositionCode,
    string DetectionTimestampField,
    string Direction,
    string? UnitCode,
    string CensoringPolicy);

/// <summary>
/// A dependency edge between two package-local refs. DependsOnVersion is the
/// pinned version when the source edge pinned one; import must carry N rather
/// than quietly resolving "latest", because a root that requires version N and
/// receives version N+1 is not the artifact that was exported.
/// </summary>
public sealed record ArtifactDependency(
    string FromRef,
    string ToRef,
    string DependencyKind,
    bool IsRequired,
    int? DependsOnVersion);

/// <summary>Provenance only. Never part of semantic equality or the hash.</summary>
public sealed record ArtifactMetadata(
    DateTime ExportedAtUtc,
    string? SourceEnvironment,
    string? ExportedBy);

public sealed record DefinitionImportResult(
    Guid RootDefinitionId,
    int RootVersionNumber,
    int DefinitionsWritten,
    int DefinitionsReused,
    int DependencyEdgesWritten,
    IReadOnlyList<ImportedDefinitionRef> Imported);

public sealed record ImportedDefinitionRef(
    string Ref,
    Guid DefinitionId,
    string DefinitionCode,
    int VersionNumber,
    bool Reused);

/// <summary>
/// Why an import was refused, in terms the caller can act on. Never "last
/// import wins": an existing definition that disagrees semantically is a
/// conflict to report, not a row to overwrite.
/// </summary>
public sealed record DefinitionImportConflict(
    string Ref,
    string DefinitionCode,
    DefinitionImportConflictReason Reason,
    string? ExistingSemanticIdentity,
    string? IncomingSemanticIdentity,
    string Detail);

public enum DefinitionImportConflictReason
{
    VersionNotExportable = 0,
    UnknownFormatVersion = 1,
    DuplicateArtifactRef = 2,
    MissingReferencedDependency = 3,
    DependencyCycle = 4,
    UnknownKind = 5,
    MalformedDetail = 6,
    KindMismatch = 7,
    SurfaceMismatch = 8,
    SemanticContentConflict = 9,
}
