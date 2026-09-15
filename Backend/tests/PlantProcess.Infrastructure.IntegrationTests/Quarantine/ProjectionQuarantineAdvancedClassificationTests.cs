using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Services.Mapping;
using PlantProcess.Application.Relationships;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Infrastructure.Definitions;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Quarantine;

/// <summary>
/// Seven live fixtures completing the Chapter 3 projection validation taxonomy.
/// Every result is produced by MappingExecutionService -> MappingRowProjector.
/// </summary>
public sealed class ProjectionQuarantineAdvancedClassificationTests
    : IAsyncLifetime
{
    private QuarantineWorld _world = null!;

    public async Task InitializeAsync() =>
        _world = await QuarantineWorld.CreateAsync("reprocess");

    public Task DisposeAsync() =>
        _world.DisposeAsync().AsTask();

    private MappingExecutionService NewService(
        PlantProcess.Infrastructure.Persistence.PlantProcessDbContext db,
        IRelationshipStore? relationshipStore = null)
    {
        var validator = relationshipStore is null
            ? new ProjectionRowValidationService(db)
            : new ProjectionRowValidationService(db, relationshipStore);

        var projector = new MappingRowProjector(db, validator);

        return new MappingExecutionService(
            db,
            projector,
            new CanonicalIdentityResolver(db),
            NullLogger<MappingExecutionService>.Instance);
    }

    private async Task<MaterialUnit> AddMaterialAsync(
        string code,
        string type = "COIL",
        string? grade = null,
        string? family = null)
    {
        await using var db = _world.NewContext();

        var material =
            new MaterialUnit(
                code,
                type,
                _world.SiteId,
                family,
                grade,
                isSynthetic: true);

        db.MaterialUnits.Add(material);
        await db.SaveChangesAsync();

        return material;
    }

    private async Task<MappingExecutionResult> ExecuteGenealogyAsync(
        string code,
        string mappingJson,
        string[] rows,
        IRelationshipStore? relationshipStore = null)
    {
        var source = code.ToLowerInvariant() + "_rows";

        var created = await _world.NewMappingAsync(
            code,
            source,
            "GenealogyEdge",
            mappingJson);

        await _world.AddRowsAsync(
            created.BatchId,
            source,
            rows);

        await using var db = _world.NewContext();

        var result = await NewService(db, relationshipStore)
            .ExecuteAsync(
                created.MappingId,
                created.BatchId,
                50,
                false,
                CancellationToken.None);

        Assert.False(result.IsFailure, result.Error?.Message);
        return result.Value!;
    }

    [Fact]
    public async Task PV09_numeric_value_outside_the_governed_range()
    {
        var material = await AddMaterialAsync("PV09M");

        await using (var seed = _world.NewContext())
        {
            var parameter = await seed.ParameterDefinitions
                .SingleAsync(
                    x => x.Id == _world.ParameterDefinitionId);

            parameter.SetExpectedRange(0m, 100m);
            await seed.SaveChangesAsync();
        }

        var created = await _world.NewMappingAsync(
            "T100PV09",
            "pv09_rows",
            "ParameterObservation",
            "{\"MaterialUnitId\":\"material\",\"ParameterDefinitionId\":\"parameter\",\"ObservedAtUtc\":\"observed\",\"NumericValue\":\"value\",\"UnitOfMeasure\":\"unit\"}");

        await _world.AddRowsAsync(
            created.BatchId,
            "pv09_rows",
            $"{{\"material\":\"{material.Id}\",\"parameter\":\"{_world.ParameterDefinitionId}\",\"observed\":\"2026-09-15T00:00:00Z\",\"value\":\"999\",\"unit\":\"degC\"}}");

        await using var db = _world.NewContext();

        var result = await NewService(db)
            .ExecuteAsync(
                created.MappingId,
                created.BatchId,
                50,
                false,
                CancellationToken.None);

        Assert.False(result.IsFailure, result.Error?.Message);

        var row = Assert.Single(result.Value!.Rows);
        Assert.Equal("Quarantined", row.Status);
        Assert.Equal("PV09", row.ValidationCode);
    }

    [Fact]
    public async Task PV10_declared_relationship_cardinality_is_violated()
    {
        var parent1 = await AddMaterialAsync("PV10P1");
        var parent2 = await AddMaterialAsync("PV10P2");
        var child = await AddMaterialAsync("PV10C");

        var relationship = Relationship(
            "REL10",
            RelationshipCardinalities.OneToMany);

        var store = new FixedRelationshipStore(relationship);

        var result = await ExecuteGenealogyAsync(
            "T100PV10",
            "{\"ParentMaterialUnitId\":\"parent\",\"ChildMaterialUnitId\":\"child\",\"RelationshipType\":\"type\",\"RelationshipCode\":\"relationship\",\"ContributionWeight\":\"weight\"}",
            new[]
            {
                $"{{\"parent\":\"{parent1.Id}\",\"child\":\"{child.Id}\",\"type\":\"ProducedInto\",\"relationship\":\"REL10\",\"weight\":\"0.50\"}}",
                $"{{\"parent\":\"{parent2.Id}\",\"child\":\"{child.Id}\",\"type\":\"ProducedInto\",\"relationship\":\"REL10\",\"weight\":\"0.50\"}}"
            },
            store);

        Assert.Equal(2, result.QuarantinedRows);
        Assert.All(
            result.Rows,
            row => Assert.Equal("PV10", row.ValidationCode));
    }

    [Fact]
    public async Task PV11_edge_grain_does_not_match_the_declared_relationship()
    {
        var parent = await AddMaterialAsync("PV11P", "SLAB");
        var child = await AddMaterialAsync("PV11C", "COIL");

        var relationship = Relationship(
            "REL11",
            RelationshipCardinalities.ManyToMany,
            "HEAT",
            "COIL");

        var result = await ExecuteGenealogyAsync(
            "T100PV11",
            "{\"ParentMaterialUnitId\":\"parent\",\"ChildMaterialUnitId\":\"child\",\"RelationshipType\":\"type\",\"RelationshipCode\":\"relationship\"}",
            new[]
            {
                $"{{\"parent\":\"{parent.Id}\",\"child\":\"{child.Id}\",\"type\":\"ProducedInto\",\"relationship\":\"REL11\"}}"
            },
            new FixedRelationshipStore(relationship));

        Assert.Equal(
            "PV11",
            Assert.Single(result.Rows).ValidationCode);
    }

    [Fact]
    public async Task PV12_genealogy_edge_would_create_a_cycle()
    {
        var a = await AddMaterialAsync("PV12A");
        var b = await AddMaterialAsync("PV12B");

        await using (var seed = _world.NewContext())
        {
            seed.GenealogyEdges.Add(
                new PlantProcess.Domain.Entities.Materials.GenealogyEdge(
                    a.Id,
                    b.Id,
                    "ProducedInto",
                    isSynthetic: true));

            await seed.SaveChangesAsync();
        }

        var result = await ExecuteGenealogyAsync(
            "T100PV12",
            "{\"ParentMaterialUnitId\":\"parent\",\"ChildMaterialUnitId\":\"child\",\"RelationshipType\":\"type\"}",
            new[]
            {
                $"{{\"parent\":\"{b.Id}\",\"child\":\"{a.Id}\",\"type\":\"ProducedInto\"}}"
            });

        Assert.Equal(
            "PV12",
            Assert.Single(result.Rows).ValidationCode);
    }

    [Fact]
    public async Task PV13_child_attribution_weights_do_not_sum_to_one()
    {
        var parent1 = await AddMaterialAsync("PV13P1");
        var parent2 = await AddMaterialAsync("PV13P2");
        var child = await AddMaterialAsync("PV13C");

        var result = await ExecuteGenealogyAsync(
            "T100PV13",
            "{\"ParentMaterialUnitId\":\"parent\",\"ChildMaterialUnitId\":\"child\",\"RelationshipType\":\"type\",\"ContributionWeight\":\"weight\",\"IsTransition\":\"transition\"}",
            new[]
            {
                $"{{\"parent\":\"{parent1.Id}\",\"child\":\"{child.Id}\",\"type\":\"SplitInto\",\"weight\":\"0.60\",\"transition\":\"true\"}}",
                $"{{\"parent\":\"{parent2.Id}\",\"child\":\"{child.Id}\",\"type\":\"SplitInto\",\"weight\":\"0.20\",\"transition\":\"true\"}}"
            });

        Assert.Equal(2, result.QuarantinedRows);

        Assert.All(
            result.Rows,
            row => Assert.Equal("PV13", row.ValidationCode));
    }

    [Fact]
    public async Task Balanced_transition_weights_are_not_misclassified_as_PV13()
    {
        var parent1 = await AddMaterialAsync("PV13OKP1");
        var parent2 = await AddMaterialAsync("PV13OKP2");
        var child = await AddMaterialAsync("PV13OKC");

        var result = await ExecuteGenealogyAsync(
            "T100PV13OK",
            "{\"ParentMaterialUnitId\":\"parent\",\"ChildMaterialUnitId\":\"child\",\"RelationshipType\":\"type\",\"ContributionWeight\":\"weight\",\"IsTransition\":\"transition\"}",
            new[]
            {
                $"{{\"parent\":\"{parent1.Id}\",\"child\":\"{child.Id}\",\"type\":\"SplitInto\",\"weight\":\"0.60\",\"transition\":\"true\"}}",
                $"{{\"parent\":\"{parent2.Id}\",\"child\":\"{child.Id}\",\"type\":\"SplitInto\",\"weight\":\"0.40\",\"transition\":\"true\"}}"
            });

        Assert.Equal(0, result.QuarantinedRows);
        Assert.Equal(2, result.MappedRows);
    }

    [Fact]
    public async Task PV14_projection_relationship_is_ambiguous_without_a_preferred_path()
    {
        var parent = await AddMaterialAsync("PV14P");
        var child = await AddMaterialAsync("PV14C");

        var first = Relationship(
            "REL14A",
            RelationshipCardinalities.ManyToMany);

        var second = Relationship(
            "REL14B",
            RelationshipCardinalities.ManyToMany);

        var store = new FixedRelationshipStore(first, second);

        var result = await ExecuteGenealogyAsync(
            "T100PV14",
            "{\"ParentMaterialUnitId\":\"parent\",\"ChildMaterialUnitId\":\"child\",\"RelationshipType\":\"type\",\"RelationshipCode\":\"relationship\"}",
            new[]
            {
                $"{{\"parent\":\"{parent.Id}\",\"child\":\"{child.Id}\",\"type\":\"ProducedInto\",\"relationship\":\"REL14A\"}}"
            },
            store);

        Assert.Equal(
            "PV14",
            Assert.Single(result.Rows).ValidationCode);
    }

    [Fact]
    public async Task PV15_cross_batch_reference_is_retryable_after_late_arrival()
    {
        await AddMaterialAsync("PV15P");

        var created = await _world.NewMappingAsync(
            "T100PV15",
            "pv15_rows",
            "GenealogyEdge",
            "{\"ParentMaterialCode\":\"parent\",\"ChildMaterialCode\":\"child\",\"RelationshipType\":\"type\"}");

        await _world.AddRowsAsync(
            created.BatchId,
            "pv15_rows",
            "{\"parent\":\"PV15P\",\"child\":\"PV15LATE\",\"type\":\"ProducedInto\"}");

        Guid quarantineId;

        await using (var db = _world.NewContext())
        {
            var first = await _world.NewExecutionService(db)
                .ExecuteAsync(
                    created.MappingId,
                    created.BatchId,
                    50,
                    false,
                    CancellationToken.None);

            Assert.False(first.IsFailure, first.Error?.Message);

            Assert.Equal(
                "PV15",
                Assert.Single(first.Value!.Rows).ValidationCode);

            quarantineId = await db.ProjectionQuarantineRecords
                .Where(x =>
                    x.MappingDefinitionId == created.MappingId)
                .Select(x => x.Id)
                .SingleAsync();
        }

        await _world.AddMaterialAsync("PV15LATE");

        await using (var db = _world.NewContext())
        {
            var retried = await _world.NewReprocessService(db)
                .ReprocessAsync(
                    quarantineId,
                    QuarantineWorld.TenantACode,
                    CancellationToken.None);

            Assert.False(retried.IsFailure, retried.Error?.Message);
            Assert.Equal("Resolved", retried.Value!.State);
        }
    }

    private static RelationshipDto Relationship(
        string code,
        string cardinality,
        string leftGrain = "COIL",
        string rightGrain = "COIL") =>
        new(
            Guid.NewGuid(),
            code,
            "ParentMaterial",
            "ChildMaterial",
            RelationshipJoinTypes.Inner,
            cardinality,
            leftGrain,
            rightGrain,
            !leftGrain.Equals(
                rightGrain,
                StringComparison.OrdinalIgnoreCase),
            null,
            null,
            false,
            RelationshipAmbiguityStates.Unambiguous,
            RelationshipValidationStates.Validated,
            Guid.NewGuid(),
            1,
            DateTime.UtcNow,
            null,
            new[]
            {
                new RelationshipMemberDto(
                    "Id",
                    "Id",
                    0)
            });

    private sealed class FixedRelationshipStore : IRelationshipStore
    {
        private readonly IReadOnlyList<RelationshipDto> _relationships;

        public FixedRelationshipStore(
            params RelationshipDto[] relationships)
        {
            _relationships = relationships;
        }

        public Task<Guid> UpsertAsync(
            Guid tenantId,
            RelationshipDeclaration declaration,
            Guid sourceDefinitionId,
            int sourceDefinitionVersion,
            DateTime effectiveFromUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RelationshipDto>> ReadPublishedAsync(
            Guid tenantId,
            string? entity,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<RelationshipDto> rows =
                string.IsNullOrWhiteSpace(entity)
                    ? _relationships
                    : _relationships
                        .Where(x =>
                            x.LeftEntity == entity ||
                            x.RightEntity == entity)
                        .ToArray();

            return Task.FromResult(rows);
        }

        public Task<RelationshipDto?> ReadByIdAsync(
            Guid tenantId,
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                _relationships.SingleOrDefault(x => x.Id == id));

        public Task<int> RetireByDefinitionAsync(
            Guid tenantId,
            Guid sourceDefinitionId,
            DateTime retiredAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> RecordValidationAsync(
            Guid tenantId,
            Guid id,
            string validationState,
            string validationDetailJson,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }
}