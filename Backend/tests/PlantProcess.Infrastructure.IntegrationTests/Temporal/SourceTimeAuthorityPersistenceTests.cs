using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PlantProcess.Analytics.Core.Kernel;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Services.Mapping;
using PlantProcess.Application.Temporal;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Persistence;
using PlantProcess.Infrastructure.Temporal;
using PlantProcess.TestSupport;

namespace PlantProcess.Infrastructure.IntegrationTests.Temporal;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SourceTimeAuthorityLiveCollection
{
    public const string Name = "SourceTimeAuthorityLive";
}

/// <summary>
/// Source Time Authority persistence and observation time provenance, proven against
/// a real canonical database (backlog reference: T-233).
///
/// The database is supplied by the runner that built it from the canonical path and
/// that drops it afterwards. There is no fallback: without that database these tests
/// skip, and a skip is never counted as evidence. Every tenant created here is
/// INACTIVE, because the projection path requires exactly one active tenant and the
/// canonical baseline already provides it.
/// </summary>
[Collection(SourceTimeAuthorityLiveCollection.Name)]
public sealed class SourceTimeAuthorityPersistenceTests
{
    private static readonly DateTime From = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static string Connection()
    {
        var available = TestDatabaseTarget.TryResolve(
            TestDatabaseTarget.IntegrationVariable, out var connectionString, out var reason);
        Skip.IfNot(available, reason);

        var database = TestDatabaseTarget.DatabaseNameOf(connectionString) ?? string.Empty;
        if (!database.Contains("acceptance", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Refusing to mutate '" + database + "': this suite runs only on a runner-owned disposable acceptance database.");
        }

        return connectionString;
    }

    private static async Task<Guid> NewInactiveTenantAsync(string connectionString)
    {
        var code = "STA-" + Guid.NewGuid().ToString("N")[..12];
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var insert = new NpgsqlCommand(
            "INSERT INTO ppiq_meta.tenants (id, tenant_code, display_name, is_active) " +
            "VALUES (@id, @code, @name, false) RETURNING id;", connection);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("code", code);
        insert.Parameters.AddWithValue("name", "Source time tenant " + code);
        return (Guid)(await insert.ExecuteScalarAsync())!;
    }

    private static SourceTimeAuthorityDeclarationRequest Declaration(
        string source,
        string signal,
        string origin,
        TimeSpan fixedOffset,
        TimeSpan resolution,
        TimeSpan skew,
        string convention,
        DateTime? from = null,
        DateTime? to = null,
        string role = "Effective",
        string? zone = null) =>
        new(source, signal, role, origin, fixedOffset, zone, resolution, skew, convention,
            from ?? From, to, null, "source-time-test", null);

    private static SourceTimeAuthorityDeclarationRequest EmbeddedSource(string source = "SRC-EMBEDDED") =>
        Declaration(source, "event_time", "EmbeddedInValue", TimeSpan.Zero,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "ResolutionIsQuantisationStep");

    private static SourceTimeAuthorityDeclarationRequest FixedOffsetSource(string source = "SRC-FIXED") =>
        Declaration(source, "event_time", "DeclaredFixedOffset", TimeSpan.FromHours(2),
            TimeSpan.FromMilliseconds(500), TimeSpan.Zero, "ResolutionIsHalfWidth");

    [SkippableFact]
    public async Task Two_sources_with_different_origins_and_known_skew_persist_and_replay_through_the_kernel()
    {
        var connectionString = Connection();
        var tenant = await NewInactiveTenantAsync(connectionString);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var store = new SourceTimeAuthorityStore(dataSource);

        var embedded = await store.DeclareAsync(tenant, EmbeddedSource(), CancellationToken.None);
        var fixedOffset = await store.DeclareAsync(tenant, FixedOffsetSource(), CancellationToken.None);
        Assert.True(embedded.IsAccepted, embedded.Detail);
        Assert.True(fixedOffset.IsAccepted, fixedOffset.Detail);

        var registry = await store.LoadRegistryAsync(tenant, From.AddDays(10), CancellationToken.None);
        Assert.Equal(2, registry.SignalCount);

        // Exact round trip: the stored row is the same contract value the kernel holds.
        Assert.True(SourceTimeAuthorityDeclarations.TryNormalise(EmbeddedSource(), out var expectedEmbedded, out _));
        Assert.True(registry.TryGetSignal("SRC-EMBEDDED", "event_time", out var loadedEmbedded));
        Assert.Equal(expectedEmbedded, loadedEmbedded);

        var first = SourceTimeAuthorityKernel.Resolve(
            registry, "SRC-EMBEDDED", "event_time",
            RawSourceTime.WithEmbeddedOffset(new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.FromHours(5))));
        var second = SourceTimeAuthorityKernel.Resolve(
            registry, "SRC-FIXED", "event_time",
            RawSourceTime.WithoutOffset(new DateTime(2026, 3, 1, 7, 0, 1, DateTimeKind.Unspecified)));

        Assert.True(first.IsResolved, first.Code);
        Assert.True(second.IsResolved, second.Code);

        // The embedded offset is preserved as given; the fixed offset is the declared one.
        Assert.Equal(TimeSpan.FromHours(5), first.Instant!.Instant.Offset);
        Assert.Equal(TimeSpan.FromHours(2), second.Instant!.Instant.Offset);

        // Known skew travels with each instant: half a one-second step plus two
        // seconds of skew, against a half-width of half a second.
        Assert.Equal(TimeSpan.FromMilliseconds(2500), first.Instant.Uncertainty);
        Assert.Equal(TimeSpan.FromMilliseconds(500), second.Instant.Uncertainty);

        // One second apart in UTC, inside the combined uncertainty: not orderable.
        var verdict = SourceTimeAuthorityKernel.Order(first.Instant, second.Instant);
        Assert.Equal(TemporalOrdering.Indeterminate, verdict.Ordering);
    }

