using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Npgsql;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Definitions.Canvas;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Definitions.Canvas;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

/// <summary>
/// PPIQ T-244 lifecycle gates.
///
/// Every gate reads the database to decide. Asserting that a call returned a
/// failure proves the caller was told; only counting rows proves that nothing
/// was written. Codes carry the fixture's test prefix so the collection reset
/// removes exactly what these tests create and nothing else.
///
/// THE TRANSACTION GATES ARE THE POINT OF THIS FILE. F1 and F2 force a failure
/// on each side of the unit of work and prove the other side did not survive.
/// F9 proves mechanically - by type, not by prose - that the projection can
/// only execute on the transaction's own connection.
/// </summary>
[Collection("CanonicalDefinitionStore")]
public sealed class CanvasDefinitionLifecycleTests : IAsyncLifetime
{
    // The fixture owns the "t090test_" namespace and applies it itself inside
    // its query helpers. This file therefore keeps TWO forms: the full code the
    // service is given, and the fixture-relative suffix its helpers expect.
    // Passing an already-prefixed code to a helper that prefixes again matches
    // nothing and reads as "the definition was never written".
    private const string FixturePrefix = "t090test_";
    private const string Prefix = FixturePrefix + "canvas_";

    private readonly DefinitionStoreFixture _fixture;

    public CanvasDefinitionLifecycleTests(DefinitionStoreFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        await ClearProjectionsAsync();
    }

    public Task DisposeAsync() => ClearProjectionsAsync();

    // ------------------------------------------------------------- fixtures

    private static string Code(string suffix) => Prefix + suffix;

    // T-253. These declared targetEntity "" - the ABSENCE of a target, which the
    // pre-T-253 shell never had to fill in because it hardcoded one. They now name a
    // real governed projection target, because a target is required to save at all.
    private const string TestTarget = "QualityEvent";

    private const string GraphA = """{"name":"a","targetEntity":"QualityEvent","tables":["t0"],"joins":[],"filters":[{"table":"t0","column":"quantity","op":">","value":"10"}]}""";
    private const string GraphAReordered = """{"tables":["t0"],"filters":[{"value":"10","op":">","column":"quantity","table":"t0"}],"joins":[],"targetEntity":"QualityEvent","name":"a"}""";
    private const string GraphB = """{"name":"a","targetEntity":"QualityEvent","tables":["t0"],"joins":[],"filters":[{"table":"t0","column":"quantity","op":">","value":"20"}]}""";

    private sealed class NullRelationshipPublication : PlantProcess.Application.Relationships.IRelationshipPublicationService
    {
        public static readonly NullRelationshipPublication Instance = new();

        public Task<PlantProcess.Application.Common.Results.ApplicationResult<IReadOnlyList<PlantProcess.Application.Relationships.RelationshipDto>>> PublishAsync(
            PlantProcess.Application.Relationships.RelationshipPublicationRequest request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException(
                "This suite publishes declaration-free versions. A call here means a join was promoted into a relationship.");
        }

        public Task<PlantProcess.Application.Common.Results.ApplicationResult<int>> RetireByDefinitionAsync(
            Guid sourceDefinitionId, CancellationToken cancellationToken)
        {
            return Task.FromResult(PlantProcess.Application.Common.Results.ApplicationResult<int>.Success(0));
        }
    }
    private CanvasDefinitionLifecycleService Service(ICanvasCompatibilityProjection? projection = null, PlantProcess.Application.Relationships.IRelationshipPublicationService? relationships = null)
    {
        var db = _fixture.NewContext();
        // T-262. The staged schema arrives as a fact, so this suite constructs the
        // service without a configuration host - which is the whole reason the service
        // does not read a key itself.
        return new CanvasDefinitionLifecycleService(
            db,
            new CanonicalDefinitionWriter(db),
            projection ?? new CanvasCompatibilityProjection(),
            new PlantProcess.Infrastructure.Canonical.CanonicalEntityCatalog(db),
            new PlantProcess.Application.Definitions.Canvas.CanvasStagingSchema("ppiq_staging"),
            relationships ?? NullRelationshipPublication.Instance);
    }

    private CanvasGraphSave Graph(string suffix, string graph, string? target = TestTarget) =>
        new(_fixture.TenantId, _fixture.OwnerId, Code(suffix), "Canvas " + suffix, graph, target);

