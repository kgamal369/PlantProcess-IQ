using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Security.Tenancy;

namespace PlantProcess.Application.Relationships;

/// <summary>
/// T-057. The relationship model's read side and publication seam.
///
/// The service holds the product semantics; the store holds rows. Everything
/// that decides what a relationship MEANS lives here, so replacing the storage
/// in T-095 cannot change a single rule.
/// </summary>
public sealed class RelationshipService : IRelationshipService, IRelationshipPublicationService
{
    private readonly IRelationshipStore _store;
    private readonly ITenantAccessor _tenantAccessor;

    public RelationshipService(IRelationshipStore store, ITenantAccessor tenantAccessor)
    {
        _store = store;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<ApplicationResult<IReadOnlyList<RelationshipDto>>> GetPublishedAsync(
        string? entity, CancellationToken cancellationToken)
    {
        if (!_tenantAccessor.TryGetTenantId(out var tenantId))
            return ApplicationResult<IReadOnlyList<RelationshipDto>>.Failure(
                ApplicationError.Validation("No tenant on the caller; the relationship model is tenant-scoped."));

        var normalised = string.IsNullOrWhiteSpace(entity) ? null : entity.Trim();
        var rows = await _store.ReadPublishedAsync(tenantId, normalised, cancellationToken);
        return ApplicationResult<IReadOnlyList<RelationshipDto>>.Success(rows);
    }

    public async Task<ApplicationResult<RelationshipDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!_tenantAccessor.TryGetTenantId(out var tenantId))
            return ApplicationResult<RelationshipDto>.Failure(
                ApplicationError.Validation("No tenant on the caller; the relationship model is tenant-scoped."));

        var row = await _store.ReadByIdAsync(tenantId, id, cancellationToken);

