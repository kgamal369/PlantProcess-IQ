using System.Text.Json;
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
public sealed class RelationshipService : IRelationshipService, IRelationshipPublicationService, IRelationshipValidationService
{
    private static readonly JsonSerializerOptions DetailJson = new(JsonSerializerDefaults.Web);

    private readonly IRelationshipStore _store;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly IRelationshipValidationEvidenceReader? _evidence;

    public RelationshipService(
        IRelationshipStore store,
        ITenantAccessor tenantAccessor,
        IRelationshipValidationEvidenceReader? evidence = null)
    {
        _store = store;
        _tenantAccessor = tenantAccessor;
        _evidence = evidence;
    }

    /// <summary>
    /// Derives validation state from real evidence and persists it. The states mean
    /// exactly what the design says they mean:
    ///
    ///   validated  - populated on both sides, rows matched, declared cardinality not
    ///                contradicted by any observed fan-out;
    ///   failed     - populated on both sides and either nothing matched, or the
    ///                observed fan-out contradicts the declared cardinality;
    ///   unproven   - a side is empty, so there is nothing to prove against; the detail
    ///                says which.
    ///
    /// An evidence read that throws is NOT persisted as anything. A relationship that
    /// could not be evaluated is unevaluated, and writing 'failed' for it would turn an
    /// infrastructure fault into a statement about the plant's data.
    /// </summary>
    public async Task<ApplicationResult<RelationshipValidationResultDto>> ValidateAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!_tenantAccessor.TryGetTenantId(out var tenantId))
            return ApplicationResult<RelationshipValidationResultDto>.Failure(
                ApplicationError.Validation("No tenant on the caller; the relationship model is tenant-scoped."));

        if (_evidence is null)
            return ApplicationResult<RelationshipValidationResultDto>.Failure(
                ApplicationError.Validation("This composition carries no validation evidence reader. That is a configuration defect, not a property of the relationship."));

        var relationship = await _store.ReadByIdAsync(tenantId, id, cancellationToken);
        if (relationship is null)
            return ApplicationResult<RelationshipValidationResultDto>.Failure(
                ApplicationError.NotFound("No published relationship with that identity."));

        var evidence = await _evidence.ReadAsync(relationship, cancellationToken);

        string state;
        string reason;
        if (evidence.LeftPopulation == 0 || evidence.RightPopulation == 0)
        {
            state = RelationshipValidationStates.Unproven;
            reason = "Insufficient evidence: '" +
                     (evidence.LeftPopulation == 0 ? relationship.LeftEntity : relationship.RightEntity) +
                     "' has no rows, so the declared members cannot be exercised.";
        }
        else if (evidence.LeftMatched == 0 && evidence.RightMatched == 0)
        {
            state = RelationshipValidationStates.Failed;
            reason = "Both sides are populated and the declared members match no rows.";
        }
        else if (evidence.CardinalityContradicted)
        {
            state = RelationshipValidationStates.Failed;
            reason = "Declared cardinality '" + evidence.DeclaredCardinality + "' is contradicted by observed '" +
                     evidence.ObservedCardinality + "'.";
        }
        else
        {
            state = RelationshipValidationStates.Validated;
            reason = "Declared members matched real rows and the observed cardinality '" +
                     evidence.ObservedCardinality + "' does not contradict the declaration.";
        }

        var detail = JsonSerializer.Serialize(new { state, reason, evidence }, DetailJson);

        var recorded = await _store.RecordValidationAsync(tenantId, id, state, detail, cancellationToken);
        if (!recorded)
            return ApplicationResult<RelationshipValidationResultDto>.Failure(
                ApplicationError.NotFound("The relationship was retired before its validation could be recorded."));

        return ApplicationResult<RelationshipValidationResultDto>.Success(
            new RelationshipValidationResultDto(relationship.Id, relationship.RelationshipCode, state, reason, evidence));
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

            // The canonical executor runs equality members. A comparison it cannot run
            // must not be publishable: a relationship the product accepts and its only
            // executor then refuses is a promise the product cannot keep.
            if (!string.IsNullOrWhiteSpace(member.Comparison)
                && !string.Equals(member.Comparison.Trim(), "=", StringComparison.Ordinal))
                return $"{RelationshipPublicationCodes.UnknownVocabulary}: key member {member.MemberOrder} compares with '{member.Comparison}'. The canonical relationship key contract is equality.";
        }

        return null;
    }

    private static ApplicationResult<IReadOnlyList<RelationshipDto>> Fail(string message) =>
        ApplicationResult<IReadOnlyList<RelationshipDto>>.Failure(ApplicationError.Validation(message));
}
