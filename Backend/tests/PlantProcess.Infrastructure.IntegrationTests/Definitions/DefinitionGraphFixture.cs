using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

/// <summary>
/// PPIQ T-091. The neutral known-answer graph, built once and reused by every
/// T-091 gate.
///
/// NEUTRAL VOCABULARY ON PURPOSE. No coil, furnace, defect or customer term
/// appears here. A fixture that speaks one industry's language quietly teaches
/// the tests that the platform is about that industry, and PPIQ is generic.
///
/// THE SHAPE, AND WHY EACH PART EXISTS:
///
///   upstream, exported with ROOT_A:
///       t091_source_x   (Widget)   - pinned edge, cross-kind
///       t091_source_y   (Analysis) - unpinned edge, resolves to published
///
///   root:
///       t091_root_a     (Analysis)
///
///   downstream, the impact answer:
///       t091_consumer_b (Model)     -> root_a          direct
///       t091_consumer_c (Model)     -> root_a          direct
///       t091_consumer_d (LogRule)   -> consumer_b      transitive
///                                   -> consumer_c      transitive  (diamond)
///
/// consumer_d is reachable through two paths, which is what makes the
/// deduplication gate meaningful: a naive walk returns it twice.
/// </summary>
public sealed class T091GraphFixture : IAsyncLifetime
{
    public const string SourceX = "t091_source_x";
    public const string SourceY = "t091_source_y";
    public const string RootA = "t091_root_a";
    public const string ConsumerB = "t091_consumer_b";
    public const string ConsumerC = "t091_consumer_c";
    public const string ConsumerD = "t091_consumer_d";

    // Outside the root's dependency closure ON PURPOSE. The cross-tenant gate
    // needs a consumer edge that is legal in the graph: pointing an existing
    // upstream dependency back at the root would close a cycle, and T-089's
    // trigger would refuse it before tenancy was ever tested.
    public const string ForeignProbe = "t091_foreign_probe";

    private string _connectionString = string.Empty;

    public Guid TenantId { get; private set; }

    public Guid OwnerId { get; private set; }

    public Dictionary<string, Guid> Ids { get; } = new(StringComparer.Ordinal);

    public async Task InitializeAsync()
    {
        var host = Environment.GetEnvironmentVariable("PPIQ_TEST_PGHOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("PPIQ_TEST_PGPORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("PPIQ_TEST_PGDATABASE") ?? "ppiq_app";
        var user = Environment.GetEnvironmentVariable("PPIQ_TEST_PGUSER") ?? "ppiq_dev";
        var password = Environment.GetEnvironmentVariable("PPIQ_TEST_PGPASSWORD") ?? "ppiq_dev_local_only";

        _connectionString =
            $"Host={host};Port={port};Database={database};Username={user};Password={password};Include Error Detail=true";

        await using var db = NewContext();
        var resolver = new CanonicalIdentityResolver(db);

        var tenant = await resolver.ResolveTenantAsync(null, CancellationToken.None);
        var owner = await resolver.ResolveOwnerAsync(null, CancellationToken.None);

        Assert.True(tenant.HasValue, "T-091 tests require a provisioned tenant; identity is never invented.");
        Assert.True(owner.HasValue, "T-091 tests require a provisioned application user.");

        TenantId = tenant!.Value;
        OwnerId = owner!.Value;

        await ResetAsync();
        await BuildAsync();
    }

    public Task DisposeAsync() => ResetAsync();

    public PlantProcessDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PlantProcessDbContext(options);
    }

    public string ConnectionString => _connectionString;

    /// <summary>
    /// Builds the graph through the canonical writer only. Nothing here inserts
    /// a semantic row directly: a fixture that wrote its own rows could produce
    /// state the product cannot produce, and every gate over it would be
    /// measuring a fiction.
    /// </summary>
    public async Task BuildAsync()
    {
        await using var db = NewContext();
        var writer = new CanonicalDefinitionWriter(db);

        Ids[SourceX] = await WriteAsync(db, writer, DefinitionKind.Widget, SourceX, "Source X",
            "{\"role\":\"upstream-widget\"}",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["widget_kind"] = "chart",
                ["chart_type"] = "bar",
                ["dimension_code"] = "dim_neutral",
                ["measure_code"] = "mea_neutral",
            });

