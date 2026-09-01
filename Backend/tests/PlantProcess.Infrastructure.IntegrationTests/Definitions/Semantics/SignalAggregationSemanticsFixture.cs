using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions.Semantics;

/// <summary>
/// PPIQ T-210. Schema-aware neutral fixture, targeting the disposable probe.
///
/// TARGETS THE PROBE, BY CONTRACT. The pack injects PPIQ_TEST_PGDATABASE and
/// PPIQ_T210_TEST_DATABASE; the fixture refuses to run unless both agree and
/// name a T-210 probe. Acceptance never touches the long-lived database.
///
/// SCHEMA-AWARE, ONCE. The table shapes are read from information_schema and
/// pg_constraint at initialisation: required columns, defaults, and which
/// columns are foreign keys and to what. Neutral rows are built from that
/// description. A required column the fixture cannot satisfy fails with ONE
/// error naming table, column, type, nullability, default and FK target.
///
/// OWNS ITS TENANTS. The probe carries no provisioned identity, and T-210's
/// two-tenant acceptance needs two real rows in ppiq_meta.tenants, so the
/// fixture creates t210_tenant_a and t210_tenant_b and removes them after.
/// </summary>
public sealed class T210SemanticsFixture : IAsyncLifetime
{
    public const string Prefix = "t210_";

    private readonly List<(string Table, Guid Id)> _created = new();
    private string _connectionString = string.Empty;

    public Guid TenantA { get; private set; }
    public Guid TenantB { get; private set; }
    public string ConnectionString => _connectionString;
    public string DatabaseName { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var host = Environment.GetEnvironmentVariable("PPIQ_TEST_PGHOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("PPIQ_TEST_PGPORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("PPIQ_TEST_PGDATABASE");
        var expected = Environment.GetEnvironmentVariable("PPIQ_T210_TEST_DATABASE");
        var user = Environment.GetEnvironmentVariable("PPIQ_TEST_PGUSER") ?? "ppiq_dev";
        var password = Environment.GetEnvironmentVariable("PPIQ_TEST_PGPASSWORD") ?? "ppiq_dev_local_only";

        if (string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(expected) ||
            !string.Equals(database, expected, StringComparison.Ordinal) ||
            !database.StartsWith("ppiq_t210_probe", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "T-210 acceptance must target a disposable probe: PPIQ_TEST_PGDATABASE='" + database +
                "', PPIQ_T210_TEST_DATABASE='" + expected + "'. Refusing to run against anything else.");
        }

        DatabaseName = database;
        _connectionString = $"Host={host};Port={port};Database={database};Username={user};Password={password};Include Error Detail=true";

        TenantA = await CreateTenantAsync("t210_tenant_a");
        TenantB = await CreateTenantAsync("t210_tenant_b");
    }

    public async Task DisposeAsync()
    {
        await ResetAsync();
    }

    public PlantProcessDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new PlantProcessDbContext(options);
    }

    /// <summary>A parameter owned by the tenant, or unowned (tenant null) for the claim gates.</summary>
    public Task<Guid> CreateParameterAsync(string suffix, Guid? tenantId)
    {
        var overrides = new Dictionary<string, (NpgsqlDbType Type, object Value)>(StringComparer.Ordinal)
        {
            ["parameter_code"] = (NpgsqlDbType.Text, Prefix + suffix),
            ["tenant_id"] = (NpgsqlDbType.Uuid, tenantId.HasValue ? tenantId.Value : DBNull.Value),
        };
        return InsertNeutralRowAsync("parameter_definitions", overrides, tenantId ?? TenantA, 0);
    }

    public Task<Guid> CreateParameterAsync(string suffix) => CreateParameterAsync(suffix, TenantA);

    /// <summary>A KPI binding for the parameter under the given tenant.</summary>
    public async Task<Guid> CreateBindingAsync(Guid parameterId, Guid tenantId, string? aggregationOverride, string? weightOverride)
    {
        var parameterColumn = await ForeignKeyColumnAsync("kpi_parameter_bindings", "parameter_definitions")
            ?? throw new InvalidOperationException("kpi_parameter_bindings has no foreign key to parameter_definitions.");

        var overrides = new Dictionary<string, (NpgsqlDbType Type, object Value)>(StringComparer.Ordinal)
        {
            [parameterColumn] = (NpgsqlDbType.Uuid, parameterId),
            ["tenant_id"] = (NpgsqlDbType.Uuid, tenantId),
        };
        if (aggregationOverride is not null) { overrides["aggregation_kind_override"] = (NpgsqlDbType.Varchar, aggregationOverride); }
        if (weightOverride is not null) { overrides["weight_basis_override"] = (NpgsqlDbType.Varchar, weightOverride); }

        return await InsertNeutralRowAsync("kpi_parameter_bindings", overrides, tenantId, 0);
    }

