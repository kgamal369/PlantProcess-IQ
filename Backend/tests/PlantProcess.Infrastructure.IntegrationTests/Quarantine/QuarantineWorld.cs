using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Application.Integration.Services.Mapping;
using PlantProcess.Domain.Entities.Configuration;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.IntegrationTests.Quarantine;

/// <summary>
/// PPIQ T-099. The governed fixture world for the live quarantine proofs.
///
/// Everything here is REAL: a real PostgreSQL database built from the canonical
/// path, real EF entities through their own constructors, the real projector and
/// the real identity authority. Nothing is mocked, because the thing under proof
/// is exactly what the database and the product do together.
///
/// TENANT COUNT IS PART OF THE CONTRACT. The batch execution path resolves
/// ResolveTenantAsync(null), which refuses when two tenants are active. Every
/// world starts with exactly one active tenant; only the isolation fixture adds
/// a second, and only after its quarantine evidence already exists.
/// </summary>
public sealed class QuarantineWorld : IAsyncDisposable
{
    public const string TenantACode = "T099A";
    public const string TenantBCode = "T099B";

    private readonly QuarantineDatabase _database;

    private QuarantineWorld(QuarantineDatabase database)
    {
        _database = database;
    }

    public string ConnectionString => _database.ConnectionString;

    public string DatabaseName => _database.Name;

    public Guid SourceSystemId { get; private set; }

    public Guid SiteId { get; private set; }

    public Guid ParameterDefinitionId { get; private set; }

    public static async Task<QuarantineWorld> CreateAsync(string suffix)
    {
        var db = await QuarantineDatabase.CreateAsync(suffix);
        var world = new QuarantineWorld(db);
        await world.SeedAsync();
        return world;
    }

