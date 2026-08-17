using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Relationships;

/// <summary>
/// T-057. The read side of the plant relationship model - the only way any of
/// the product's consumers may see a join.
///
/// A capability that re-derives a join instead of reading this is a defect.
/// </summary>
public interface IRelationshipService
{
    /// <summary>Unretired relationships, optionally narrowed to one entity on either side.</summary>
    Task<ApplicationResult<IReadOnlyList<RelationshipDto>>> GetPublishedAsync(
        string? entity, CancellationToken cancellationToken);

    /// <summary>One relationship with its ordered members. Retired relationships are not returned.</summary>
    Task<ApplicationResult<RelationshipDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Entities that currently participate in at least one unretired relationship.</summary>
    Task<ApplicationResult<IReadOnlyList<RelationshipEntityDto>>> GetEntitiesAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// T-057. The publication seam.
///
/// This is NOT an authoring API and is deliberately not reachable over HTTP.
/// Publishing a transformation definition emits relationships; that is the only
/// way a relationship comes into existence. In M1 the seam is called by the
/// publication path directly; in M2a the same seam is called by DF4 publish.
/// The signature does not change either way.
/// </summary>
public interface IRelationshipPublicationService
{
    /// <summary>
    /// Emits the relationships a definition version declares. Republishing the
    /// same definition retires what the previous version emitted and emits the
    /// new set: a superseded relationship is DEACTIVATED, never deleted, so a
    /// finding computed under it stays explainable.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<RelationshipDto>>> PublishAsync(
        RelationshipPublicationRequest request, CancellationToken cancellationToken);

    /// <summary>Retires everything a definition emitted. Returns how many were retired.</summary>
    Task<ApplicationResult<int>> RetireByDefinitionAsync(
        Guid sourceDefinitionId, CancellationToken cancellationToken);
}

/// <summary>
/// T-057. The persistence port.
///
/// M1 satisfies this with compatibility persistence whose physical shape is an
/// implementation detail and is named NOWHERE outside the adapter that owns it.
/// T-095 replaces the adapter with the canonical ppiq_meta tables. Nothing above
/// this interface - service, endpoints, tests, consumers - changes when it does.
/// That is the whole point of the seam, and the reason no test in this task may
/// contain a table name.
/// </summary>
public interface IRelationshipStore
{
    Task<Guid> UpsertAsync(
        Guid tenantId,
        RelationshipDeclaration declaration,
        Guid sourceDefinitionId,
        int sourceDefinitionVersion,
        DateTime effectiveFromUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RelationshipDto>> ReadPublishedAsync(
        Guid tenantId, string? entity, CancellationToken cancellationToken);

    Task<RelationshipDto?> ReadByIdAsync(
        Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<int> RetireByDefinitionAsync(
        Guid tenantId, Guid sourceDefinitionId, DateTime retiredAtUtc, CancellationToken cancellationToken);
}