using System.Data;
using Npgsql;

namespace PlantProcess.Infrastructure.Definitions.Canvas;

/// <summary>
/// PPIQ T-262. ONE READER OF STAGED COLUMN METADATA, TWO CALLERS.
///
/// The dataset endpoint owned this query inline. T-262 needs the same facts at save
/// time, to resolve the type of a bound source column - and a second query would be a
/// second schema authority that agrees today and drifts later. So the statement and the
/// row shape live here, and the endpoint and the lifecycle both call it.
///
/// This is an EXTRACTION, not a redesign. No caching, no refresh schedule, no
/// persistence, no new discovery surface: exactly the metadata the endpoint already
/// exposed, reachable from one more place.
/// </summary>
public static class StagedColumnCatalogue
{
    /// <summary>One staged column, as the catalogue reports it.</summary>
    public sealed record StagedColumnFact(string Table, string Column, string SqlType, bool IsNullable, bool DeclaredKey);

    private const string ColumnSql = @"
SELECT c.table_name, c.column_name, c.data_type, c.is_nullable,
       (k.column_name IS NOT NULL) AS is_declared_key
FROM information_schema.columns c
LEFT JOIN (
    SELECT tc.table_schema, tc.table_name, kcu.column_name
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
      ON kcu.constraint_name = tc.constraint_name
     AND kcu.table_schema = tc.table_schema
    WHERE tc.table_schema = $1 AND tc.constraint_type IN ('PRIMARY KEY', 'UNIQUE')
) k ON k.table_schema = c.table_schema
   AND k.table_name = c.table_name
   AND k.column_name = c.column_name
WHERE c.table_schema = $1
ORDER BY c.table_name, c.ordinal_position;";

    /// <summary>For the dataset endpoint, which holds a data source.</summary>
    public static async Task<IReadOnlyList<StagedColumnFact>> ReadAsync(
        NpgsqlDataSource dataSource, string schema, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(ColumnSql);
        command.Parameters.AddWithValue(schema);
        return await ReadRowsAsync(command, cancellationToken);
    }

    /// <summary>
    /// For the lifecycle, which holds the context's own connection and must stay inside
    /// the transaction that is already open.
    /// </summary>
    public static async Task<IReadOnlyList<StagedColumnFact>> ReadAsync(
        NpgsqlConnection connection, string schema, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = new NpgsqlCommand(ColumnSql, connection);
        command.Parameters.AddWithValue(schema);
        return await ReadRowsAsync(command, cancellationToken);
    }

    private static async Task<IReadOnlyList<StagedColumnFact>> ReadRowsAsync(
        NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var facts = new List<StagedColumnFact>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facts.Add(new StagedColumnFact(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                string.Equals(reader.GetString(3), "YES", StringComparison.OrdinalIgnoreCase),
                reader.GetBoolean(4)));
        }

        return facts;
    }
}