        // A retired relationship is not missing - it is deactivated so a finding
        // computed under it stays explainable - but it is not part of the model a
        // consumer may traverse, so the consumer-facing answer is the same.
        return row is null
            ? ApplicationResult<RelationshipDto>.Failure(
                ApplicationError.NotFound("No published relationship with that identity."))
            : ApplicationResult<RelationshipDto>.Success(row);
    }

    public async Task<ApplicationResult<IReadOnlyList<RelationshipEntityDto>>> GetEntitiesAsync(
        CancellationToken cancellationToken)
    {
        var published = await GetPublishedAsync(null, cancellationToken);
        if (published.IsFailure)
            return ApplicationResult<IReadOnlyList<RelationshipEntityDto>>.Failure(published.Error!);

        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var relationship in published.Value!)
        {
            foreach (var side in new[] { relationship.LeftEntity, relationship.RightEntity })
                counts[side] = counts.TryGetValue(side, out var seen) ? seen + 1 : 1;
        }

        IReadOnlyList<RelationshipEntityDto> entities = counts
            .Select(pair => new RelationshipEntityDto(pair.Key, pair.Value))
            .ToList();

        return ApplicationResult<IReadOnlyList<RelationshipEntityDto>>.Success(entities);
    }

    public async Task<ApplicationResult<IReadOnlyList<RelationshipDto>>> PublishAsync(
        RelationshipPublicationRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
            return Fail("A publication carries at least one declaration.");

        if (!_tenantAccessor.TryGetTenantId(out var tenantId))
            return Fail("No tenant on the caller; a relationship cannot be published without one.");

        if (request.SourceDefinitionId == Guid.Empty)
            return Fail("A relationship must name the definition that published it.");

        if (request.SourceDefinitionVersion <= 0)
            return Fail("A relationship must name the definition VERSION that published it, so a historical result stays explainable after the model changes.");

        if (request.Relationships is null || request.Relationships.Count == 0)
            return Fail("A publication with no declarations is not a publication.");

        foreach (var declaration in request.Relationships)
        {
            var refusal = Validate(declaration);
            if (refusal is not null) return Fail(refusal);
        }

        var effectiveFromUtc = DateTime.UtcNow;

        // Republication supersedes rather than deletes. The prior emission is
        // retired first so the unique code is free, and its rows stay readable.
        await _store.RetireByDefinitionAsync(tenantId, request.SourceDefinitionId, effectiveFromUtc, cancellationToken);

        foreach (var declaration in request.Relationships)
        {
            await _store.UpsertAsync(
                tenantId, declaration, request.SourceDefinitionId, request.SourceDefinitionVersion,
                effectiveFromUtc, cancellationToken);
        }

        // Returned by reading BACK, never by echoing the request. An emission
        // that cannot be read is not a publication, and echoing would hide that.
        var published = await _store.ReadPublishedAsync(tenantId, null, cancellationToken);
        var emitted = published
            .Where(r => r.SourceDefinitionId == request.SourceDefinitionId
                        && r.SourceDefinitionVersion == request.SourceDefinitionVersion)
            .ToList();

        return ApplicationResult<IReadOnlyList<RelationshipDto>>.Success(emitted);
    }

    public async Task<ApplicationResult<int>> RetireByDefinitionAsync(
        Guid sourceDefinitionId, CancellationToken cancellationToken)
    {
        if (!_tenantAccessor.TryGetTenantId(out var tenantId))
            return ApplicationResult<int>.Failure(
                ApplicationError.Validation("No tenant on the caller."));

        var retired = await _store.RetireByDefinitionAsync(
            tenantId, sourceDefinitionId, DateTime.UtcNow, cancellationToken);

        return ApplicationResult<int>.Success(retired);
    }

    /// <summary>
    /// Publication-time rules. Each refusal names its code and says what is
    /// wrong in a sentence, because the person reading it is a plant engineer
    /// looking at their own declaration, not a developer reading a stack trace.
    /// </summary>
    private static string? Validate(RelationshipDeclaration d)
    {
        if (d is null) return "A null declaration cannot be published.";
        if (string.IsNullOrWhiteSpace(d.RelationshipCode)) return "A relationship needs a code.";
        if (string.IsNullOrWhiteSpace(d.LeftEntity) || string.IsNullOrWhiteSpace(d.RightEntity))
            return $"{RelationshipPublicationCodes.UnknownVocabulary}: a relationship needs an entity on both sides.";
        if (string.IsNullOrWhiteSpace(d.GrainLeft) || string.IsNullOrWhiteSpace(d.GrainRight))
            return $"{RelationshipPublicationCodes.UnknownVocabulary}: a relationship needs a declared grain on both sides, so a cross-grain join is recognised as one.";

        if (!RelationshipJoinTypes.All.Contains(d.JoinType, StringComparer.Ordinal))
            return $"{RelationshipPublicationCodes.UnknownVocabulary}: '{d.JoinType}' is not a join type.";
        if (!RelationshipCardinalities.All.Contains(d.Cardinality, StringComparer.Ordinal))
            return $"{RelationshipPublicationCodes.UnknownVocabulary}: '{d.Cardinality}' is not a cardinality.";

        if (d.AttributionRule is not null
            && !RelationshipAttributionRules.All.Contains(d.AttributionRule, StringComparer.Ordinal))
            return $"{RelationshipPublicationCodes.UnknownVocabulary}: '{d.AttributionRule}' is not an attribution rule.";

        var convertsGrain = !string.Equals(d.GrainLeft, d.GrainRight, StringComparison.Ordinal);
        if (convertsGrain
            && (d.AttributionRule is null
                || string.Equals(d.AttributionRule, RelationshipAttributionRules.None, StringComparison.Ordinal)))
        {
            return $"{RelationshipPublicationCodes.GrainConversionWithoutAttribution}: this relationship converts grain from '{d.GrainLeft}' to '{d.GrainRight}' and must declare how a parent's value is divided across children.";
        }

        if (d.Members is null || d.Members.Count == 0)
            return $"{RelationshipPublicationCodes.MembersOutOfOrderOrIncomplete}: a relationship needs at least one key pair.";

        var orders = d.Members.Select(m => m.MemberOrder).OrderBy(o => o).ToArray();
        for (var i = 0; i < orders.Length; i++)
        {
            if (orders[i] != i)
                return $"{RelationshipPublicationCodes.MembersOutOfOrderOrIncomplete}: composite key members must be contiguous from 0; got [{string.Join(", ", orders)}]. Order matters because real plants key on two or three columns.";
        }

        foreach (var member in d.Members)
        {
            if (string.IsNullOrWhiteSpace(member.LeftColumn) || string.IsNullOrWhiteSpace(member.RightColumn))
                return $"{RelationshipPublicationCodes.MembersOutOfOrderOrIncomplete}: key member {member.MemberOrder} is missing a column on one side.";
        }

        return null;
    }

    private static ApplicationResult<IReadOnlyList<RelationshipDto>> Fail(string message) =>
        ApplicationResult<IReadOnlyList<RelationshipDto>>.Failure(ApplicationError.Validation(message));
}