    [SkippableFact]
    public async Task An_identical_redeclaration_returns_the_same_identity()
    {
        var connectionString = Connection();
        var tenant = await NewInactiveTenantAsync(connectionString);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var store = new SourceTimeAuthorityStore(dataSource);

        var first = await store.DeclareAsync(tenant, FixedOffsetSource(), CancellationToken.None);
        var again = await store.DeclareAsync(tenant, FixedOffsetSource(" SRC-FIXED "), CancellationToken.None);

        Assert.True(first.IsAccepted, first.Detail);
        Assert.True(again.IsAccepted, again.Detail);
        Assert.Equal(first.Id, again.Id);
    }

    [SkippableFact]
    public async Task A_different_declaration_at_the_same_start_is_a_conflict()
    {
        var connectionString = Connection();
        var tenant = await NewInactiveTenantAsync(connectionString);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var store = new SourceTimeAuthorityStore(dataSource);

        Assert.True((await store.DeclareAsync(tenant, FixedOffsetSource(), CancellationToken.None)).IsAccepted);

        var changed = Declaration("SRC-FIXED", "event_time", "DeclaredFixedOffset", TimeSpan.FromHours(3),
            TimeSpan.FromMilliseconds(500), TimeSpan.Zero, "ResolutionIsHalfWidth");
        var result = await store.DeclareAsync(tenant, changed, CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(SourceTimeCodes.ConflictingDeclaration, result.Code);
    }

    [SkippableFact]
    public async Task An_overlapping_window_is_a_conflict_and_an_adjacent_window_is_lawful()
    {
        var connectionString = Connection();
        var tenant = await NewInactiveTenantAsync(connectionString);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var store = new SourceTimeAuthorityStore(dataSource);

        var closed = Declaration("SRC-WINDOW", "event_time", "DeclaredFixedOffset", TimeSpan.FromHours(1),
            TimeSpan.Zero, TimeSpan.Zero, "ResolutionIsHalfWidth", From, From.AddDays(30));
        Assert.True((await store.DeclareAsync(tenant, closed, CancellationToken.None)).IsAccepted);

        var overlapping = Declaration("SRC-WINDOW", "event_time", "DeclaredFixedOffset", TimeSpan.FromHours(2),
            TimeSpan.Zero, TimeSpan.Zero, "ResolutionIsHalfWidth", From.AddDays(10));
        var refused = await store.DeclareAsync(tenant, overlapping, CancellationToken.None);
        Assert.False(refused.IsAccepted);
        Assert.Equal(SourceTimeCodes.ConflictingDeclaration, refused.Code);

        var adjacent = Declaration("SRC-WINDOW", "event_time", "DeclaredFixedOffset", TimeSpan.FromHours(2),
            TimeSpan.Zero, TimeSpan.Zero, "ResolutionIsHalfWidth", From.AddDays(30));
        Assert.True((await store.DeclareAsync(tenant, adjacent, CancellationToken.None)).IsAccepted);

        var before = await store.FindInForceAsync(tenant, "SRC-WINDOW", "event_time", From.AddDays(29), CancellationToken.None);
        var after = await store.FindInForceAsync(tenant, "SRC-WINDOW", "event_time", From.AddDays(30), CancellationToken.None);
        var earlier = await store.FindInForceAsync(tenant, "SRC-WINDOW", "event_time", From.AddDays(-1), CancellationToken.None);

        Assert.Equal(TimeSpan.FromHours(1), before!.Declaration.FixedOffset);
        Assert.Equal(TimeSpan.FromHours(2), after!.Declaration.FixedOffset);
        Assert.Null(earlier);
    }

    [SkippableFact]
    public async Task Declarations_are_invisible_across_tenants()
    {
        var connectionString = Connection();
        var tenantA = await NewInactiveTenantAsync(connectionString);
        var tenantB = await NewInactiveTenantAsync(connectionString);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var store = new SourceTimeAuthorityStore(dataSource);

        Assert.True((await store.DeclareAsync(tenantA, FixedOffsetSource(), CancellationToken.None)).IsAccepted);

        Assert.Empty(await store.ListInForceAsync(tenantB, From.AddDays(1), CancellationToken.None));
        Assert.Null(await store.FindInForceAsync(tenantB, "SRC-FIXED", "event_time", From.AddDays(1), CancellationToken.None));
        Assert.Equal(0, (await store.LoadRegistryAsync(tenantB, From.AddDays(1), CancellationToken.None)).SignalCount);

        // The same keys under another tenant are an independent declaration, not a conflict.
        var other = Declaration("SRC-FIXED", "event_time", "DeclaredFixedOffset", TimeSpan.FromHours(-5),
            TimeSpan.Zero, TimeSpan.Zero, "ResolutionIsHalfWidth");
        Assert.True((await store.DeclareAsync(tenantB, other, CancellationToken.None)).IsAccepted);

        var a = await store.FindInForceAsync(tenantA, "SRC-FIXED", "event_time", From.AddDays(1), CancellationToken.None);
        var b = await store.FindInForceAsync(tenantB, "SRC-FIXED", "event_time", From.AddDays(1), CancellationToken.None);
        Assert.Equal(TimeSpan.FromHours(2), a!.Declaration.FixedOffset);
        Assert.Equal(TimeSpan.FromHours(-5), b!.Declaration.FixedOffset);
    }

    [SkippableFact]
    public async Task The_database_refuses_what_the_contract_refuses_even_without_the_application()
    {
        var connectionString = Connection();
        var tenant = await NewInactiveTenantAsync(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var zoneless = new NpgsqlCommand(
            "SELECT ppiq_meta.declare_source_time_signal(@t, 'SRC-DB', 'event_time', 'Effective', " +
            "'DeclaredZoneRule', 0, NULL, 0, 0, 'ResolutionIsHalfWidth', @f, NULL);", connection))
        {
            zoneless.Parameters.AddWithValue("t", tenant);
            zoneless.Parameters.AddWithValue("f", From);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => zoneless.ExecuteScalarAsync());
            Assert.StartsWith("ST11", ex.MessageText, StringComparison.Ordinal);
        }

        await using (var negative = new NpgsqlCommand(
            "INSERT INTO ppiq_meta.source_time_authorities (tenant_id, source_key, signal_key, time_role, offset_origin, " +
            "resolution_ticks, max_clock_skew_ticks, uncertainty_convention, effective_from_utc) " +
            "VALUES (@t, 'SRC-DB', 'raw', 'Effective', 'EmbeddedInValue', -1, 0, 'ResolutionIsHalfWidth', @f);", connection))
        {
            negative.Parameters.AddWithValue("t", tenant);
            negative.Parameters.AddWithValue("f", From);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => negative.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }

        await using (var undeclared = new NpgsqlCommand(
            "INSERT INTO ppiq_meta.source_time_authorities (tenant_id, source_key, signal_key, time_role, offset_origin, " +
            "resolution_ticks, max_clock_skew_ticks, uncertainty_convention, effective_from_utc) " +
            "VALUES (@t, 'SRC-DB', 'raw2', 'Effective', 'Undeclared', 0, 0, 'ResolutionIsHalfWidth', @f);", connection))
        {
            undeclared.Parameters.AddWithValue("t", tenant);
            undeclared.Parameters.AddWithValue("f", From);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => undeclared.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        }
    }

