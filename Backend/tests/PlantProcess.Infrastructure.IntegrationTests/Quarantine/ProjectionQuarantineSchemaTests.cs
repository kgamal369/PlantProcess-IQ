using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.IntegrationTests.Quarantine;

/// <summary>
/// PPIQ T-099 live gates B, C, D and M.
///
/// The table contract is proven against real PostgreSQL, through real refusals.
/// A CHECK that is never violated in a test is a CHECK nobody has proven exists.
/// </summary>
[Collection(ProjectionQuarantineLiveCollection.Name)]
public class ProjectionQuarantineSchemaTests : IAsyncLifetime
{
    private QuarantineWorld _world = null!;
    private Guid _tenantId;
    private Guid _batchId;
    private Guid _mappingId;
    private Guid _stagingId;

    public async Task InitializeAsync()
    {
        _world = await QuarantineWorld.CreateAsync("schema");

        var created = await _world.NewMappingAsync(
            "T099SCHEMA", "schema_rows", "MaterialUnit",
            "{\"MaterialCode\":\"code\",\"MaterialUnitType\":\"type\",\"SiteCode\":\"site\"}");
        _batchId = created.BatchId;
        _mappingId = created.MappingId;

        await _world.AddRowsAsync(_batchId, "schema_rows", "{\"code\":\"S1\",\"type\":\"COIL\",\"site\":\"T099SITE\"}");

        await using var db = _world.NewContext();
        _stagingId = await db.StagingRecords.Where(x => x.ImportBatchId == _batchId).Select(x => x.Id).FirstAsync();

        await using var connection = new NpgsqlConnection(_world.ConnectionString);
        await connection.OpenAsync();
        await using var tenant = new NpgsqlCommand(
            "SELECT id FROM ppiq_meta.tenants WHERE tenant_code = @c;", connection);
        tenant.Parameters.AddWithValue("c", QuarantineWorld.TenantACode);
        _tenantId = (Guid)(await tenant.ExecuteScalarAsync())!;
    }

    public Task DisposeAsync() => _world.DisposeAsync().AsTask();

    private async Task<Guid> InsertAsync(
        string code = "PV01",
        int attemptCount = 1,
        int rowNumber = 1,
        string state = "Open",
        DateTime? resolvedAtUtc = null,
        Guid? resolvedCanonicalId = null,
        Guid? importBatchId = null,
        Guid? stagingRecordId = null,
        Guid? mappingDefinitionId = null,
        Guid? tenantId = null,
        string mappingVersion = "v1")
    {
        await using var connection = new NpgsqlConnection(_world.ConnectionString);
        await connection.OpenAsync();
        await using var insert = new NpgsqlCommand(
            @"INSERT INTO ppiq_staging.projection_quarantine
                (validation_code, detail, suggested_correction, import_batch_id, staging_record_id,
                 staging_row_number, source_object_name, mapping_definition_id, mapping_version,
                 target_entity_name, tenant_id, state, attempt_count, resolved_at_utc, resolved_canonical_id)
              VALUES (@code, 'detail', 'correction', @batch, @staging, @row, 'schema_rows', @mapping, @version,
                      'MaterialUnit', @tenant, @state, @attempt, @resolvedAt, @resolvedId)
              RETURNING id;", connection);
        insert.Parameters.AddWithValue("code", code);
        insert.Parameters.AddWithValue("batch", importBatchId ?? _batchId);
        insert.Parameters.AddWithValue("staging", stagingRecordId ?? _stagingId);
        insert.Parameters.AddWithValue("row", rowNumber);
        insert.Parameters.AddWithValue("mapping", mappingDefinitionId ?? _mappingId);
        insert.Parameters.AddWithValue("version", mappingVersion);
        insert.Parameters.AddWithValue("tenant", tenantId ?? _tenantId);
        insert.Parameters.AddWithValue("state", state);
        insert.Parameters.AddWithValue("attempt", attemptCount);
        insert.Parameters.AddWithValue("resolvedAt", (object?)resolvedAtUtc ?? DBNull.Value);
        insert.Parameters.AddWithValue("resolvedId", (object?)resolvedCanonicalId ?? DBNull.Value);

        return (Guid)(await insert.ExecuteScalarAsync())!;
    }

    // ---------------------------------------------------------------- Gate B
    [Fact]
    public async Task Gate_B_table_carries_the_exact_intended_columns_including_base_entity_parity()
    {
        await using var connection = new NpgsqlConnection(_world.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            @"SELECT column_name FROM information_schema.columns
              WHERE table_schema = 'ppiq_staging' AND table_name = 'projection_quarantine';", connection);

        var found = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                found.Add(reader.GetString(0));
            }
        }

        var expected = new[]
        {
            // BaseEntity storage parity. The EF entity inherits every one of these.
            "id", "created_at_utc", "updated_at_utc", "is_synthetic", "source_system",
            "source_record_id", "is_deleted", "deleted_at_utc", "deleted_reason",
            // T-099 quarantine fields.
            "validation_code", "detail", "offending_value", "suggested_correction",
            "import_batch_id", "staging_record_id", "staging_row_number", "source_object_name",
            "mapping_definition_id", "mapping_version", "target_entity_name", "tenant_id",
            "state", "attempt_count", "last_attempt_at_utc", "resolved_at_utc", "resolved_canonical_id"
        };

        // EXACT, not a subset. A column nobody expected is as much a contract
        // break as a missing one: it would be storage the EF entity does not
        // know about and no test would ever notice.
        Assert.Equal(
            expected.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            found.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task Gate_B_attempt_count_below_one_is_refused()
    {
        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(attemptCount: 0));
    }

    [Fact]
    public async Task Gate_B_a_non_positive_row_number_is_refused()
    {
        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(rowNumber: 0));
    }

