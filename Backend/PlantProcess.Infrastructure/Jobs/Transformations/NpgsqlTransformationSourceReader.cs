using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PlantProcess.Application.Definitions.Transformations;
using PlantProcess.Application.Jobs.Execution.Transformations;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Jobs.Transformations;

/// <summary>
/// Executes statements produced by the one transformation compiler on the context's own
/// connection and ambient transaction, and answers which staged relations expose the
/// governed provenance contract. Identifiers are checked with the compiler's own rule
/// before anything is composed; values are always bound.
/// </summary>
public sealed class NpgsqlTransformationSourceReader : ITransformationSourceReader
{
    private readonly PlantProcessDbContext _db;

    public NpgsqlTransformationSourceReader(PlantProcessDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<string>> ProvenanceCapableRelationsAsync(
        string schema,
        IReadOnlyList<string> relations,
        CancellationToken cancellationToken)
    {
        if (relations.Count == 0) { return Array.Empty<string>(); }

        NpgsqlConnection connection = await OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            "SELECT c.table_name FROM information_schema.columns c "
            + "WHERE c.table_schema = $1 AND c.table_name = ANY($2) AND c.column_name = ANY($3) "
            + "GROUP BY c.table_name HAVING count(DISTINCT c.column_name) = 2 "
            + "ORDER BY c.table_name",
            connection,
            Ambient());

        command.Parameters.Add(new NpgsqlParameter { Value = schema });
        command.Parameters.Add(new NpgsqlParameter { Value = relations.Distinct(StringComparer.Ordinal).ToArray() });
        command.Parameters.Add(new NpgsqlParameter
        {
            Value = new[] { TransformationProvenanceColumns.SourceSystem, TransformationProvenanceColumns.SourceRecordId }
        });

        var found = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                found.Add(reader.GetString(0));
            }
        }

        return found.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    public async Task<long> CountRowsAsync(
        string schema,
        string relation,
        CancellationToken cancellationToken)
    {
        if (!TransformationSafeSelect.Ident(schema) || !TransformationSafeSelect.Ident(relation))
        {
            throw new InvalidOperationException("A staged relation identifier failed the compiler's identifier rule.");
        }

        NpgsqlConnection connection = await OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM \"" + schema + "\".\"" + relation + "\"",
            connection,
            Ambient());

        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<object?[]>> ReadAsync(
        string sql,
        IReadOnlyList<object> parameters,
        int expectedColumnCount,
        CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection, Ambient());
        foreach (object parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        }

        var rows = new List<object?[]>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (reader.FieldCount != expectedColumnCount)
            {
                throw new InvalidOperationException(
                    "The compiled statement returned " + reader.FieldCount + " columns and the admitted plan declares "
                    + expectedColumnCount + ".");
            }

            while (await reader.ReadAsync(cancellationToken))
            {
                var values = new object?[expectedColumnCount];
                for (int i = 0; i < expectedColumnCount; i++)
                {
                    values[i] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
                }

                rows.Add(values);
            }
        }

        return rows;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private NpgsqlTransaction? Ambient() =>
        _db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;
}