    [SkippableFact]
    public async Task Runtime_role_has_select_and_function_execute_but_no_direct_table_DML()
    {
        var connectionString = Connection();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT " +
            "has_table_privilege('plantprocess_app','ppiq_meta.source_time_authorities','SELECT')," +
            "has_table_privilege('plantprocess_app','ppiq_meta.source_time_authorities','INSERT')," +
            "has_table_privilege('plantprocess_app','ppiq_meta.source_time_authorities','UPDATE')," +
            "has_table_privilege('plantprocess_app','ppiq_meta.source_time_authorities','DELETE')," +
            "has_function_privilege('plantprocess_app','ppiq_meta.declare_source_time_signal(uuid,text,text,text,text,bigint,text,bigint,bigint,text,timestamptz,timestamptz,uuid,text,text)','EXECUTE');", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1)); Assert.False(reader.GetBoolean(2)); Assert.False(reader.GetBoolean(3));
        Assert.True(reader.GetBoolean(4));
    }

    // ------------------------------------------------------------------ provenance

    private static PlantProcessDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    private static async Task RequireSingleActiveTenantAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand("SELECT count(*) FROM ppiq_meta.tenants WHERE is_active = true;", connection);
        var active = Convert.ToInt32(await count.ExecuteScalarAsync());
        Assert.True(active == 1,
            "The canonical baseline must carry exactly one active tenant for projection; saw " + active + ".");
    }

    private static async Task<Guid> ActiveTenantIdAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT id FROM ppiq_meta.tenants WHERE is_active = true ORDER BY id LIMIT 2;", connection);
        var ids = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) ids.Add(reader.GetGuid(0));
        Assert.Single(ids);
        return ids[0];
    }

    private static async Task DeclareProjectionSignalsAsync(string connectionString, Guid tenant, string sourceKey, params (string Signal, string Role, string Origin, long FixedTicks)[] signals)
    {
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var store = new SourceTimeAuthorityStore(dataSource);
        foreach (var signal in signals)
        {
            var request = new SourceTimeAuthorityDeclarationRequest(
                sourceKey, signal.Signal, signal.Role, signal.Origin, TimeSpan.FromTicks(signal.FixedTicks), null,
                TimeSpan.Zero, TimeSpan.Zero, "ResolutionIsHalfWidth",
                new DateTime(2020,1,1,0,0,0,DateTimeKind.Utc), null, null, "integration", null);
            var result = await store.DeclareAsync(tenant, request, CancellationToken.None);
            Assert.True(result.IsAccepted, result.Code + ": " + result.Detail);
        }
    }

    private static async Task<(Guid MappingId, Guid BatchId, Guid MaterialId, Guid ParameterId, string SourceKey)> ProjectionWorldAsync(
        string connectionString, string mappingJson, string rawJsonTemplate)
    {
        var tag = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        await using var db = NewContext(connectionString);

        var source = new SourceSystemDefinition("STA-SRC-" + tag, "Source time source " + tag, "File", isSynthetic: true);
        var site = new Site("STA-SITE-" + tag, "Source time site " + tag, isSynthetic: true);
        var parameter = new ParameterDefinition(
            parameterCode: "STA-PARAM-" + tag,
            parameterName: "Source time parameter " + tag,
            valueType: "Text",
            unitOfMeasure: null,
            parameterCategory: null,
            industryTemplate: null,
            isSynthetic: true);
        db.SourceSystemDefinitions.Add(source);
        db.Sites.Add(site);
        db.ParameterDefinitions.Add(parameter);
        await db.SaveChangesAsync();

        var material = new MaterialUnit("STA-UNIT-" + tag, "UNIT", site.Id, null, null, isSynthetic: true);
        db.MaterialUnits.Add(material);

        var objectName = "sta_rows_" + tag.ToLowerInvariant();
        var batch = new ImportBatch(source.Id, "STA-BATCH-" + tag, "File", isSynthetic: true, sourceObjectName: objectName);
        var mapping = new MappingDefinition(source.Id, "STA-MAP-" + tag, "Source time mapping " + tag, objectName,
            "ParameterObservation", mappingJson, isSynthetic: true);
        db.ImportBatches.Add(batch);
        db.MappingDefinitions.Add(mapping);
        await db.SaveChangesAsync();

        var raw = rawJsonTemplate
            .Replace("{MATERIAL}", material.Id.ToString(), StringComparison.Ordinal)
            .Replace("{PARAMETER}", parameter.Id.ToString(), StringComparison.Ordinal);
        db.StagingRecords.Add(new StagingRecord(batch.Id, objectName, 1, raw, isSynthetic: true));
        await db.SaveChangesAsync();

        return (mapping.Id, batch.Id, material.Id, parameter.Id, source.SourceSystemCode);
    }

    private static async Task<MappingExecutionRowResult> ExecuteOneAsync(string connectionString, Guid mappingId, Guid batchId)
    {
        await using var db = NewContext(connectionString);
        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var sourceTime = new SourceTimeAuthorityStore(dataSource);
        var service = new MappingExecutionService(
            db, new MappingRowProjector(db, sourceTime), new CanonicalIdentityResolver(db),
            NullLogger<MappingExecutionService>.Instance);

        var result = await service.ExecuteAsync(mappingId, batchId, 10, false, CancellationToken.None);
        Assert.False(result.IsFailure, result.Error?.ToString());
        return Assert.Single(result.Value!.Rows);
    }

    private const string ProvenanceMapping =
        "{\"MaterialUnitId\":\"m\",\"ParameterDefinitionId\":\"p\",\"ObservedAtUtc\":\"t\",\"TextValue\":\"v\"," +
        "\"SourceTimestampUtc\":\"src\",\"ServerTimestampUtc\":\"srv\"}";

    [SkippableFact]
    public async Task A_projected_observation_keeps_source_server_and_receipt_time()
    {
        var connectionString = Connection();
        await RequireSingleActiveTenantAsync(connectionString);

        var world = await ProjectionWorldAsync(connectionString, ProvenanceMapping,
            "{\"m\":\"{MATERIAL}\",\"p\":\"{PARAMETER}\",\"t\":\"2026-03-01 12:00:02\",\"v\":\"open\"," +
            "\"src\":\"2026-03-01T10:00:00Z\",\"srv\":\"2026-03-01T10:00:01Z\"}");
        var tenant = await ActiveTenantIdAsync(connectionString);
        await DeclareProjectionSignalsAsync(connectionString, tenant, world.SourceKey,
            ("t", "Effective", "DeclaredFixedOffset", TimeSpan.FromHours(2).Ticks),
            ("src", "SourceAsserted", "EmbeddedInValue", 0),
            ("srv", "SourceAsserted", "EmbeddedInValue", 0));

        var startedUtc = DateTime.UtcNow.AddMinutes(-1);
        var row = await ExecuteOneAsync(connectionString, world.MappingId, world.BatchId);

        Assert.Equal("Mapped", row.Status);
        Assert.NotNull(row.CanonicalEntityId);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand(
            "SELECT source_timestamp_utc, server_timestamp_utc, ingested_at_utc, observed_at_utc " +
            "FROM ppiq_plant.parameter_observations WHERE id = @id;", connection);
        query.Parameters.AddWithValue("id", row.CanonicalEntityId!.Value);
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        Assert.Equal(new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc), reader.GetDateTime(0));
        Assert.Equal(new DateTime(2026, 3, 1, 10, 0, 1, DateTimeKind.Utc), reader.GetDateTime(1));
        Assert.True(reader.GetDateTime(2) >= startedUtc, "The receipt time must be the ingestion instant, not a source value.");
        Assert.Equal(new DateTime(2026, 3, 1, 10, 0, 2, DateTimeKind.Utc), reader.GetDateTime(3));
        await reader.DisposeAsync();

        await using var lineage = new NpgsqlCommand(
            "SELECT sr.import_batch_id, sr.id, po.id FROM ppiq_staging.staging_records sr " +
            "JOIN ppiq_plant.parameter_observations po ON po.id = sr.canonical_entity_id " +
            "WHERE po.id = @id AND sr.canonical_entity_name = 'ParameterObservation';", connection);
        lineage.Parameters.AddWithValue("id", row.CanonicalEntityId!.Value);
        await using var lineageReader = await lineage.ExecuteReaderAsync();
        Assert.True(await lineageReader.ReadAsync());
        Assert.Equal(world.BatchId, lineageReader.GetGuid(0));
        Assert.NotEqual(Guid.Empty, lineageReader.GetGuid(1));
        Assert.Equal(row.CanonicalEntityId.Value, lineageReader.GetGuid(2));
    }

    [SkippableFact]
    public async Task A_malformed_supplied_source_timestamp_is_typed_evidence_not_a_silent_null()
    {
        var connectionString = Connection();
        await RequireSingleActiveTenantAsync(connectionString);

        var world = await ProjectionWorldAsync(connectionString, ProvenanceMapping,
            "{\"m\":\"{MATERIAL}\",\"p\":\"{PARAMETER}\",\"t\":\"2026-03-01T10:00:02Z\",\"v\":\"open\"," +
            "\"src\":\"not-a-time\",\"srv\":\"2026-03-01T10:00:01Z\"}");
        var tenant = await ActiveTenantIdAsync(connectionString);
        await DeclareProjectionSignalsAsync(connectionString, tenant, world.SourceKey,
            ("t", "Effective", "EmbeddedInValue", 0),
            ("src", "SourceAsserted", "EmbeddedInValue", 0),
            ("srv", "SourceAsserted", "EmbeddedInValue", 0));

        var row = await ExecuteOneAsync(connectionString, world.MappingId, world.BatchId);

        Assert.Equal("Quarantined", row.Status);
        Assert.Equal("PV02", row.ValidationCode);
    }

    [SkippableFact]
    public async Task A_missing_time_declaration_quarantines_as_PV02_with_ST01_evidence()
    {
        var connectionString = Connection();
        await RequireSingleActiveTenantAsync(connectionString);
        var world = await ProjectionWorldAsync(connectionString, ProvenanceMapping,
            "{\"m\":\"{MATERIAL}\",\"p\":\"{PARAMETER}\",\"t\":\"2026-03-01 12:00:02\",\"v\":\"open\"}");
        var row = await ExecuteOneAsync(connectionString, world.MappingId, world.BatchId);
        Assert.Equal("Quarantined", row.Status);
        Assert.Equal("PV02", row.ValidationCode);
        Assert.Contains("ST01", row.Message ?? string.Empty, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task An_observation_written_without_source_times_still_records_its_receipt_time()
    {
        var connectionString = Connection();
        var world = await ProjectionWorldAsync(connectionString, ProvenanceMapping,
            "{\"m\":\"{MATERIAL}\",\"p\":\"{PARAMETER}\",\"t\":\"2026-03-01T10:00:02Z\",\"v\":\"unused\"}");

        var startedUtc = DateTime.UtcNow.AddMinutes(-1);
        Guid id;
        await using (var db = NewContext(connectionString))
        {
            var observation = new ParameterObservation(
                world.MaterialId, world.ParameterId,
                new DateTime(2026, 3, 2, 8, 0, 0, DateTimeKind.Utc), isSynthetic: true, textValue: "direct");
            db.ParameterObservations.Add(observation);
            await db.SaveChangesAsync();
            id = observation.Id;
        }

        await using (var db = NewContext(connectionString))
        {
            var stored = await db.ParameterObservations
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.SourceTimestampUtc,
                    x.ServerTimestampUtc,
                    Ingested = EF.Property<DateTime>(x, "IngestedAtUtc")
                })
                .SingleAsync();

            Assert.Null(stored.SourceTimestampUtc);
            Assert.Null(stored.ServerTimestampUtc);
            Assert.True(stored.Ingested >= startedUtc);
        }
    }
}