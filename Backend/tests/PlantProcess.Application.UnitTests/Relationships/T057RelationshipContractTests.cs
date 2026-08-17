using PlantProcess.Application.Relationships;
using PlantProcess.Application.Security.Tenancy;
using Xunit;

namespace PlantProcess.Application.UnitTests.Relationships;

/// <summary>
/// T-057. The relationship contract, proven through the service only.
///
/// There is no table name anywhere in this file, and that is a requirement
/// rather than a style choice: when T-095 replaces the M1 compatibility storage
/// with the canonical ppiq_meta tables, every assertion here must still hold
/// without being edited. A test that knows where the rows live is a test that
/// has to be rewritten when they move.
/// </summary>
public sealed class T057RelationshipContractTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class FixedTenant : ITenantAccessor
    {
        private readonly Guid? _tenantId;
        public FixedTenant(Guid? tenantId) => _tenantId = tenantId;
        public Guid TenantId => _tenantId ?? throw new TenantResolutionException();
        public bool TryGetTenantId(out Guid tenantId)
        {
            tenantId = _tenantId ?? Guid.Empty;
            return _tenantId.HasValue;
        }
    }

    /// <summary>An in-memory stand-in for the persistence port, storing what a real store stores and nothing about how.</summary>
    private sealed class InMemoryStore : IRelationshipStore
    {
        private readonly List<RelationshipDto> _rows = new();

        public Task<Guid> UpsertAsync(Guid tenantId, RelationshipDeclaration d, Guid defId, int defVersion,
            DateTime effectiveFromUtc, CancellationToken ct)
        {
            var id = Guid.NewGuid();
            _rows.Add(new RelationshipDto(
                id, d.RelationshipCode, d.LeftEntity, d.RightEntity, d.JoinType, d.Cardinality,
                d.GrainLeft, d.GrainRight,
                !string.Equals(d.GrainLeft, d.GrainRight, StringComparison.Ordinal),
                d.AttributionRule, d.AttributionExpression, d.IsPreferredPath,
                RelationshipAmbiguityStates.Unambiguous, RelationshipValidationStates.Unproven,
                defId, defVersion, effectiveFromUtc, null,
                d.Members.OrderBy(m => m.MemberOrder).ToList()));
            return Task.FromResult(id);
        }

        public Task<IReadOnlyList<RelationshipDto>> ReadPublishedAsync(Guid tenantId, string? entity, CancellationToken ct)
        {
            IReadOnlyList<RelationshipDto> live = _rows
                .Where(r => r.RetiredAtUtc is null)
                .Where(r => entity is null || r.LeftEntity == entity || r.RightEntity == entity)
                .OrderBy(r => r.RelationshipCode, StringComparer.Ordinal)
                .ToList();
            return Task.FromResult(live);
        }

        public Task<RelationshipDto?> ReadByIdAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(_rows.FirstOrDefault(r => r.Id == id && r.RetiredAtUtc is null));

        public Task<int> RetireByDefinitionAsync(Guid tenantId, Guid defId, DateTime retiredAtUtc, CancellationToken ct)
        {
            var count = 0;
            for (var i = 0; i < _rows.Count; i++)
            {
                if (_rows[i].SourceDefinitionId != defId || _rows[i].RetiredAtUtc is not null) continue;
                _rows[i] = _rows[i] with { RetiredAtUtc = retiredAtUtc };
                count++;
            }
            return Task.FromResult(count);
        }
    }

    private static RelationshipService Service(Guid? tenant = null) =>
        new(new InMemoryStore(), new FixedTenant(tenant ?? Tenant));

    private static RelationshipDeclaration SameGrain(string code = "REL_A") => new(
        code, "entity_left", "entity_right",
        RelationshipJoinTypes.Inner, RelationshipCardinalities.OneToMany,
        "unit", "unit", null, null, false,
        new List<RelationshipMemberDto> { new("left_key", "right_key", 0) });

    private static RelationshipPublicationRequest Publication(
        Guid definitionId, int version, params RelationshipDeclaration[] declarations) =>
        new(definitionId, version, declarations.ToList());

    // ---- publication -------------------------------------------------------

    [Fact]
    public async Task Publishing_emits_a_relationship_that_can_be_read_back()
    {
        var service = Service();
        var definitionId = Guid.NewGuid();

        var published = await service.PublishAsync(Publication(definitionId, 1, SameGrain()), CancellationToken.None);

        Assert.True(published.IsSuccess);
        Assert.Single(published.Value!);

        var readBack = await service.GetPublishedAsync(null, CancellationToken.None);
        Assert.True(readBack.IsSuccess);
        Assert.Single(readBack.Value!);
        Assert.Equal("REL_A", readBack.Value![0].RelationshipCode);
    }

    [Fact]
    public async Task A_published_relationship_names_the_definition_version_that_emitted_it()
    {
        var service = Service();
        var definitionId = Guid.NewGuid();

        var published = await service.PublishAsync(Publication(definitionId, 7, SameGrain()), CancellationToken.None);

        Assert.Equal(definitionId, published.Value![0].SourceDefinitionId);
        Assert.Equal(7, published.Value![0].SourceDefinitionVersion);
    }

    [Fact]
    public async Task Ordered_composite_key_members_survive_publication()
    {
        var service = Service();
        var declaration = SameGrain() with
        {
            Members = new List<RelationshipMemberDto>
            {
                new("heat_id", "heat_ref", 1),
                new("plant_id", "plant_ref", 0)
            }
        };

        var published = await service.PublishAsync(
            Publication(Guid.NewGuid(), 1, declaration), CancellationToken.None);

        Assert.True(published.IsSuccess);
        var members = published.Value![0].Members;
        Assert.Equal(2, members.Count);
        Assert.Equal(0, members[0].MemberOrder);
        Assert.Equal("plant_id", members[0].LeftColumn);
        Assert.Equal(1, members[1].MemberOrder);
        Assert.Equal("heat_id", members[1].LeftColumn);
    }

    [Fact]
    public async Task Cardinality_and_join_type_survive_publication()
    {
        var service = Service();
        var declaration = SameGrain() with
        {
            JoinType = RelationshipJoinTypes.Left,
            Cardinality = RelationshipCardinalities.ManyToMany
        };

        var published = await service.PublishAsync(
            Publication(Guid.NewGuid(), 1, declaration), CancellationToken.None);

        Assert.Equal(RelationshipJoinTypes.Left, published.Value![0].JoinType);
        Assert.Equal(RelationshipCardinalities.ManyToMany, published.Value![0].Cardinality);
    }

    [Fact]
    public async Task A_newly_published_relationship_is_unproven_and_does_not_claim_otherwise()
    {
        var service = Service();
        var published = await service.PublishAsync(
            Publication(Guid.NewGuid(), 1, SameGrain()), CancellationToken.None);

        Assert.Equal(RelationshipValidationStates.Unproven, published.Value![0].ValidationState);
        Assert.Equal(RelationshipAmbiguityStates.Unambiguous, published.Value![0].AmbiguityState);
    }

    // ---- refusals ----------------------------------------------------------

    [Fact]
    public async Task Grain_conversion_without_an_attribution_rule_is_refused()
    {
        var service = Service();
        var declaration = SameGrain() with { GrainLeft = "heat", GrainRight = "coil" };

        var published = await service.PublishAsync(
            Publication(Guid.NewGuid(), 1, declaration), CancellationToken.None);

        Assert.True(published.IsFailure);
        Assert.Contains(RelationshipPublicationCodes.GrainConversionWithoutAttribution, published.Error!.Message);
    }

    [Fact]
    public async Task Grain_conversion_with_an_attribution_rule_is_accepted_and_flagged_as_converting()
    {
        var service = Service();
        var declaration = SameGrain() with
        {
            GrainLeft = "heat",
            GrainRight = "coil",
            AttributionRule = RelationshipAttributionRules.Weighted,
            AttributionExpression = "mass_fraction"
        };

        var published = await service.PublishAsync(
            Publication(Guid.NewGuid(), 1, declaration), CancellationToken.None);

        Assert.True(published.IsSuccess);
        Assert.True(published.Value![0].IsGrainConverting);
    }

    [Fact]
    public async Task Key_members_that_are_not_contiguous_from_zero_are_refused()
    {
        var service = Service();
        var declaration = SameGrain() with
        {
            Members = new List<RelationshipMemberDto>
            {
                new("a", "b", 0),
                new("c", "d", 2)
            }
        };

        var published = await service.PublishAsync(
            Publication(Guid.NewGuid(), 1, declaration), CancellationToken.None);

        Assert.True(published.IsFailure);
        Assert.Contains(RelationshipPublicationCodes.MembersOutOfOrderOrIncomplete, published.Error!.Message);
    }

    [Fact]
    public async Task A_relationship_with_no_key_members_is_refused()
    {
        var service = Service();
        var declaration = SameGrain() with { Members = new List<RelationshipMemberDto>() };

        var published = await service.PublishAsync(
            Publication(Guid.NewGuid(), 1, declaration), CancellationToken.None);

        Assert.True(published.IsFailure);
    }

    [Fact]
    public async Task An_unknown_cardinality_is_refused_rather_than_stored()
    {
        var service = Service();
        var declaration = SameGrain() with { Cardinality = "one-to-lots" };

        var published = await service.PublishAsync(
            Publication(Guid.NewGuid(), 1, declaration), CancellationToken.None);

        Assert.True(published.IsFailure);
        Assert.Contains(RelationshipPublicationCodes.UnknownVocabulary, published.Error!.Message);
    }

    [Fact]
    public async Task A_publication_that_does_not_name_its_definition_version_is_refused()
    {
        var service = Service();

        var published = await service.PublishAsync(
            Publication(Guid.NewGuid(), 0, SameGrain()), CancellationToken.None);

        Assert.True(published.IsFailure);
    }

    [Fact]
    public async Task A_caller_with_no_tenant_cannot_publish_or_read()
    {
        var service = new RelationshipService(new InMemoryStore(), new FixedTenant(null));

        Assert.True((await service.PublishAsync(
            Publication(Guid.NewGuid(), 1, SameGrain()), CancellationToken.None)).IsFailure);
        Assert.True((await service.GetPublishedAsync(null, CancellationToken.None)).IsFailure);
    }

    // ---- retirement and consumer visibility --------------------------------

    [Fact]
    public async Task A_retired_relationship_is_not_returned_to_consumers()
    {
        var service = Service();
        var definitionId = Guid.NewGuid();

        await service.PublishAsync(Publication(definitionId, 1, SameGrain()), CancellationToken.None);
        var retired = await service.RetireByDefinitionAsync(definitionId, CancellationToken.None);

        Assert.True(retired.IsSuccess);
        Assert.Equal(1, retired.Value);

        var readBack = await service.GetPublishedAsync(null, CancellationToken.None);
        Assert.True(readBack.IsSuccess);
        Assert.Empty(readBack.Value!);
    }

    [Fact]
    public async Task A_retired_relationship_cannot_be_fetched_by_identity_either()
    {
        var service = Service();
        var definitionId = Guid.NewGuid();

        var published = await service.PublishAsync(Publication(definitionId, 1, SameGrain()), CancellationToken.None);
        var id = published.Value![0].Id;

        Assert.True((await service.GetByIdAsync(id, CancellationToken.None)).IsSuccess);

        await service.RetireByDefinitionAsync(definitionId, CancellationToken.None);

        Assert.True((await service.GetByIdAsync(id, CancellationToken.None)).IsFailure);
    }

    [Fact]
    public async Task Republishing_a_definition_supersedes_its_previous_emission_rather_than_duplicating_it()
    {
        var service = Service();
        var definitionId = Guid.NewGuid();

        await service.PublishAsync(Publication(definitionId, 1, SameGrain()), CancellationToken.None);
        var second = await service.PublishAsync(Publication(definitionId, 2, SameGrain()), CancellationToken.None);

        Assert.True(second.IsSuccess);

        var live = await service.GetPublishedAsync(null, CancellationToken.None);
        Assert.Single(live.Value!);
        Assert.Equal(2, live.Value![0].SourceDefinitionVersion);
    }

    // ---- read filters ------------------------------------------------------

    [Fact]
    public async Task The_model_can_be_narrowed_to_one_entity_on_either_side()
    {
        var service = Service();
        var a = SameGrain("REL_A");
        var b = SameGrain("REL_B") with { LeftEntity = "other_left", RightEntity = "other_right" };

        await service.PublishAsync(Publication(Guid.NewGuid(), 1, a, b), CancellationToken.None);

        var narrowed = await service.GetPublishedAsync("entity_right", CancellationToken.None);
        Assert.Single(narrowed.Value!);
        Assert.Equal("REL_A", narrowed.Value![0].RelationshipCode);
    }

    [Fact]
    public async Task Entities_report_how_many_relationships_touch_them()
    {
        var service = Service();
        await service.PublishAsync(Publication(Guid.NewGuid(), 1, SameGrain()), CancellationToken.None);

        var entities = await service.GetEntitiesAsync(CancellationToken.None);

        Assert.True(entities.IsSuccess);
        Assert.Equal(2, entities.Value!.Count);
        Assert.All(entities.Value!, e => Assert.Equal(1, e.RelationshipCount));
    }

    // ---- frozen vocabulary -------------------------------------------------

    [Fact]
    public void The_refusal_catalogue_is_the_products_and_not_a_reduced_second_one()
    {
        Assert.Equal("RL01", RelationshipRefusalCodes.AmbiguousPath);
        Assert.Equal("RL02", RelationshipRefusalCodes.UnprovenRelationship);
        Assert.Equal("RL03", RelationshipRefusalCodes.NoPath);
        Assert.Equal("RL04", RelationshipRefusalCodes.RetirementBlocked);
    }

    [Fact]
    public void Exploration_is_manual_and_every_other_purpose_is_automated()
    {
        // This is the RL02 boundary: an unproven relationship may be explored by
        // hand and may not be trained on. T-058 enforces it; T-057 freezes it.
        Assert.False(RelationshipConsumerPurposes.IsAutomated(RelationshipConsumerPurposes.Explore));
        Assert.True(RelationshipConsumerPurposes.IsAutomated(RelationshipConsumerPurposes.ModelTraining));
        Assert.True(RelationshipConsumerPurposes.IsAutomated(RelationshipConsumerPurposes.Correlation));
        Assert.Contains(RelationshipConsumerPurposes.Genealogy, RelationshipConsumerPurposes.All);
    }
}