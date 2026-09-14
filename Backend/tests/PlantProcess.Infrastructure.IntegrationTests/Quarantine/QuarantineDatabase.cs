using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using PlantProcess.TestSupport;

namespace PlantProcess.Infrastructure.IntegrationTests.Quarantine;

/// <summary>
/// A run-owned disposable child of the already-certified canonical template.
/// Names are fixed by the runner before creation. Administrative CREATE/DROP
/// commands have their own finite budgets; target query budgets do not cover
/// administrative commands on postgres. Teardown failures remain test failures.
/// </summary>
public sealed class QuarantineDatabase : IAsyncDisposable
{
    public const string RunNonceVariable = "PPIQ_T099_RUN_NONCE";
    private const string LifecycleLogVariable = "PPIQ_T099_LIFECYCLE_LOG";
    private const int AdminConnectTimeoutSeconds = 60;
    private const int AdminCommandTimeoutSeconds = 180;
    private readonly string _adminConnectionString;
    private bool _disposed;

    private QuarantineDatabase(string name, string adminConnectionString, string connectionString)
    {
        Name = name;
        _adminConnectionString = adminConnectionString;
        ConnectionString = connectionString;
    }

    public string Name { get; }
    public string ConnectionString { get; }

    private static string RunNonce()
    {
        var nonce = Environment.GetEnvironmentVariable(RunNonceVariable);
        if (string.IsNullOrWhiteSpace(nonce) || !Regex.IsMatch(nonce, "^[a-z0-9_]{1,24}$"))
            throw new InvalidOperationException(RunNonceVariable + " must contain 1..24 lowercase identifier characters.");
        return nonce;
    }

    public static string NameFor(string suffix)
    {
        if (suffix is not ("schema" or "classify" or "reprocess" or "tenant"))
            throw new InvalidOperationException("Database suffix is not in the runner-owned child manifest.");
        var name = "ppiq_acceptance_t099_" + RunNonce() + "_" + suffix;
        if (name.Length > 63 || TestDatabaseTarget.IsProtected(name))
            throw new InvalidOperationException("Refusing an invalid or protected disposable database name.");
        return name;
    }

    private static void RequireOwnedChild(string name)
    {
        foreach (var suffix in new[] { "schema", "classify", "reprocess", "tenant" })
            if (string.Equals(name, NameFor(suffix), StringComparison.Ordinal))
                return;
        throw new InvalidOperationException("Refusing DDL outside this runner's exact child database manifest.");
    }

    private static void Log(string phase, string database, string outcome, Stopwatch watch,
        int? backendPid = null, string? errorType = null)
    {
        var line = JsonSerializer.Serialize(new
        {
            utc = DateTimeOffset.UtcNow,
            phase, database, outcome,
            elapsedMs = watch.ElapsedMilliseconds,
            backendPid, errorType,
            commandTimeoutSeconds = AdminCommandTimeoutSeconds
        });
        Console.Error.WriteLine("QUARANTINE_DB " + line);
        var path = Environment.GetEnvironmentVariable(LifecycleLogVariable);
        if (!string.IsNullOrWhiteSpace(path))
            File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
    }

    private static async Task<bool> ExistsAsync(NpgsqlConnection admin, string name)
    {
        await using var query = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_database WHERE datname = @name);", admin)
        { CommandTimeout = 30 };
        query.Parameters.AddWithValue("name", name);
        var result = await query.ExecuteScalarAsync();
        return result is bool exists
            ? exists
            : throw new InvalidOperationException("Database existence verification returned no boolean result.");
    }

    private static async Task DropAndVerifyAsync(NpgsqlConnection admin, string name, string phase)
    {
        RequireOwnedChild(name);
        var watch = Stopwatch.StartNew();
        Log(phase, name, "begin", watch, admin.ProcessID);
        try
        {
            await using var drop = new NpgsqlCommand(
                "DROP DATABASE IF EXISTS \"" + name + "\" WITH (FORCE);", admin)
            { CommandTimeout = AdminCommandTimeoutSeconds };
            await drop.ExecuteNonQueryAsync();
            if (await ExistsAsync(admin, name))
                throw new InvalidOperationException("Owned database still exists after DROP returned successfully: " + name);
            Log(phase, name, "verified_absent", watch, admin.ProcessID);
        }
        catch (Exception ex)
        {
            // Do not retry the test or convert a failed cleanup to a Passed result.
            Log(phase, name, "failed", watch, errorType: ex.GetType().FullName);
            throw;
        }
    }

    public static async Task<QuarantineDatabase> CreateAsync(string suffix)
    {
        var name = NameFor(suffix);
        var nonce = RunNonce();
        var sourceConnection = TestDatabaseTarget.RequireTemplateSource();
        var sourceBuilder = new NpgsqlConnectionStringBuilder(sourceConnection);
        var source = sourceBuilder.Database ?? string.Empty;
        var expectedTemplate = "ppiq_acceptance_t099_" + nonce + "_template";
        if (!string.Equals(source, expectedTemplate, StringComparison.Ordinal) ||
            TestDatabaseTarget.IsProtected(source) || source.Length > 63)
            throw new InvalidOperationException("The template must be this runner's exact non-protected fresh database.");

        var adminString = new NpgsqlConnectionStringBuilder(sourceConnection)
        {
            Database = "postgres",
            Timeout = AdminConnectTimeoutSeconds,
            CommandTimeout = AdminCommandTimeoutSeconds,
            Pooling = false,
            Enlist = false,
            ApplicationName = "ppiq-t099-admin-" + nonce
        }.ConnectionString;
        var targetString = new NpgsqlConnectionStringBuilder(sourceConnection)
        {
            Database = name,
            IncludeErrorDetail = true,
            CommandTimeout = 180,
            Timeout = 60,
            ApplicationName = "ppiq-t099-test-" + nonce
        }.ConnectionString;

        // This affects only the serial test process's pools, never other processes.
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminString);
        var watch = Stopwatch.StartNew();
        Log("create_admin_open", name, "begin", watch);
        await admin.OpenAsync();
        Log("create_admin_open", name, "connected", watch, admin.ProcessID);
        await DropAndVerifyAsync(admin, name, "initial_drop");

        watch.Restart();
        Log("create", name, "begin", watch, admin.ProcessID);
        try
        {
            await using var create = new NpgsqlCommand(
                "CREATE DATABASE \"" + name + "\" TEMPLATE \"" + source + "\";", admin)
            { CommandTimeout = AdminCommandTimeoutSeconds };
            await create.ExecuteNonQueryAsync();
            if (!await ExistsAsync(admin, name))
                throw new InvalidOperationException("CREATE returned without the owned database existing: " + name);
            Log("create", name, "verified_present", watch, admin.ProcessID);
        }
        catch (Exception ex)
        {
            Log("create", name, "failed", watch, errorType: ex.GetType().FullName);
            // The outer runner registered this exact name before creation and owns
            // fallback cleanup even when no fixture object could be returned.
            throw;
        }
        return new QuarantineDatabase(name, adminString, targetString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        RequireOwnedChild(Name);
        NpgsqlConnection.ClearAllPools();
        var watch = Stopwatch.StartNew();
        Log("dispose_admin_open", Name, "begin", watch);
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        Log("dispose_admin_open", Name, "connected", watch, admin.ProcessID);
        await DropAndVerifyAsync(admin, Name, "dispose_drop");
        _disposed = true; // Set only after a successful DROP and absence query.
    }
}
