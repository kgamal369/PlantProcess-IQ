using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

/// <summary>
/// PPIQ T-090. Shared state for the canonical definition store tests.
///
/// INDEPENDENT CONTEXTS ARE THE POINT. NewContext returns a context with its
/// own connection every time, because the first-parent race test launches two
/// callers through a Barrier. Sharing one context would make them serialise on
/// a single connection and the test would pass while proving nothing.
///
/// TENANT AND OWNER ARE REAL ROWS. The writer refuses synthesised identity, so
/// the fixture reads them from ppiq_meta.tenants and ppiq_meta.app_users and
/// fails loudly if the database has none - an invented GUID here would let a
/// test pass that production could not.
/// </summary>
public sealed class DefinitionStoreFixture : IAsyncLifetime
{
    private string _connectionString = string.Empty;

    public Guid TenantId { get; private set; }

    public Guid OwnerId { get; private set; }

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

        Assert.True(tenant.HasValue,
            "No tenant could be resolved. These tests require a provisioned database; they do not invent identity.");
        Assert.True(owner.HasValue,
            "No application user could be resolved. These tests require a provisioned database.");

        TenantId = tenant!.Value;
        OwnerId = owner!.Value;
    }

    public Task DisposeAsync() => ResetAsync();

    public PlantProcessDbContext NewContext()
    {
        // BUILT THE WAY PRODUCTION BUILDS IT. The frozen T-039 file warns that a
        // context configured with UseNpgsql alone emits PascalCase column names
        // and fails on its first statement. The convention is part of the
        // mapping, and this fixture gets no exemption from it. (W1-T090-FIXTURE-01)
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PlantProcessDbContext(options);
    }

    /// <summary>
    /// Removes only the definitions these tests create. Test codes carry a
    /// recognisable prefix so a reset can never remove product or customer
    /// definitions from a shared development database.
    /// </summary>
    public async Task ResetAsync()
    {
        // BOTTOM-UP, ALWAYS. fk_definition_versions_store forbids deleting a
        // parent that still has version children, so the reset removes child
        // rows first: outcome and detail rows, dependencies, versions, and only
        // then the store identity. (W1-T090-RESET-01)
        await ExecuteAsync(
            """
            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't090test_%')
            DELETE FROM ppiq_meta.outcome_details od
             USING ppiq_meta.definition_versions v, doomed
             WHERE od.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't090test_%')
            DELETE FROM ppiq_meta.transformation_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't090test_%')
            DELETE FROM ppiq_meta.widget_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't090test_%')
            DELETE FROM ppiq_meta.analysis_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't090test_%')
            DELETE FROM ppiq_meta.model_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't090test_%')
            DELETE FROM ppiq_meta.log_rule_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't090test_%')
            DELETE FROM ppiq_meta.definition_dependencies d USING doomed
             WHERE d.definition_id = doomed.id OR d.depends_on_definition_id = doomed.id;

            WITH doomed AS (
                SELECT s.id FROM ppiq_meta.definition_store s
                 WHERE s.tenant_id = @tenant_id AND s.definition_code LIKE 't090test_%')
            DELETE FROM ppiq_meta.definition_versions v USING doomed
             WHERE v.definition_id = doomed.id;

            DELETE FROM ppiq_meta.definition_store
             WHERE tenant_id = @tenant_id AND definition_code LIKE 't090test_%';
            """);
    }

    private const string Prefix = "t090test_";

    private static string Code(string suffix) => Prefix + suffix;

    // ------------------------------------------------------------- write shapes

    public CanonicalDefinitionWrite SampleWrite(string code) =>
        new(DefinitionKind.Widget, TenantId, OwnerId, Code(code), "Sample widget",
            "{\"widgetTitle\":\"Sample widget\"}",
            CanonicalVersionStatus.Published,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["widget_kind"] = "chart",
                ["chart_type"] = "bar",
                ["dimension_code"] = "day",
                ["measure_code"] = "defectRate",
            });

    /// <summary>
    /// A complete SM-06 declaration carrying all ten frozen fields. This is the
    /// controlled acceptance definition, never a migrated legacy row.
    /// </summary>
    public CanonicalDefinitionWrite CompleteOutcomeWrite(
        string code,
        string detectionPosition = "final_inspection",
        string outcomeCode = "surface_defect") =>
        new(DefinitionKind.Transformation, TenantId, OwnerId, Code(code), "Outcome contract",
            "{\"contract\":\"outcome\"}",
            CanonicalVersionStatus.Draft,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["projection_mode"] = "declared",
            },
            new[]
            {
                new CanonicalOutcomeDeclaration(
                    OutcomeCode: outcomeCode,
                    OutcomeType: "binary",
                    ClassTaxonomyRef: null,
                    OrdinalRankMapJson: null,
                    GrainCode: "material_unit",
                    DetectionPositionCode: detectionPosition,
                    DetectionTimestampField: "inspected_at_utc",
                    Direction: "lower_is_better",
                    UnitCode: null,
                    CensoringPolicy: "none"),
            });

    /// <summary>
    /// What a migration produces when the legacy row could not supply the
    /// leakage anchors. It is legal as a draft and must never publish.
    /// </summary>
    public CanonicalDefinitionWrite SentinelOutcomeWrite(string code) =>
        CompleteOutcomeWrite(code) with
        {
            Status = CanonicalVersionStatus.Draft,
            Outcomes = new[]
            {
                new CanonicalOutcomeDeclaration(
                    OutcomeCode: "surface_defect",
                    OutcomeType: "binary",
                    ClassTaxonomyRef: null,
                    OrdinalRankMapJson: null,
                    GrainCode: "material_unit",
                    DetectionPositionCode: DefinitionKindRegistry.MigratedUnknown,
                    DetectionTimestampField: DefinitionKindRegistry.MigratedUnknown,
                    Direction: "none",
                    UnitCode: null,
                    CensoringPolicy: "none"),
            },
        };

    /// <summary>
    /// Two outcomes on one transformation version. Proves outcome_code is the
    /// key WITHIN a version rather than across the store.
    /// </summary>
    public CanonicalDefinitionWrite TwoOutcomeWrite(string code) =>
        CompleteOutcomeWrite(code) with
        {
            Outcomes = new[]
            {
                new CanonicalOutcomeDeclaration(
                    "surface_defect", "binary", null, null, "material_unit",
                    "final_inspection", "inspected_at_utc", "lower_is_better", null, "none"),
                new CanonicalOutcomeDeclaration(
                    "yield_ratio", "continuous", null, null, "material_unit",
                    "final_inspection", "inspected_at_utc", "higher_is_better", "ratio", "none"),
            },
        };

    // ---------------------------------------------------------------- operations

    public async Task<CanonicalDefinitionVersion> WriteAsync(CanonicalDefinitionWrite write)
    {
        var result = await TryWriteAsync(write);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    public async Task<ApplicationResult<CanonicalDefinitionVersion>> TryWriteAsync(CanonicalDefinitionWrite write)
    {
        await using var db = NewContext();
        var writer = new CanonicalDefinitionWriter(db);

        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await writer.WriteVersionAsync(write, CancellationToken.None);

        if (result.IsSuccess) { await transaction.CommitAsync(); }
        else { await transaction.RollbackAsync(); }

        return result;
    }

    public async Task<CanonicalDefinitionVersion> PublishAsync(Guid definitionId, int versionNumber)
    {
        var result = await TryPublishAsync(definitionId, versionNumber);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    public async Task<ApplicationResult<CanonicalDefinitionVersion>> TryPublishAsync(Guid definitionId, int versionNumber)
    {
        await using var db = NewContext();
        var writer = new CanonicalDefinitionWriter(db);

        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await writer.PublishAsync(definitionId, versionNumber, CancellationToken.None);

        if (result.IsSuccess) { await transaction.CommitAsync(); }
        else { await transaction.RollbackAsync(); }

        return result;
    }

    public async Task<CanonicalDefinitionVersion> ResolveExactAsync(Guid definitionId, int versionNumber)
    {
        await using var db = NewContext();
        var result = await new CanonicalDefinitionWriter(db)
            .ResolveExactAsync(definitionId, versionNumber, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    public async Task<CanonicalDefinitionVersion> ResolvePublishedAsync(Guid definitionId)
    {
        await using var db = NewContext();
        var result = await new CanonicalDefinitionWriter(db)
            .ResolvePublishedAsync(definitionId, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!;
    }

    // ------------------------------------------------------------- observations

    public Task<long> CountVersionsAsync() =>
        ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_versions v " +
                    "JOIN ppiq_meta.definition_store s ON s.id = v.definition_id " +
                    "WHERE s.definition_code LIKE 't090test_%';");

    public Task<long> CountVersionsForCodeAsync(string code) =>
        ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_versions v " +
                    "JOIN ppiq_meta.definition_store s ON s.id = v.definition_id " +
                    "WHERE s.definition_code = @code;", Code(code));

    public Task<long> CountDefinitionsForCodeAsync(string code) =>
        ScalarAsync("SELECT count(*) FROM ppiq_meta.definition_store WHERE definition_code = @code;", Code(code));

    public Task<long> CountOutcomeRowsAsync(Guid versionId) =>
        ScalarAsync("SELECT count(*) FROM ppiq_meta.outcome_details WHERE definition_version_id = @version;",
                    versionId: versionId);

    public Task<long> CountDetailRowsAsync(string table, string code) =>
        ScalarAsync($"SELECT count(*) FROM ppiq_meta.{table} d " +
                    "JOIN ppiq_meta.definition_versions v ON v.id = d.definition_version_id " +
                    "JOIN ppiq_meta.definition_store s ON s.id = v.definition_id " +
                    "WHERE s.definition_code = @code;", Code(code));

    public async Task<Guid?> FindDefinitionAsync(string code)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT id FROM ppiq_meta.definition_store WHERE definition_code = @code LIMIT 1;", connection);
        command.Parameters.AddWithValue("code", Code(code));

        return await command.ExecuteScalarAsync() as Guid?;
    }

    public async Task<List<int>> VersionNumbersForCodeAsync(string code)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT v.version_number FROM ppiq_meta.definition_versions v " +
            "JOIN ppiq_meta.definition_store s ON s.id = v.definition_id " +
            "WHERE s.definition_code = @code ORDER BY v.version_number;", connection);
        command.Parameters.AddWithValue("code", Code(code));

        var numbers = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) { numbers.Add(reader.GetInt32(0)); }
        return numbers;
    }

    /// <summary>
    /// Runs a statement expected to be refused BY THE DATABASE and returns the
    /// error text. Used to prove immutability is enforced by the trigger rather
    /// than by application code, which any caller could bypass.
    /// </summary>
    public async Task<string> ExpectSqlFailureAsync(string sql, Guid id)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);

        try
        {
            await command.ExecuteNonQueryAsync();
            return string.Empty;
        }
        catch (PostgresException exception)
        {
            return exception.SqlState + " " + exception.MessageText;
        }
    }

    private async Task<long> ScalarAsync(string sql, string? code = null, Guid? versionId = null)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        if (code is not null) { command.Parameters.AddWithValue("code", code); }
        if (versionId is not null) { command.Parameters.AddWithValue("version", versionId.Value); }

        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("tenant_id", TenantId);
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// One fixture instance across the canonical store tests. They share a database
/// and each resets only its own prefixed definitions.
/// </summary>
[CollectionDefinition("CanonicalDefinitionStore")]
public sealed class CanonicalDefinitionStoreCollection : ICollectionFixture<DefinitionStoreFixture>
{
}
