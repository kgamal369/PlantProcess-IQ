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

        // The session default must actually have persisted on this connection.
        await using (var check = new NpgsqlCommand("SHOW default_transaction_read_only", c))
        {
            var dtro = (string?)await check.ExecuteScalarAsync(CancellationToken.None);
            Assert.Equal("on", dtro);
        }

        // default_transaction_read_only is evaluated at transaction start, so probe the write
        // inside an explicit transaction opened AFTER Apply. A permanent CREATE is rejected by
        // the engine with SQLSTATE 25006 (read_only_sql_transaction). TEMP objects are exempt
        // from read-only and must not be used as the probe.
        await using var tx = await c.BeginTransactionAsync(CancellationToken.None);
        try
        {
            await using var cmd = new NpgsqlCommand("CREATE TABLE _ppiq_ro_probe(id int)", c, (NpgsqlTransaction)tx);
            await cmd.ExecuteNonQueryAsync(CancellationToken.None);
            await tx.RollbackAsync(CancellationToken.None);
            Assert.Fail("Write succeeded on a read-only session - read-only enforcement is not working.");
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "25006")
        {
            // expected: the engine rejected the write
        }
    }
}