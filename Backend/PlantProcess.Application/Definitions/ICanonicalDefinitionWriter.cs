using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Definitions;

/// <summary>
/// PPIQ T-090. THE SINGLE CANONICAL WRITE PATH FOR EVERY DEFINITION PURPOSE.
///
/// WHY THIS EXISTS RATHER THAN CALLERS WRITING THE STORE THEMSELVES.
/// Three writers currently create semantic definitions: the public definition
/// service, the system-template authority that runs at startup, and the Page
/// Builder endpoints. If each writes the canonical store in its own way, the
/// store stops being one authority and becomes three implementations that
/// happen to share tables. Every one of them calls this instead.
///
/// WHY NOT ROUTE THE TEMPLATE PATH THROUGH IDefinitionService.
/// The template authority runs inside a startup hosted service and has no HTTP
/// caller, no ApplicationResult surface to return and no payload JSON. Giving
/// it the full public service would hand it a contract it does not need in
/// order to get persistence it does. One shared internal writer, called by
/// both, is the smaller and more honest arrangement.
///
/// TRANSACTIONAL LAW. Every method here is one logical operation. Canonical
/// identity, immutable version, typed detail and the operational serving row
/// either all land or none do. There is no reachable state in which an
/// operational row shows V2 while the canonical authority holds V1 - that
/// state is precisely the dual authority this task removes.
///
/// PUBLISHED RESOLUTION IS NOT LATEST. definition_store.current_version records
/// the newest version number, which is not the same question as which version
/// is usable. A draft raised the number without becoming truth. Callers that
/// need runtime truth ask ResolvePublishedAsync, which reads status explicitly.
/// </summary>
public interface ICanonicalDefinitionWriter
{
    /// <summary>
    /// Creates or reuses the parent identity for a definition and writes a new
    /// immutable version carrying the supplied semantic content.
    ///
    /// The version is created in the requested status. Callers migrating
    /// incomplete legacy semantics must request Draft: a version that cannot be
    /// completed honestly must not be published, because a published version is
    /// what every downstream gate treats as fact.
    ///
    /// Identical redeclaration is not an error and does not fork a version. The
    /// store contract decides that by semantic hash, so a startup path that
    /// runs on every boot does not accumulate a version per restart. That is
    /// the difference between convergence and accumulation.
    /// </summary>
    Task<ApplicationResult<CanonicalDefinitionVersion>> WriteVersionAsync(
        CanonicalDefinitionWrite write,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes an existing version through the canonical lifecycle. The
    /// database refuses any content change to an already-published version, so
    /// this may only move a draft or validated version forward.
    /// </summary>
    Task<ApplicationResult<CanonicalDefinitionVersion>> PublishAsync(
        Guid definitionId,
        int versionNumber,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves one exact version, whatever its status. This is the historical
    /// lookup: a snapshot taken under V1 must still resolve V1 after V2 exists.
    /// </summary>
    Task<ApplicationResult<CanonicalDefinitionVersion>> ResolveExactAsync(
        Guid definitionId,
        int versionNumber,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the current PUBLISHED version, explicitly by status rather than
    /// by highest version number. A newer draft must not become runtime truth
    /// merely by existing.
    /// </summary>
    Task<ApplicationResult<CanonicalDefinitionVersion>> ResolvePublishedAsync(
        Guid definitionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Withdraws publication from a definition without deleting its history.
    ///
    /// A product widget the declarations no longer name, or a page whose serving
    /// row was soft-deleted, must stop resolving as published runtime truth. Its
    /// versions remain - they are the evidence of what ran while it existed, and
    /// an execution snapshot taken under version 3 must still resolve version 3
    /// afterwards. Only the publication moves, to superseded.
    ///
    /// Idempotent: retiring an already-retired definition succeeds and changes
    /// nothing, because a startup convergence path runs on every boot.
    /// </summary>
    Task<ApplicationResult> RetireAsync(
        Guid definitionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds a definition by its tenant-scoped code, or reports its absence.
    /// Used by writers that own a stable external code - system templates own
    /// widget codes, the Page Builder owns slugs - to converge onto the same
    /// canonical identity across restarts instead of creating a new one.
    /// Read-only, so it does not require the caller's transaction.
    /// </summary>
    Task<ApplicationResult<Guid?>> FindByCodeAsync(
        Guid tenantId,
        string definitionCode,
        CancellationToken cancellationToken);
}

/// <summary>
/// One canonical write. Detail is the typed payload for kinds that have a
/// detail table and is ignored for the payload-only kinds, which the registry
/// identifies rather than the caller guessing.
/// </summary>
public sealed record CanonicalDefinitionWrite(
    DefinitionKind Kind,
    Guid TenantId,
    Guid OwnerId,
    string DefinitionCode,
    string Name,
    string ContentJson,
    CanonicalVersionStatus Status,
    IReadOnlyDictionary<string, object?>? Detail = null,
    IReadOnlyList<CanonicalOutcomeDeclaration>? Outcomes = null);

/// <summary>
/// SM-06 outcome semantics, declared as a child of an S1 transformation
/// version. Several outcomes may share one version - outcome_code is the key
/// within a version, not across the store.
///
/// Every field the frozen contract names is required here and none is
/// nullable-by-convenience. A migration that cannot supply DetectionPositionCode
/// must say migrated_unknown and stay in draft; it may not omit the field and
/// let a default stand in for a leakage anchor.
/// </summary>
public sealed record CanonicalOutcomeDeclaration(
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
/// What a caller gets back. DefinitionId is definition_store.id - the canonical
/// identity, not a translated compatibility id.
/// </summary>
public sealed record CanonicalDefinitionVersion(
    Guid DefinitionId,
    Guid VersionId,
    string DefinitionCode,
    DefinitionKind Kind,
    string Surface,
    int VersionNumber,
    CanonicalVersionStatus Status,
    string ContentJson,
    string DefinitionHash,
    DateTime CreatedAtUtc,
    Guid? CreatedBy);

/// <summary>
/// The version lifecycle as script 831 declares it. Named here so callers do
/// not pass status strings the database would reject at the last moment.
/// </summary>
public enum CanonicalVersionStatus
{
    Draft = 1,
    Validated = 2,
    Published = 3,
    PausedByDrift = 4,
    RolledBack = 5,
    Superseded = 6,
}