    private CanvasSqlSave Sql(string suffix, string sql, string? forked = null, string? target = TestTarget) =>
        new(_fixture.TenantId, _fixture.OwnerId, Code(suffix), "SQL " + suffix, null, sql, forked, target);

    private static CanvasProjectionHandles GraphHandles(Guid sessionId) => new(sessionId, null, null, "test");

    /// <summary>
    /// ppiq_visual_mapper_versions references ppiq_visual_mapper_sessions, so a
    /// projection needs a real session. Created under the test prefix; the
    /// reset deletes sessions and the FK cascades to their versions.
    /// </summary>
    private async Task<Guid> NewSessionAsync(string suffix)
    {
        await using var db = _fixture.NewContext();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO ppiq_meta.ppiq_visual_mapper_sessions (tenant_id, source_code, display_name, source_kind, status) " +
            "VALUES (@t, @c, @c, 'generic_relational', 'draft') RETURNING id;", connection);
        command.Parameters.AddWithValue("t", _fixture.TenantId);
        command.Parameters.AddWithValue("c", Code(suffix) + "_" + Guid.NewGuid().ToString("N")[..8]);
        return (Guid)(await command.ExecuteScalarAsync())!;
    }
    private static CanvasProjectionHandles SqlHandles() => new(null, "SQL", null, "test");

    // ---------------------------------------------------------- CANONICAL SAVE

    [Fact]
    [Trait("Gate", "CANVAS_GRAPH_SAVE")]
    public async Task A_graph_save_creates_a_canonical_transformation_draft()
    {
        var saved = await Service().SaveGraphAsync(Graph("graph_save", GraphA), CancellationToken.None);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal(1, saved.Value!.VersionNumber);
        Assert.Equal("Draft", saved.Value.Status);
        Assert.Equal(CanvasDefinitionContent.RepresentationGraph, saved.Value.Representation.Representation);

        var stored = await _fixture.ResolveExactAsync(saved.Value.DefinitionId, 1);
        Assert.Equal(DefinitionKind.Transformation, stored.Kind);
        // Read back from jsonb, so PostgreSQL has renormalised the text:
        // ": " separators and its own key order. Assert the VALUE, not my
        // serialiser's byte layout, or this gate tests the database formatter.
        using var storedDocument = JsonDocument.Parse(stored.ContentJson);
        Assert.Equal("graph", storedDocument.RootElement.GetProperty("representation").GetString());
        Assert.True(storedDocument.RootElement.TryGetProperty("graph", out _));
    }

    [Fact]
    [Trait("Gate", "CANVAS_SQL_SAVE")]
    public async Task A_sql_save_creates_a_canonical_transformation_draft_with_sql_representation()
    {
        var saved = await Service().SaveSqlAsync(Sql("sql_save", "SELECT 1 AS one"), CancellationToken.None);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal(CanvasDefinitionContent.RepresentationSql, saved.Value!.Representation.Representation);
        Assert.NotNull(saved.Value.Representation.Sql);
        Assert.Null(saved.Value.Representation.GraphJson);
    }

    // ------------------------------------------------------------- F1 / F2

    /// <summary>F1: a canonical refusal leaves no projection row and reports failure.</summary>
    [Fact]
    [Trait("Gate", "F1_CANONICAL_REFUSAL_ROLLBACK")]
    public async Task F1_publish_of_a_version_that_does_not_exist_writes_no_projection()
    {
        var service = Service();
        var saved = await service.SaveGraphAsync(Graph("f1", GraphA), CancellationToken.None);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        var sessionId = await NewSessionAsync("f1");
        var projectionsBefore = await CountGraphProjectionsAsync(sessionId);

        var published = await service.PublishAsync(_fixture.TenantId, Code("f1"), 99, GraphHandles(sessionId), CancellationToken.None);

        Assert.True(published.IsFailure);
        Assert.Equal(projectionsBefore, await CountGraphProjectionsAsync(sessionId));
        Assert.Equal("Draft", (await _fixture.ResolveExactAsync(saved.Value!.DefinitionId, 1)).Status.ToString());
    }

    /// <summary>
    /// F2: the projection fails AFTER the canonical publish succeeded inside
    /// the transaction. The canonical publish must not survive, and published
    /// truth must be unchanged. This is the proof that there is one transaction.
    /// </summary>
    [Fact]
    [Trait("Gate", "F2_PROJECTION_FAILURE_ROLLBACK")]
    public async Task F2_a_projection_failure_rolls_back_the_canonical_publish()
    {
        var saved = await Service().SaveGraphAsync(Graph("f2", GraphA), CancellationToken.None);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        var failing = new FailingProjection();
        var published = await Service(failing).PublishAsync(
            _fixture.TenantId, Code("f2"), 1, GraphHandles(await NewSessionAsync("f2")), CancellationToken.None);

        Assert.True(published.IsFailure);
        Assert.True(failing.WasInvokedInsideTransaction, "the projection was not invoked on an open transaction");

        var afterwards = await _fixture.ResolveExactAsync(saved.Value!.DefinitionId, 1);
        Assert.Equal(CanonicalVersionStatus.Draft, afterwards.Status);

        await using var db = _fixture.NewContext();
        var publishedNow = await new CanonicalDefinitionWriter(db).ResolvePublishedAsync(saved.Value.DefinitionId, CancellationToken.None);
        Assert.True(publishedNow.IsFailure, "a published version exists although the projection failed");
    }

    // ----------------------------------------------------------------- F3

    [Fact]
    [Trait("Gate", "F3_UNSAFE_SQL_REFUSED")]
    public async Task F3_unsafe_sql_is_refused_before_anything_is_written()
    {
        var definitions = await _fixture.CountDefinitionsForCodeAsync("canvas_f3");
        var versions = await _fixture.CountVersionsAsync();

        var saved = await Service().SaveSqlAsync(Sql("f3", "DELETE FROM ppiq_meta.definition_store"), CancellationToken.None);

        Assert.True(saved.IsFailure);
        Assert.Equal(definitions, await _fixture.CountDefinitionsForCodeAsync("canvas_f3"));
        Assert.Equal(versions, await _fixture.CountVersionsAsync());
        Assert.Equal(0, await CountSqlProjectionsAsync(Code("f3")));
    }

    // ----------------------------------------------------------------- F4

    [Fact]
    [Trait("Gate", "F4_SQL_STAYS_SQL")]
    public async Task F4_a_sql_definition_reopens_as_sql_and_never_as_a_fabricated_graph()
    {
        var service = Service();
        var forked = GraphA;
        var saved = await service.SaveSqlAsync(Sql("f4", "SELECT 1 AS one UNION ALL SELECT 2", forked), CancellationToken.None);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        var reopened = await service.ReopenAsync(_fixture.TenantId, Code("f4"), null, CancellationToken.None);

        Assert.True(reopened.IsSuccess, reopened.Error?.Message);
        Assert.Equal(CanvasDefinitionContent.RepresentationSql, reopened.Value!.Representation.Representation);
        Assert.Null(reopened.Value.Representation.GraphJson);
        Assert.NotNull(reopened.Value.Representation.ForkedFromGraphJson);
    }

    // ----------------------------------------------------------------- F5

    [Fact]
    [Trait("Gate", "F5_IDENTITY_CONTINUITY")]
    public async Task F5_reload_and_edit_produce_a_new_version_of_the_same_definition()
    {
        var service = Service();

        var v1 = await service.SaveGraphAsync(Graph("f5", GraphA), CancellationToken.None);
        Assert.True(v1.IsSuccess, v1.Error?.Message);

        var reopened = await service.ReopenAsync(_fixture.TenantId, Code("f5"), null, CancellationToken.None);
        Assert.True(reopened.IsSuccess, reopened.Error?.Message);
        Assert.Equal(v1.Value!.DefinitionId, reopened.Value!.DefinitionId);
        Assert.Equal(1, reopened.Value.VersionNumber);

        var v2 = await service.SaveGraphAsync(Graph("f5", GraphB), CancellationToken.None);
        Assert.True(v2.IsSuccess, v2.Error?.Message);

        Assert.Equal(v1.Value.DefinitionId, v2.Value!.DefinitionId);
        Assert.Equal(2, v2.Value.VersionNumber);
        Assert.NotEqual(v1.Value.DefinitionHash, v2.Value.DefinitionHash);
        Assert.Equal(1L, await _fixture.CountDefinitionsForCodeAsync("canvas_f5"));

        var v1Again = await _fixture.ResolveExactAsync(v1.Value.DefinitionId, 1);
        using var v1Document = JsonDocument.Parse(v1Again.ContentJson);
        Assert.Equal(
            "10",
            v1Document.RootElement.GetProperty("graph").GetProperty("filters")[0].GetProperty("value").GetString());
    }

    // ----------------------------------------------------------------- F6

    [Fact]
    [Trait("Gate", "F6_IDEMPOTENCE")]
    public async Task F6_saving_identical_semantics_twice_does_not_fork_a_version()
    {
        var service = Service();

        var first = await service.SaveGraphAsync(Graph("f6", GraphA), CancellationToken.None);
        var second = await service.SaveGraphAsync(Graph("f6", GraphA), CancellationToken.None);
        var reordered = await service.SaveGraphAsync(Graph("f6", GraphAReordered), CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.True(reordered.IsSuccess, reordered.Error?.Message);

        Assert.Equal(first.Value!.DefinitionHash, second.Value!.DefinitionHash);
        Assert.Equal(first.Value.DefinitionHash, reordered.Value!.DefinitionHash);
        Assert.Equal(new List<int> { 1 }, await _fixture.VersionNumbersForCodeAsync("canvas_f6"));
    }

    // ----------------------------------------------------------------- F7

    [Fact]
    [Trait("Gate", "F7_TENANT_ISOLATION")]
    public async Task F7_a_code_does_not_resolve_publish_or_reopen_under_another_tenant()
    {
        var service = Service();
        var saved = await service.SaveGraphAsync(Graph("f7", GraphA), CancellationToken.None);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        var otherTenant = Guid.NewGuid();

        var reopened = await service.ReopenAsync(otherTenant, Code("f7"), null, CancellationToken.None);
        Assert.True(reopened.IsFailure);
        Assert.Equal(ApplicationErrorType.NotFound, reopened.Error!.Type);

        var published = await service.PublishAsync(otherTenant, Code("f7"), 1, GraphHandles(await NewSessionAsync("f7")), CancellationToken.None);
        Assert.True(published.IsFailure);
        Assert.Equal(CanonicalVersionStatus.Draft, (await _fixture.ResolveExactAsync(saved.Value!.DefinitionId, 1)).Status);
    }

    // ----------------------------------------------------------------- F8

    [Fact]
    [Trait("Gate", "F8_PUBLISH_EXACTNESS")]
    public async Task F8_publishing_version_n_makes_the_canonical_published_resolution_exactly_n()
    {
        var service = Service();
        var v1 = await service.SaveGraphAsync(Graph("f8", GraphA), CancellationToken.None);
        var v2 = await service.SaveGraphAsync(Graph("f8", GraphB), CancellationToken.None);
        Assert.True(v1.IsSuccess && v2.IsSuccess);

        var sessionId = await NewSessionAsync("f8");
        var published = await service.PublishAsync(_fixture.TenantId, Code("f8"), 1, GraphHandles(sessionId), CancellationToken.None);

        Assert.True(published.IsSuccess, published.Error?.Message);
        Assert.Equal(1, published.Value!.VersionNumber);

        var resolved = await _fixture.ResolvePublishedAsync(v1.Value!.DefinitionId);
        Assert.Equal(1, resolved.VersionNumber);
        Assert.Equal(CanonicalVersionStatus.Published, resolved.Status);
        Assert.Equal(1, await CountGraphProjectionsAsync(sessionId));
    }

    [Fact]
    [Trait("Gate", "F8_PUBLISH_EXACTNESS")]
    public async Task F8_sql_publish_writes_the_execution_projection_in_the_same_unit_of_work()
    {
        var service = Service();
        var saved = await service.SaveSqlAsync(Sql("f8sql", "SELECT 1 AS one"), CancellationToken.None);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        var published = await service.PublishAsync(_fixture.TenantId, Code("f8sql"), 1, SqlHandles(), CancellationToken.None);

        Assert.True(published.IsSuccess, published.Error?.Message);
        Assert.Equal(1, await CountSqlProjectionsAsync(Code("f8sql")));
        Assert.Equal(CanonicalVersionStatus.Published, (await _fixture.ResolvePublishedAsync(saved.Value!.DefinitionId)).Status);
    }

    // ----------------------------------------------------------------- F9

    /// <summary>
    /// The projection refuses any connection that is not the transaction's own.
    /// A projection on a second connection would be outside the unit of work
    /// and would be exactly the split-brain this task removes.
    /// </summary>
    [Fact]
    [Trait("Gate", "F9_SAME_TRANSACTION_MECHANICAL")]
    public async Task F9_the_projection_cannot_run_on_a_connection_other_than_the_transactions_own()
    {
        await using var first = _fixture.NewContext();
        await using var second = _fixture.NewContext();

        var firstConnection = (NpgsqlConnection)first.Database.GetDbConnection();
        var secondConnection = (NpgsqlConnection)second.Database.GetDbConnection();
        await firstConnection.OpenAsync();
        await secondConnection.OpenAsync();

        await using var transaction = await firstConnection.BeginTransactionAsync();

        var projection = new CanvasCompatibilityProjection();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            projection.ProjectGraphVersionAsync(secondConnection, transaction, _fixture.TenantId, Guid.NewGuid(), 1, "{}", "test", CancellationToken.None));

        await transaction.RollbackAsync();
    }

    [Fact]
    [Trait("Gate", "F9_SAME_TRANSACTION_MECHANICAL")]
    public async Task F9_a_projection_written_on_the_callers_transaction_disappears_when_it_rolls_back()
    {
        await using var db = _fixture.NewContext();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync();

        var sessionId = await NewSessionAsync("f9");

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await new CanvasCompatibilityProjection().ProjectGraphVersionAsync(
                connection, transaction, _fixture.TenantId, sessionId, 1, "{}", "test", CancellationToken.None);
            await transaction.RollbackAsync();
        }

        Assert.Equal(0, await CountGraphProjectionsAsync(sessionId));
    }

    // ------------------------------------------------------ PORTABILITY (Q5)

    [Fact]
    [Trait("Gate", "PORTABILITY_COMPATIBLE")]
    public async Task A_canvas_definition_exports_through_the_existing_portability_contract()
    {
        var service = Service();
        var saved = await service.SaveGraphAsync(Graph("port", GraphA), CancellationToken.None);
        Assert.True(saved.IsSuccess, saved.Error?.Message);

        await using var db = _fixture.NewContext();
        var writer = new CanonicalDefinitionWriter(db);
        var graph = new CanonicalDefinitionGraph(db);
        var exporter = new DefinitionExporter(db, graph);
        var portability = new DefinitionPortability(exporter, new DefinitionImporter(db, writer, exporter));

        var exported = await portability.ExportAsync(_fixture.TenantId, saved.Value!.DefinitionId, 1, CancellationToken.None);

        Assert.True(exported.IsSuccess, exported.Error?.Message);
        var root = Assert.Single(exported.Value!.Definitions, d => d.DefinitionCode == Code("port"));
        Assert.Equal("transformation", root.Kind);
        Assert.Equal(1, root.VersionNumber);
    }

    // --------------------------------------------------------------- helpers

    private sealed class FailingProjection : ICanvasCompatibilityProjection
    {
        public bool WasInvokedInsideTransaction { get; private set; }

        public Task ProjectGraphVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, Guid sessionId, int versionNumber, string graphJson, string publishedBy, CancellationToken cancellationToken)
        {
            WasInvokedInsideTransaction = transaction is not null && ReferenceEquals(transaction.Connection, connection);
            throw new InvalidOperationException("forced projection failure");
        }

        public Task ProjectSqlVersionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string mappingCode, string? displayName, string? canonicalEntity, int versionNumber, string definitionJson, string status, CancellationToken cancellationToken)
        {
            WasInvokedInsideTransaction = transaction is not null && ReferenceEquals(transaction.Connection, connection);
            throw new InvalidOperationException("forced projection failure");
        }
    }

    private async Task<long> CountGraphProjectionsAsync(Guid sessionId)
    {
        await using var db = _fixture.NewContext();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM ppiq_meta.ppiq_visual_mapper_versions WHERE session_id = @s;", connection);
        command.Parameters.AddWithValue("s", sessionId);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<long> CountSqlProjectionsAsync(string code)
    {
        await using var db = _fixture.NewContext();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM ppiq_meta.ppiq_mapping_versions WHERE mapping_code = @c;", connection);
        command.Parameters.AddWithValue("c", code);
        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task ClearProjectionsAsync()
    {
        await using var db = _fixture.NewContext();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "DELETE FROM ppiq_meta.ppiq_mapping_versions WHERE mapping_code LIKE @p; " +
            "DELETE FROM ppiq_meta.ppiq_visual_mapper_sessions WHERE tenant_id = @t AND source_code LIKE @p;", connection);
        command.Parameters.AddWithValue("p", Prefix + "%");
        command.Parameters.AddWithValue("t", _fixture.TenantId);
        await command.ExecuteNonQueryAsync();
    }
}