        Ids[SourceY] = await WriteAsync(db, writer, DefinitionKind.Analysis, SourceY, "Source Y",
            "{\"role\":\"upstream-analysis\"}",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outcome_code"] = "neutral_outcome",
                ["grain_code"] = "neutral_grain",
                ["method_code"] = "neutral_method",
            });

        Ids[RootA] = await WriteAsync(db, writer, DefinitionKind.Analysis, RootA, "Root A",
            "{\"role\":\"root\"}",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["outcome_code"] = "root_outcome",
                ["grain_code"] = "neutral_grain",
                ["method_code"] = "neutral_method",
            });

        Ids[ConsumerB] = await WriteAsync(db, writer, DefinitionKind.Model, ConsumerB, "Consumer B",
            "{\"role\":\"direct-consumer\"}",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["algorithm_code"] = "neutral_algorithm",
                ["hyperparameters"] = "{\"depth\":3}",
            });

        Ids[ConsumerC] = await WriteAsync(db, writer, DefinitionKind.Model, ConsumerC, "Consumer C",
            "{\"role\":\"direct-consumer\"}",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["algorithm_code"] = "neutral_algorithm",
                ["hyperparameters"] = "{\"depth\":5}",
            });

        Ids[ConsumerD] = await WriteAsync(db, writer, DefinitionKind.LogRule, ConsumerD, "Consumer D",
            "{\"role\":\"transitive-consumer\"}",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["condition_expression"] = "value > 1",
                ["severity"] = "warning",
                ["message_template"] = "Neutral rule fired",
            });

        Ids[ForeignProbe] = await WriteAsync(db, writer, DefinitionKind.Filter, ForeignProbe, "Foreign Probe",
            "{\"role\":\"tenancy-probe\"}",
            new Dictionary<string, object?>(StringComparer.Ordinal));

        // Upstream: one pinned edge (version 1 of source X) and one unpinned
        // edge, which export must resolve to the published version and then
        // write into the artifact as a pin.
        await EdgeAsync(Ids[RootA], Ids[SourceX], "master_item", true, 1);
        await EdgeAsync(Ids[RootA], Ids[SourceY], "feature_set", true, null);

        // Downstream: two direct consumers and one transitive consumer reached
        // through both of them.
        await EdgeAsync(Ids[ConsumerB], Ids[RootA], "model", true, null);
        await EdgeAsync(Ids[ConsumerC], Ids[RootA], "model", true, 1);
        await EdgeAsync(Ids[ConsumerD], Ids[ConsumerB], "model", true, null);
        await EdgeAsync(Ids[ConsumerD], Ids[ConsumerC], "model", false, null);
    }

    private async Task<Guid> WriteAsync(
        PlantProcessDbContext db,
        CanonicalDefinitionWriter writer,
        DefinitionKind kind,
        string code,
        string name,
        string contentJson,
        IReadOnlyDictionary<string, object?> detail)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();

        var result = await writer.WriteVersionAsync(new CanonicalDefinitionWrite(
            kind, TenantId, OwnerId, code, name, contentJson,
            CanonicalVersionStatus.Published, detail), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        await transaction.CommitAsync();

        return result.Value!.DefinitionId;
    }

    public async Task EdgeAsync(Guid from, Guid to, string dependencyKind, bool required, int? pinnedVersion)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO ppiq_meta.definition_dependencies
                (tenant_id, definition_id, depends_on_definition_id, depends_on_version,
                 dependency_kind, is_required, created_by)
            VALUES (@tenant_id, @definition_id, @depends_on_definition_id, @depends_on_version,
                    @dependency_kind, @is_required, @created_by)
            ON CONFLICT (definition_id, depends_on_definition_id, dependency_kind) DO NOTHING;
            """, connection);

        command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = TenantId });
        command.Parameters.Add(new NpgsqlParameter("definition_id", NpgsqlDbType.Uuid) { Value = from });
        command.Parameters.Add(new NpgsqlParameter("depends_on_definition_id", NpgsqlDbType.Uuid) { Value = to });
        command.Parameters.Add(new NpgsqlParameter("dependency_kind", NpgsqlDbType.Text) { Value = dependencyKind });
        command.Parameters.Add(new NpgsqlParameter("is_required", NpgsqlDbType.Boolean) { Value = required });
        command.Parameters.Add(new NpgsqlParameter("created_by", NpgsqlDbType.Uuid) { Value = OwnerId });
        command.Parameters.Add(new NpgsqlParameter("depends_on_version", NpgsqlDbType.Integer)
        {
            Value = pinnedVersion.HasValue ? pinnedVersion.Value : (object)DBNull.Value
        });

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Bottom-up, for the same reason T-090's fixture is: the store refuses to
    /// drop a parent that still has version children, and edges reference the
    /// store from both ends.
    /// </summary>
    public async Task ResetAsync()
    {
        await ExecuteAsync(
            """
            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.definition_dependencies d USING doomed
             WHERE d.definition_id = doomed.id OR d.depends_on_definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.outcome_details od USING ppiq_meta.definition_versions v, doomed
             WHERE od.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.widget_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.analysis_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.model_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.log_rule_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.definition_versions v USING doomed
             WHERE v.definition_id = doomed.id;

            DELETE FROM ppiq_meta.definition_store
             WHERE tenant_id = @tenant_id AND definition_code LIKE 't091_%';
            """);
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = TenantId });
        await command.ExecuteNonQueryAsync();
    }

    public async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = TenantId });
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}

[CollectionDefinition("T091DefinitionGraph")]
public sealed class T091GraphCollection : ICollectionFixture<T091GraphFixture>
{
}
