using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Definitions.Canvas;

/// <summary>
/// PPIQ T-244. THE ONE LIFECYCLE FOR CANVAS-AUTHORED DEFINITIONS.
///
/// Graph authoring and authored SQL converge here onto the canonical
/// definition authority. Before this service, graph publish wrote
/// ppiq_visual_mapper_versions keyed by session and SQL save wrote
/// ppiq_mapping_versions keyed by mapping code - two stores with two identity
/// semantics and no canonical version behind either. The comment in the SQL
/// endpoint recorded that T-039 deferred exactly this convergence to M2a.
///
/// AUTHORITY. The canonical write is the lifecycle event. The legacy rows the
/// execution path still reads are a compatibility projection written inside
/// the same transaction and subordinate to it. A user-visible success means
/// the canonical version AND its projection committed together; there is no
/// reachable state in which one exists without the other.
///
/// IDENTITY. DefinitionCode is the tenant-scoped, version-independent handle
/// that survives reload. session_id and mapping_code remain projection keys.
/// </summary>
public interface ICanvasDefinitionLifecycle
{
    /// <summary>
    /// Saves a graph-authored definition as a canonical draft version. Under
    /// the writer's semantic-hash law, an unchanged graph reuses the existing
    /// version rather than creating one.
    /// </summary>
    Task<ApplicationResult<CanvasDefinitionVersion>> SaveGraphAsync(
        CanvasGraphSave save,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves an authored-SQL definition as a canonical draft version. The SQL
    /// is validated by the server safe-SQL authority BEFORE anything is
    /// written; an unsafe statement leaves no canonical or projection trace.
    /// </summary>
    Task<ApplicationResult<CanvasDefinitionVersion>> SaveSqlAsync(
        CanvasSqlSave save,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes an existing canonical version through the writer's publish
    /// semantics and writes the serving projection in the same transaction.
    /// </summary>
    Task<ApplicationResult<CanvasDefinitionVersion>> PublishAsync(
        Guid tenantId,
        string definitionCode,
        int versionNumber,
        CanvasProjectionHandles handles,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reopens a definition by its tenant-scoped code: the canonical identity,
    /// the requested (or latest) version, and its representation. Read-only;
    /// opening a screen never mutates the store.
    /// </summary>
    Task<ApplicationResult<CanvasDefinitionVersion>> ReopenAsync(
        Guid tenantId,
        string definitionCode,
        int? versionNumber,
        CancellationToken cancellationToken);
}

/// <summary>
/// T-253. OutputTarget is the governed canonical entity name this definition writes to.
/// It is nullable ON THE WIRE so a caller that omits it is refused BY NAME with a typed
/// code, rather than rejected by a model binder before the refusal can be worded.
/// </summary>
public sealed record CanvasGraphSave(
    Guid TenantId,
    Guid OwnerId,
    string DefinitionCode,
    string DisplayName,
    string GraphJson,
    string? OutputTarget);

/// <summary>
/// The legacy keys the execution projection still needs at publish time.
/// They are handles for the projection, never canonical identity, and they
/// are not part of the hashed content.
/// </summary>
public sealed record CanvasProjectionHandles(
    Guid? SessionId,
    string? DisplayName,
    string? CanonicalEntity,
    string? PublishedBy);

/// <summary>
/// T-253. CanonicalEntity remains ONLY the legacy projection handle it always was.
/// OutputTarget is the canonical identity, and the two are not interchangeable: the
/// handle is not hashed, does not survive a version and is not returned by reopen.
/// </summary>
public sealed record CanvasSqlSave(
    Guid TenantId,
    Guid OwnerId,
    string DefinitionCode,
    string DisplayName,
    string? CanonicalEntity,
    string Sql,
    string? ForkedFromGraphJson,
    string? OutputTarget);

/// <summary>
/// The canonical identity a Canvas caller receives. DefinitionId is
/// definition_store.id. Representation is read from the stored content, so a
/// reopened SQL definition is reported as SQL and never as a fabricated graph.
/// </summary>
public sealed record CanvasDefinitionVersion(
    Guid DefinitionId,
    Guid VersionId,
    string DefinitionCode,
    int VersionNumber,
    string Status,
    string DefinitionHash,
    CanvasDefinitionRepresentation Representation);