    public PlantProcessDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PlantProcessDbContext(options);
    }

    public MappingExecutionService NewExecutionService(PlantProcessDbContext db) =>
        new(db, new MappingRowProjector(db), new CanonicalIdentityResolver(db),
            NullLogger<MappingExecutionService>.Instance);

    public ProjectionQuarantineReprocessService NewReprocessService(PlantProcessDbContext db) =>
        new(db, new MappingRowProjector(db), new CanonicalIdentityResolver(db),
            NullLogger<ProjectionQuarantineReprocessService>.Instance);

    public IMappingRowProjector NewProjector(PlantProcessDbContext db) => new MappingRowProjector(db);

    // ------------------------------------------------------------------
    // Tenancy. Written through SQL because tenants are an authority this
    // product reads, not an EF entity it owns.
    // ------------------------------------------------------------------
    public async Task<Guid> EnsureTenantAsync(string code, string displayName)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var insert = new NpgsqlCommand(
            @"INSERT INTO ppiq_meta.tenants (id, tenant_code, display_name, is_active)
              VALUES (@id, @code, @name, true)
              ON CONFLICT (tenant_code) DO UPDATE SET is_active = true
              RETURNING id;", connection);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("code", code);
        insert.Parameters.AddWithValue("name", displayName);

        var value = await insert.ExecuteScalarAsync();
        return (Guid)value!;
    }

    public string BaselineTenantCode { get; private set; } = string.Empty;

    public Guid BaselineTenantId { get; private set; }

    public async Task<IReadOnlyList<(Guid Id, string Code)>> ActiveTenantsAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var query = new NpgsqlCommand(
            "SELECT id, tenant_code FROM ppiq_meta.tenants WHERE is_active = true ORDER BY tenant_code;", connection);

        var rows = new List<(Guid, string)>();
        await using var reader = await query.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add((reader.GetGuid(0), reader.GetString(1)));
        }

        return rows;
    }

    public async Task SetTenantActiveAsync(Guid tenantId, bool isActive)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var update = new NpgsqlCommand(
            "UPDATE ppiq_meta.tenants SET is_active = @active WHERE id = @id;", connection);
        update.Parameters.AddWithValue("active", isActive);
        update.Parameters.AddWithValue("id", tenantId);
        await update.ExecuteNonQueryAsync();
    }

    public async Task<int> ActiveTenantCountAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand(
            "SELECT count(*) FROM ppiq_meta.tenants WHERE is_active = true;", connection);
        return Convert.ToInt32(await count.ExecuteScalarAsync());
    }

    public async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    // ------------------------------------------------------------------
    // Governed baseline every world shares.
    // ------------------------------------------------------------------
    private async Task SeedAsync()
    {
        // MEASURED, NOT ASSUMED. Script 301 on the canonical path inserts the
        // active compatibility tenant 'default-demo', so a fresh canonical child
        // already carries exactly one active tenant. An earlier draft of this
        // fixture assumed zero and would have left two active, which makes
        // ResolveTenantAsync(null) refuse every Execute and Preview in gates E-K.
        //
        // The baseline is DEACTIVATED, never deleted or renamed: it is canonical
        // truth this fixture borrows a seat from, not drift to be cleaned up.
        var baseline = await ActiveTenantsAsync();
        if (baseline.Count != 1)
        {
            throw new InvalidOperationException(
                "The fresh canonical child carries " + baseline.Count +
                " active tenants; exactly one baseline tenant was expected. Codes: " +
                string.Join(", ", baseline.Select(x => x.Code)) +
                ". That is a canonical-baseline contradiction, not a fixture condition to work around.");
        }

        BaselineTenantCode = baseline[0].Code;
        BaselineTenantId = baseline[0].Id;
        await SetTenantActiveAsync(BaselineTenantId, false);

        await EnsureTenantAsync(TenantACode, "T-099 tenant A");

        var afterSeed = await ActiveTenantCountAsync();
        if (afterSeed != 1)
        {
            throw new InvalidOperationException(
                "Expected exactly one active tenant after seeding, saw " + afterSeed + ".");
        }

        await using var db = NewContext();

        var source = new SourceSystemDefinition("T099SRC", "T-099 source", "File", isSynthetic: true);
        db.SourceSystemDefinitions.Add(source);

        var site = new Site("T099SITE", "T-099 site", isSynthetic: true);
        db.Sites.Add(site);

        var template = new IndustryTemplate("T099TPL", "T-099 template", "RailSteel", isSynthetic: true);
        db.IndustryTemplates.Add(template);

        await db.SaveChangesAsync();

        // The governed vocabulary PV07 consults. One active entry is what makes
        // the catalogue authoritative; an empty catalogue governs nothing.
        db.MaterialUnitTypeDefinitions.Add(new MaterialUnitTypeDefinition(
            template.Id, "COIL", "Coil", isSynthetic: true));

        // The governed unit authority PV08 consults.
        var parameter = new ParameterDefinition(
            parameterCode: "T099TEMP",
            parameterName: "T-099 temperature",
            valueType: "Numeric",
            unitOfMeasure: "degC",
            parameterCategory: null,
            industryTemplate: null,
            isSynthetic: true);
        db.ParameterDefinitions.Add(parameter);

        await db.SaveChangesAsync();

        SourceSystemId = source.Id;
        SiteId = site.Id;
        ParameterDefinitionId = parameter.Id;
    }

    // ------------------------------------------------------------------
    // Fixture builders.
    // ------------------------------------------------------------------
    public async Task<(Guid BatchId, Guid MappingId)> NewMappingAsync(
        string code,
        string sourceObjectName,
        string targetEntityName,
        string mappingJson,
        string mappingVersion = "v1")
    {
        await using var db = NewContext();

        var batch = new ImportBatch(SourceSystemId, code + "-BATCH", "File", isSynthetic: true, sourceObjectName: sourceObjectName);
        db.ImportBatches.Add(batch);

        var mapping = new MappingDefinition(
            SourceSystemId, code, code + " mapping", sourceObjectName, targetEntityName,
            mappingJson, isSynthetic: true, mappingVersion: mappingVersion);
        db.MappingDefinitions.Add(mapping);

        await db.SaveChangesAsync();
        return (batch.Id, mapping.Id);
    }

    public async Task AddRowsAsync(Guid batchId, string sourceObjectName, params string[] rawJson)
    {
        await using var db = NewContext();
        var row = 1;
        foreach (var json in rawJson)
        {
            db.StagingRecords.Add(new StagingRecord(batchId, sourceObjectName, row, json, isSynthetic: true));
            row++;
        }

        await db.SaveChangesAsync();
    }

    public async Task<MaterialUnit> AddMaterialAsync(string materialCode)
    {
        await using var db = NewContext();
        var material = new MaterialUnit(materialCode, "COIL", SiteId, null, null, isSynthetic: true);
        db.MaterialUnits.Add(material);
        await db.SaveChangesAsync();
        return material;
    }

    public ValueTask DisposeAsync() => _database.DisposeAsync();
}