    // The coherence rule is a biconditional, so the gate proves every form of
    // it. Representative samples would leave half the truth table unasserted.
    [Theory]
    [InlineData("Open", true, false)]
    [InlineData("Open", false, true)]
    [InlineData("Open", true, true)]
    [InlineData("Resolved", false, false)]
    [InlineData("Resolved", true, false)]
    [InlineData("Resolved", false, true)]
    public async Task Gate_B_every_incoherent_resolution_form_is_refused(string state, bool time, bool canonical)
    {
        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(
            state: state,
            resolvedAtUtc: time ? DateTime.UtcNow : null,
            resolvedCanonicalId: canonical ? Guid.NewGuid() : null,
            mappingVersion: "v-coh-" + state + "-" + time + "-" + canonical));
    }

    [Fact]
    public async Task Gate_B_open_without_any_resolution_is_accepted()
    {
        var id = await InsertAsync(state: "Open", mappingVersion: "v-coh-open-legal");
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Gate_B_resolved_with_time_and_canonical_identity_is_accepted()
    {
        var id = await InsertAsync(
            state: "Resolved", resolvedAtUtc: DateTime.UtcNow, resolvedCanonicalId: Guid.NewGuid(),
            mappingVersion: "v-coh-resolved-legal");
        Assert.NotEqual(Guid.Empty, id);
    }

    // ---------------------------------------------------------------- Gate C
    [Theory]
    [InlineData("PV01")]
    [InlineData("PV02")]
    [InlineData("PV03")]
    [InlineData("PV04")]
    [InlineData("PV05")]
    [InlineData("PV06")]
    [InlineData("PV07")]
    [InlineData("PV08")]
    public async Task Gate_C_the_eight_T099_codes_are_accepted(string code)
    {
        var id = await InsertAsync(code: code, rowNumber: 1, mappingVersion: "v-" + code);
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task Gate_C_PV09_is_refused_by_the_database_until_T100()
    {
        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(code: "PV09"));
    }

    // ---------------------------------------------------------------- Gate D
    [Fact]
    public async Task Gate_D_a_fabricated_import_batch_is_refused()
    {
        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(importBatchId: Guid.NewGuid()));
    }

    [Fact]
    public async Task Gate_D_a_fabricated_staging_record_is_refused()
    {
        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(stagingRecordId: Guid.NewGuid()));
    }

    [Fact]
    public async Task Gate_D_a_fabricated_mapping_definition_is_refused()
    {
        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(mappingDefinitionId: Guid.NewGuid()));
    }

    [Fact]
    public async Task Gate_D_a_fabricated_tenant_is_refused()
    {
        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(tenantId: Guid.NewGuid()));
    }

    [Theory]
    [InlineData("fk_projection_quarantine_import_batch")]
    [InlineData("fk_projection_quarantine_staging_record")]
    [InlineData("fk_projection_quarantine_mapping_definition")]
    [InlineData("fk_projection_quarantine_tenant")]
    public async Task Gate_D_every_lineage_foreign_key_refuses_to_cascade_a_delete(string constraintName)
    {
        await using var connection = new NpgsqlConnection(_world.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT confdeltype FROM pg_constraint WHERE conname = @name AND contype = 'f';", connection);
        command.Parameters.AddWithValue("name", constraintName);

        var action = await command.ExecuteScalarAsync();

        // 'r' is RESTRICT. Existence of a reference proves nothing about what a
        // delete would do to the evidence hanging off it.
        Assert.NotNull(action);
        Assert.Equal('r', (char)action!);
    }

    // ---------------------------------------------------------------- Gate M
    [Fact]
    public async Task Gate_M_two_open_records_for_one_row_and_version_cannot_coexist_and_history_survives_resolution()
    {
        const string version = "v-uniqueness";

        var first = await InsertAsync(mappingVersion: version);

        // The active queue is protected: a second Open episode for the same
        // staged row under the same producing version is refused by the index.
        await Assert.ThrowsAsync<PostgresException>(() => InsertAsync(mappingVersion: version));

        await using (var db = _world.NewContext())
        {
            var record = await db.ProjectionQuarantineRecords.FirstAsync(x => x.Id == first);
            record.MarkResolved(Guid.NewGuid());
            await db.SaveChangesAsync();
        }

        // A later lawful episode does not require deleting or rewriting the
        // resolved record: evidence is immutable, only the queue is exclusive.
        var second = await InsertAsync(mappingVersion: version);
        Assert.NotEqual(first, second);

        await using var verify = _world.NewContext();
        var all = await verify.ProjectionQuarantineRecords
            .Where(x => x.MappingVersion == version)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, all.Count);
        Assert.Equal(ProjectionQuarantineRecord.StateResolved, all[0].State);
        Assert.Equal(ProjectionQuarantineRecord.StateOpen, all[1].State);
        Assert.NotNull(all[0].ResolvedCanonicalId);
    }
}