    public async Task<long> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) { command.Parameters.AddWithValue(name, value); }
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public async Task<Guid?> ReadTenantOfParameterAsync(Guid parameterId)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT tenant_id FROM ppiq_meta.parameter_definitions WHERE id = @id;", connection);
        command.Parameters.AddWithValue("id", parameterId);
        var value = await command.ExecuteScalarAsync();
        return value is Guid g ? g : null;
    }

    // ------------------------------------------------------------ internals --

    private async Task<Guid> CreateTenantAsync(string code)
    {
        // The tenant authority's shape is known from the committed T-090
        // measurement: id, tenant_code, display_name, environment_name,
        // is_active, created_at_utc.
        var overrides = new Dictionary<string, (NpgsqlDbType Type, object Value)>(StringComparer.Ordinal)
        {
            ["tenant_code"] = (NpgsqlDbType.Text, code),
            ["display_name"] = (NpgsqlDbType.Text, code),
            ["environment_name"] = (NpgsqlDbType.Text, "t210"),
            ["is_active"] = (NpgsqlDbType.Boolean, true),
        };
        return await InsertNeutralRowAsync("tenants", overrides, Guid.Empty, 0);
    }

    private sealed record ColumnShape(string Name, string Type, bool Required, bool HasDefault, string? FkTable);

    private async Task<List<ColumnShape>> DescribeAsync(NpgsqlConnection connection, string table)
    {
        var shapes = new List<ColumnShape>();
        await using var describe = new NpgsqlCommand(
            """
            SELECT c.column_name, c.data_type, c.is_nullable = 'NO', c.column_default IS NOT NULL,
                   (SELECT cl.relname FROM pg_constraint k
                      JOIN pg_attribute a ON a.attrelid = k.conrelid AND a.attnum = ANY (k.conkey)
                      JOIN pg_class cl ON cl.oid = k.confrelid
                     WHERE k.contype = 'f' AND k.conrelid = ('ppiq_meta.' || @table)::regclass
                       AND a.attname = c.column_name LIMIT 1) AS fk_table
              FROM information_schema.columns c
             WHERE c.table_schema = 'ppiq_meta' AND c.table_name = @table
             ORDER BY c.ordinal_position;
            """, connection);
        describe.Parameters.AddWithValue("table", table);

        await using var reader = await describe.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            shapes.Add(new ColumnShape(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return shapes;
    }

    private async Task<string?> ForeignKeyColumnAsync(string table, string target)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var fk = new NpgsqlCommand(
            """
            SELECT a.attname FROM pg_constraint k
              JOIN pg_attribute a ON a.attrelid = k.conrelid AND a.attnum = ANY (k.conkey)
             WHERE k.contype = 'f'
               AND k.conrelid = ('ppiq_meta.' || @table)::regclass
               AND k.confrelid = ('ppiq_meta.' || @target)::regclass
             LIMIT 1;
            """, connection);
        fk.Parameters.AddWithValue("table", table);
        fk.Parameters.AddWithValue("target", target);
        return (await fk.ExecuteScalarAsync()) as string;
    }

    /// <summary>
    /// Builds one neutral row for a table from its measured shape. Required
    /// foreign keys to other tables are satisfied by creating a neutral row in
    /// the target (one level deep, then two), so a binding's mandatory
    /// references are real rows rather than invented uuids.
    /// </summary>
    private async Task<Guid> InsertNeutralRowAsync(
        string table,
        Dictionary<string, (NpgsqlDbType Type, object Value)> overrides,
        Guid tenantId,
        int depth)
    {
        if (depth > 2)
        {
            throw new InvalidOperationException("T210SemanticsFixture: foreign-key chain deeper than two levels while creating " + table);
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var shape = await DescribeAsync(connection, table);
        if (!shape.Any(c => c.Name == "id"))
        {
            throw new InvalidOperationException("T210SemanticsFixture: ppiq_meta." + table + " has no id column.");
        }

        var id = Guid.NewGuid();
        var names = new List<string>();
        var values = new List<string>();
        await using var insert = new NpgsqlCommand { Connection = connection };

        void Bind(string name, NpgsqlDbType type, object value)
        {
            names.Add(name);
            values.Add("@" + name);
            insert.Parameters.Add(new NpgsqlParameter(name, type) { Value = value });
        }

        Bind("id", NpgsqlDbType.Uuid, id);
        foreach (var pair in overrides) { Bind(pair.Key, pair.Value.Type, pair.Value.Value); }

        foreach (var column in shape)
        {
            if (names.Contains(column.Name)) { continue; }
            if (!column.Required || column.HasDefault) { continue; }

            if (column.Name == "tenant_id")
            {
                Bind("tenant_id", NpgsqlDbType.Uuid, tenantId == Guid.Empty ? (object)DBNull.Value : tenantId);
                continue;
            }

            if (column.FkTable is not null)
            {
                var target = await InsertNeutralRowAsync(column.FkTable,
                    new Dictionary<string, (NpgsqlDbType, object)>(StringComparer.Ordinal), tenantId, depth + 1);
                Bind(column.Name, NpgsqlDbType.Uuid, target);
                continue;
            }

            var isCodeLike = column.Name.Contains("code", StringComparison.OrdinalIgnoreCase) ||
                             column.Name.Contains("name", StringComparison.OrdinalIgnoreCase) ||
                             column.Name.Contains("key", StringComparison.OrdinalIgnoreCase);

            switch (column.Type)
            {
                case "character varying":
                case "text":
                case "character":
                    Bind(column.Name, NpgsqlDbType.Text, isCodeLike ? Prefix + table + "_" + id.ToString("N")[..8] : "t210");
                    break;
                case "uuid": Bind(column.Name, NpgsqlDbType.Uuid, Guid.NewGuid()); break;
                case "integer": Bind(column.Name, NpgsqlDbType.Integer, 0); break;
                case "bigint": Bind(column.Name, NpgsqlDbType.Bigint, 0L); break;
                case "smallint": Bind(column.Name, NpgsqlDbType.Smallint, (short)0); break;
                case "numeric":
                case "double precision":
                case "real": Bind(column.Name, NpgsqlDbType.Double, 0.0); break;
                case "boolean": Bind(column.Name, NpgsqlDbType.Boolean, false); break;
                case "timestamp with time zone":
                case "timestamp without time zone": Bind(column.Name, NpgsqlDbType.TimestampTz, DateTime.UtcNow); break;
                case "date": Bind(column.Name, NpgsqlDbType.Date, DateTime.UtcNow.Date); break;
                case "jsonb":
                case "json": Bind(column.Name, NpgsqlDbType.Jsonb, "{}"); break;
                default:
                    throw new InvalidOperationException(
                        "T210SemanticsFixture cannot satisfy required column: table=ppiq_meta." + table +
                        " column=" + column.Name + " type=" + column.Type + " nullable=false hasDefault=false" +
                        " fk=" + (column.FkTable ?? "none") + " tenant=" + tenantId);
            }
        }

        insert.CommandText = "INSERT INTO ppiq_meta." + table + " (" + string.Join(", ", names) + ") VALUES (" +
                             string.Join(", ", values) + ");";
        await insert.ExecuteNonQueryAsync();

        _created.Add((table, id));
        return id;
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Reverse creation order: children were always created after (and
        // because of) their parents, so deleting newest-first respects every
        // foreign key. Rows already deleted (cascade) are simply absent.
        foreach (var (table, id) in Enumerable.Reverse(_created))
        {
            if (table == "parameter_definitions")
            {
                await using var history = new NpgsqlCommand(
                    "DELETE FROM ppiq_meta.parameter_signal_semantics_history WHERE parameter_definition_id = @id;", connection);
                history.Parameters.AddWithValue("id", id);
                await history.ExecuteNonQueryAsync();
            }

            await using var command = new NpgsqlCommand("DELETE FROM ppiq_meta." + table + " WHERE id = @id;", connection);
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync();
        }

        _created.Clear();
    }
}

[CollectionDefinition("T210Semantics")]
public sealed class T210SemanticsCollection : ICollectionFixture<T210SemanticsFixture>
{
}
