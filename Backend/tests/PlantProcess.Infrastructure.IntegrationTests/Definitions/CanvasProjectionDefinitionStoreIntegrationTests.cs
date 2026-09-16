using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Definitions.Canvas;
using PlantProcess.Application.Relationships;
using PlantProcess.Infrastructure.Canonical;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Definitions.Canvas;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

[Collection("CanonicalDefinitionStore")]
public sealed class CanvasProjectionDefinitionStoreIntegrationTests : IAsyncLifetime
{
    private const string Target = "QualityEvent";
    private const string Prefix = "t090test_projection_";
    private const string GraphJson = """{"name":"projection-integration","targetEntity":"QualityEvent","tables":["t0"],"joins":[],"filters":[]}""";
    private readonly DefinitionStoreFixture _fixture;
    private CanvasProjectionTestFixture? _projection;

    public CanvasProjectionDefinitionStoreIntegrationTests(DefinitionStoreFixture fixture) => _fixture = fixture;
    private CanvasProjectionTestFixture Projection => _projection ?? throw new InvalidOperationException("Projection fixture is not initialised.");

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _projection = await CanvasProjectionTestFixture.CreateAsync(_fixture, Target);
    }

    public async Task DisposeAsync()
    {
        if (_projection is not null) await _projection.DisposeAsync();
        await _fixture.ResetAsync();
    }

    private CanvasDefinitionLifecycleService Service()
    {
        var db = _fixture.NewContext();
        return new CanvasDefinitionLifecycleService(
            db,
            new CanonicalDefinitionWriter(db),
            new CanvasCompatibilityProjection(),
            new CanonicalEntityCatalog(db),
            new CanvasStagingSchema("ppiq_staging"),
            new NoopRelationshipPublicationService());
    }

    private static string Code(string suffix) => Prefix + suffix;

    // This suite closes projection-declaration / definition-store integration.
    // Relationship publication is a separate producer concern and is not invoked by
    // these save/reopen gates. Supplying a real store/service here would broaden the
    // test and could make relationship side effects part of T-262 by accident. The
    // lifecycle constructor nevertheless requires the production seam, so an inert
    // implementation satisfies composition without becoming a second relationship
    // authority or mutating relationship rows.
    private sealed class NoopRelationshipPublicationService : IRelationshipPublicationService
    {
        public Task<ApplicationResult<IReadOnlyList<RelationshipDto>>> PublishAsync(
            RelationshipPublicationRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "T-262 projection save/reopen proof unexpectedly invoked relationship publication.");

        public Task<ApplicationResult<int>> RetireByDefinitionAsync(
            Guid sourceDefinitionId,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "T-262 projection save/reopen proof unexpectedly invoked relationship retirement.");
    }

    [Fact]
    public async Task Live_graph_and_sql_declarations_round_trip_through_the_definition_store()
    {
        var graph = await Service().SaveGraphAsync(
            new CanvasGraphSave(_fixture.TenantId, _fixture.OwnerId, Code("roundtrip_graph"), "graph", GraphJson, Target, Projection.GraphDeclarationJson),
            CancellationToken.None);
        Assert.True(graph.IsSuccess, graph.Error?.Message);
        AssertJsonEquivalent(Projection.GraphDeclarationJson, graph.Value!.Representation.ProjectionJson);

        var sqlText = Projection.SqlFor("SELECT 1 AS quantity");
        var sql = await Service().SaveSqlAsync(
            new CanvasSqlSave(_fixture.TenantId, _fixture.OwnerId, Code("roundtrip_sql"), "sql", null, sqlText, null, Target, Projection.SqlDeclarationJson),
            CancellationToken.None);
        Assert.True(sql.IsSuccess, sql.Error?.Message);
        AssertJsonEquivalent(Projection.SqlDeclarationJson, sql.Value!.Representation.ProjectionJson);

        await AssertDetailMatchesAsync(graph.Value.VersionId, Projection.GraphDeclarationJson);
        await AssertDetailMatchesAsync(sql.Value.VersionId, Projection.SqlDeclarationJson);
    }

    [Fact]
    public async Task Changing_only_a_binding_changes_the_hash_and_old_version_reopens_unchanged()
    {
        var service = Service();
        var code = Code("binding_version");
        var first = await service.SaveGraphAsync(
            new CanvasGraphSave(_fixture.TenantId, _fixture.OwnerId, code, "binding", GraphJson, Target, Projection.GraphDeclarationJson),
            CancellationToken.None);
        Assert.True(first.IsSuccess, first.Error?.Message);

        var second = await service.SaveGraphAsync(
            new CanvasGraphSave(_fixture.TenantId, _fixture.OwnerId, code, "binding", GraphJson, Target, Projection.AlternateGraphDeclarationJson),
            CancellationToken.None);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(1, first.Value!.VersionNumber);
        Assert.Equal(2, second.Value!.VersionNumber);
        Assert.NotEqual(first.Value.DefinitionHash, second.Value.DefinitionHash);

        var reopen1 = await service.ReopenAsync(_fixture.TenantId, code, 1, CancellationToken.None);
        var reopen2 = await service.ReopenAsync(_fixture.TenantId, code, 2, CancellationToken.None);
        Assert.True(reopen1.IsSuccess, reopen1.Error?.Message);
        Assert.True(reopen2.IsSuccess, reopen2.Error?.Message);
        AssertJsonEquivalent(Projection.GraphDeclarationJson, reopen1.Value!.Representation.ProjectionJson);
        AssertJsonEquivalent(Projection.AlternateGraphDeclarationJson, reopen2.Value!.Representation.ProjectionJson);
    }

    [Fact]
    public async Task Missing_declaration_is_a_typed_refusal_and_writes_no_definition()
    {
        var suffix = "missing";
        var result = await Service().SaveGraphAsync(
            new CanvasGraphSave(_fixture.TenantId, _fixture.OwnerId, Code(suffix), "missing", GraphJson, Target, null),
            CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Contains("declares no projection", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await _fixture.CountDefinitionsForCodeAsync("projection_" + suffix));
    }

    [Fact]
    public async Task Legacy_version_reopens_without_fabricated_projection_and_cannot_be_resaved_without_upgrade()
    {
        var code = Code("legacy");
        await using (var db = _fixture.NewContext())
        {
            var writer = new CanonicalDefinitionWriter(db);
            await using var tx = await db.Database.BeginTransactionAsync();
            var write = new CanonicalDefinitionWrite(
                DefinitionKind.Transformation,
                _fixture.TenantId,
                _fixture.OwnerId,
                code,
                "legacy",
                CanvasDefinitionContent.ForGraph(GraphJson, Target),
                CanonicalVersionStatus.Draft,
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["projection_mode"] = "declared",
                    ["target_entities"] = "[]",
                });
            var written = await writer.WriteVersionAsync(write, CancellationToken.None);
            Assert.True(written.IsSuccess, written.Error?.Message);
            await tx.CommitAsync();
        }

        var service = Service();
        var reopened = await service.ReopenAsync(_fixture.TenantId, code, 1, CancellationToken.None);
        Assert.True(reopened.IsSuccess, reopened.Error?.Message);
        Assert.Null(reopened.Value!.Representation.ProjectionJson);

        var unsafelyUnchanged = await service.SaveGraphAsync(
            new CanvasGraphSave(_fixture.TenantId, _fixture.OwnerId, code, "legacy", GraphJson, Target, null),
            CancellationToken.None);
        Assert.True(unsafelyUnchanged.IsFailure);
        Assert.Contains("declares no projection", unsafelyUnchanged.Error!.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { 1 }, await _fixture.VersionNumbersForCodeAsync("projection_legacy"));
    }

    private static void AssertJsonEquivalent(string expectedJson, string? actualJson)
    {
        Assert.False(string.IsNullOrWhiteSpace(actualJson), "stored projection JSON is absent");
        var expected = System.Text.Json.Nodes.JsonNode.Parse(expectedJson);
        var actual = System.Text.Json.Nodes.JsonNode.Parse(actualJson!);
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(expected, actual),
            "projection declaration changed semantically across definition-store round trip");
    }

    private async Task AssertDetailMatchesAsync(Guid versionId, string declarationJson)
    {
        await using var db = _fixture.NewContext();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT projection_mode, target_entities::text FROM ppiq_meta.transformation_details WHERE definition_version_id=@id;",
            connection);
        cmd.Parameters.AddWithValue("id", versionId);
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("declared", reader.GetString(0));
        var expected = CanvasProjectionDeclaration.ToDetailProjection(declarationJson, Target);
        var expectedNode = System.Text.Json.Nodes.JsonNode.Parse(expected);
        var actualNode = System.Text.Json.Nodes.JsonNode.Parse(reader.GetString(1));
        Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(expectedNode, actualNode),
            "transformation_details.target_entities disagrees with the hashed projection declaration");
    }
}