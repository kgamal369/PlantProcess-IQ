using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.IntegrationTests.Definitions;

/// <summary>
/// PPIQ T-091. A throwaway canonical database for the clean-room round trip.
///
/// PPIQ T-252: the template source is the runner-owned disposable integration
/// database, never ppiq_app. No shared database is frozen, terminated or copied.
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
        // PPIQ T-252. The source is now a disposable database the runner provisioned
        // solely to be a template, which no test ever opens. That single change
        // removes the reason the freeze below ever existed.
        var sourceConnection = PlantProcess.TestSupport.TestDatabaseTarget.RequireTemplateSource();
        var sourceBuilder = new NpgsqlConnectionStringBuilder(sourceConnection);
        var source = sourceBuilder.Database ?? string.Empty;

        if (PlantProcess.TestSupport.TestDatabaseTarget.IsProtected(source))
        {
            throw new InvalidOperationException(
                "Refusing to clone the protected database '" + source + "'. A disposable fixture may only be built from a database this runner owns.");
        }

        var name = "ppiq_t091_" + Guid.NewGuid().ToString("N")[..12];
        var admin = new NpgsqlConnectionStringBuilder(sourceConnection) { Database = "postgres" }.ConnectionString;
        var target = new NpgsqlConnectionStringBuilder(sourceConnection) { Database = name, IncludeErrorDetail = true }.ConnectionString;

        if (source.Contains('"') || name.Contains('"'))
        {
            throw new InvalidOperationException("Database identifiers must not contain quote characters.");
        }

        // OUR OWN CONNECTIONS FIRST: this process must not be one of the sessions
        // the copy waits on. Nothing else connects to the source, because the
        // source is a database this run created for itself.
        NpgsqlConnection.ClearAllPools();

        // WHAT USED TO BE HERE, AND WHY IT IS GONE. This method used to forbid
        // logins on the source and terminate every other session on it, because
        // the source was ppiq_app - shared, live, and reconnected to within
        // milliseconds. A crash between the freeze and the reopen left the
        // developer database refusing logins, and a test
        // fixture has no authority over shared runtime state in the first place.
        // Making the source exclusively owned removes the race rather than
        // defending against it, so no freeze and no termination are needed.
        await using (var connection = new NpgsqlConnection(admin))
        {
            await connection.OpenAsync();

            await using var create = new NpgsqlCommand(
                "CREATE DATABASE \"" + name + "\" TEMPLATE \"" + source + "\";", connection);

            // Copying a provisioned database is minutes of work on a laptop and
            // the default command timeout is thirty seconds.
            create.CommandTimeout = 900;
            await create.ExecuteNonQueryAsync();
        }

        // OWNERSHIP BEGINS AT CREATION, NOT AT RETURN. The previous revision ran
        // RemoveFixtureNamespaceAsync before the caller ever held the object, so a
        // throw there orphaned a database nobody knew the name of.
        var probe = new DisposableDatabase(name, admin, target);
        try
        {
            await probe.RemoveFixtureNamespaceAsync();
        }
        catch
        {
            await probe.DisposeAsync();
            throw;
        }

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
