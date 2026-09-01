using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

/// <summary>
/// PPIQ T-091. A throwaway canonical database for the clean-room round trip.
///
/// WHY IT IS BUILT BY TEMPLATE RATHER THAN BY REPLAYING THE CANONICAL PATH.
/// The canonical replay is the right proof for "can this schema be built from
/// zero", and T-090 already owns that gate and runs it. Paying for it again
/// here would add minutes to every T-091 run to re-prove someone else's
/// acceptance. CREATE DATABASE ... TEMPLATE copies the already-migrated
/// structure of the working database, which is the same schema by construction.
///
/// The copy also brings the working database's ROWS, which is exactly why the
/// round-trip test asserts an empty SEMANTIC baseline for its own codes rather
/// than assuming an empty database: what must be clean is the t091_ namespace,
/// not the installation.
/// </summary>
public sealed class DisposableDatabase : IAsyncDisposable
{
    private readonly string _adminConnectionString;

    private DisposableDatabase(string name, string adminConnectionString, string connectionString)
    {
        Name = name;
        _adminConnectionString = adminConnectionString;
        ConnectionString = connectionString;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public static async Task<DisposableDatabase> CreateAsync()
    {
        var host = Environment.GetEnvironmentVariable("PPIQ_TEST_PGHOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("PPIQ_TEST_PGPORT") ?? "5432";
        var source = Environment.GetEnvironmentVariable("PPIQ_TEST_PGDATABASE") ?? "ppiq_app";
        var user = Environment.GetEnvironmentVariable("PPIQ_TEST_PGUSER") ?? "ppiq_dev";
        var password = Environment.GetEnvironmentVariable("PPIQ_TEST_PGPASSWORD") ?? "ppiq_dev_local_only";

        var name = "ppiq_t091_" + Guid.NewGuid().ToString("N")[..12];
        var admin = $"Host={host};Port={port};Database=postgres;Username={user};Password={password}";
        var target = $"Host={host};Port={port};Database={name};Username={user};Password={password};Include Error Detail=true";

        if (source.Contains('"') || name.Contains('"'))
        {
            throw new InvalidOperationException("Database identifiers must not contain quote characters.");
        }

        // OUR OWN CONNECTIONS FIRST: the collection fixture holds pooled
        // connections to the source and this process must not be one of the
        // sessions the copy waits on.
        NpgsqlConnection.ClearAllPools();

        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();

            // TERMINATION ALONE IS A RACE. A prior revision terminated the
            // other sessions and then copied - and lost, with 55006: anything
            // running against the source (a dev API, an admin tool) reconnects
            // within milliseconds. PostgreSQL's own template databases use the
            // real mechanism: while ALLOW_CONNECTIONS is false, NOBODY can
            // attach, so terminate-then-copy has no window. The finally below
            // reopens the source even when the copy itself fails - a broken
            // gate must never leave the working database refusing logins.
            await using (var forbid = new NpgsqlCommand(
                "ALTER DATABASE \"" + source + "\" WITH ALLOW_CONNECTIONS false;", connection))
            {
                forbid.CommandTimeout = 120;
                await forbid.ExecuteNonQueryAsync();
            }

            try
            {
                await using (var terminate = new NpgsqlCommand(
                    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity " +
                    "WHERE datname = @source AND pid <> pg_backend_pid();", connection))
                {
                    terminate.CommandTimeout = 120;
                    terminate.Parameters.Add(new NpgsqlParameter("source", NpgsqlDbType.Text) { Value = source });
                    await terminate.ExecuteNonQueryAsync();
                }

                await using var create = new NpgsqlCommand(
                    "CREATE DATABASE \"" + name + "\" TEMPLATE \"" + source + "\";", connection);

                // Copying a provisioned database is minutes of work on a laptop
                // and the default command timeout is thirty seconds.
                create.CommandTimeout = 900;
                await create.ExecuteNonQueryAsync();
            }
            finally
            {
                await using var allow = new NpgsqlCommand(
                    "ALTER DATABASE \"" + source + "\" WITH ALLOW_CONNECTIONS true;", connection);
                allow.CommandTimeout = 120;
                await allow.ExecuteNonQueryAsync();
            }
        }

        var probe = new DisposableDatabase(name, admin, target);
        await probe.RemoveFixtureNamespaceAsync();
        return probe;
    }

    public PlantProcessDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PlantProcessDbContext(options);
    }

    public async Task<long> ScalarAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    public async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(sql, connection) { CommandTimeout = 300 };
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// The template copy inherits whatever t091_ rows the working database held
    /// when the copy was taken. They are removed so the clean-room phase starts
    /// from a genuinely empty semantic namespace rather than from a residue
    /// that would make an import look idempotent for the wrong reason.
    /// </summary>
    private async Task RemoveFixtureNamespaceAsync()
    {
        await ExecuteAsync(
            """
            WITH doomed AS (
                SELECT id FROM ppiq_meta.definition_store WHERE definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.definition_dependencies d USING doomed
             WHERE d.definition_id = doomed.id OR d.depends_on_definition_id = doomed.id;

            WITH doomed AS (
                SELECT id FROM ppiq_meta.definition_store WHERE definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.outcome_details od USING ppiq_meta.definition_versions v, doomed
             WHERE od.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT id FROM ppiq_meta.definition_store WHERE definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.widget_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT id FROM ppiq_meta.definition_store WHERE definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.analysis_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT id FROM ppiq_meta.definition_store WHERE definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.model_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT id FROM ppiq_meta.definition_store WHERE definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.log_rule_details d USING ppiq_meta.definition_versions v, doomed
             WHERE d.definition_version_id = v.id AND v.definition_id = doomed.id;

            WITH doomed AS (
                SELECT id FROM ppiq_meta.definition_store WHERE definition_code LIKE 't091_%')
            DELETE FROM ppiq_meta.definition_versions v USING doomed WHERE v.definition_id = doomed.id;

            DELETE FROM ppiq_meta.definition_store WHERE definition_code LIKE 't091_%';
            """);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();

        await using (var terminate = new NpgsqlCommand(
            "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = @name;", connection))
        {
            terminate.CommandTimeout = 120;
            terminate.Parameters.Add(new NpgsqlParameter("name", NpgsqlDbType.Text) { Value = Name });
            await terminate.ExecuteNonQueryAsync();
        }

        await using var drop = new NpgsqlCommand("DROP DATABASE IF EXISTS \"" + Name + "\";", connection)
        {
            CommandTimeout = 300
        };
        await drop.ExecuteNonQueryAsync();
    }
}
