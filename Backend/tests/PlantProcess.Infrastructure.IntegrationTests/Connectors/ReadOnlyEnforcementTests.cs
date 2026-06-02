using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace PlantProcess.Infrastructure.IntegrationTests.Connectors;

/// <summary>
/// P1-03 integration proof. Requires a reachable PostgreSQL via the
/// PPIQ_TEST_PG_CONNSTRING env var (or Testcontainers). Skips cleanly otherwise so
/// the suite stays green on machines without Docker/Postgres.
/// </summary>
public sealed class ReadOnlyEnforcementTests
{
    private static string? Conn => System.Environment.GetEnvironmentVariable("PPIQ_TEST_PG_CONNSTRING");

    [SkippableFact]
    public async Task PostgreSql_session_is_read_only_after_apply()
    {
        Skip.If(string.IsNullOrWhiteSpace(Conn), "Set PPIQ_TEST_PG_CONNSTRING to run this integration test.");

        await using var c = new NpgsqlConnection(Conn);
        await c.OpenAsync(CancellationToken.None);
        await PlantProcess.Infrastructure.Connectors.Common.ConnectorReadOnlySession.ApplyAsync(c, CancellationToken.None);

        await c.ExecuteRawScalarSafe("CREATE TEMP TABLE _ppiq_ro_probe(id int)"); // expected to throw
        // If we reach here the session was NOT read-only:
        Assert.Fail("Write succeeded on a read-only session - read-only enforcement is not working.");
    }
}

internal static class _PgTestExtensions
{
    public static async Task ExecuteRawScalarSafe(this NpgsqlConnection c, string sql)
    {
        try
        {
            await using var cmd = new NpgsqlCommand(sql, c);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "25006") // read_only_sql_transaction
        {
            return; // expected: the engine rejected the write
        }
    }
}
