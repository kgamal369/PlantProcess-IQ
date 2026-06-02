using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace PlantProcess.Infrastructure.Connectors.Common;

/// <summary>
/// P1-03: enforces read-only at the SERVER session level on every opened DB
/// connection, plus a statement timeout, so a write is rejected by the database
/// engine - not only by SafeSqlValidator. SQL Server has no session read-only
/// mode, so it is hardened via least-privilege login + ApplicationIntent=ReadOnly
/// in the connection string; here we at least pin a conservative isolation level.
/// </summary>
public static class ConnectorReadOnlySession
{
    public const int DefaultStatementTimeoutSeconds = 30;

    public static async Task ApplyAsync(DbConnection connection, CancellationToken ct, int? statementTimeoutSeconds = null)
    {
        if (connection is null || connection.State != ConnectionState.Open)
            return;

        var secs = statementTimeoutSeconds ?? DefaultStatementTimeoutSeconds;

        string? sql = connection switch
        {
            NpgsqlConnection => $"SET default_transaction_read_only = on; SET statement_timeout = {secs * 1000};",
            MySqlConnection  => "SET SESSION TRANSACTION READ ONLY",
            OracleConnection => "SET TRANSACTION READ ONLY",
            SqlConnection    => "SET TRANSACTION ISOLATION LEVEL READ COMMITTED",
            _                => null
        };

        if (sql is null)
            return;

        await using var cmd = connection.CreateCommand();
        cmd.CommandText    = sql;
        cmd.CommandTimeout = secs;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
