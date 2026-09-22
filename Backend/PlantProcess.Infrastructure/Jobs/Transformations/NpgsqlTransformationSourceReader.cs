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
public sealed class NpgsqlTransformationSourceReader : ITransformationSourceReader, IDefinitionScopedTransformationSourceReader
{
    private readonly PlantProcessDbContext _db;

    public NpgsqlTransformationSourceReader(PlantProcessDbContext db)
    {
        _db = db;
    }

    public async Task<IAsyncDisposable> BindDefinitionAsync(Guid definitionId, int version, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        bool opened = connection.State != ConnectionState.Open;
        if (opened) await _db.Database.OpenConnectionAsync(ct);
        string previous = string.Empty;
        bool changed = false;
        try
        {
            await using var identity = new NpgsqlCommand(
                "SELECT d.tenant_id FROM ppiq_meta.definition_store d JOIN ppiq_meta.definition_versions v ON v.definition_id=d.id AND v.tenant_id=d.tenant_id "
                + "WHERE d.id=$1 AND v.version_number=$2 AND d.definition_kind='transformation' AND NOT d.is_deleted AND NOT v.is_deleted", connection, Ambient());
            identity.Parameters.AddWithValue(definitionId);
            identity.Parameters.AddWithValue(version);
            var tenant = await identity.ExecuteScalarAsync(ct);
            if (tenant is not Guid tenantId || tenantId == Guid.Empty)
                throw new InvalidOperationException("The exact transformation has no authoritative tenant.");
            await using var old = new NpgsqlCommand("SELECT coalesce(current_setting('app.current_tenant',true),'')",connection,Ambient());
            previous = (string)(await old.ExecuteScalarAsync(ct))!;
            await using var bind = new NpgsqlCommand("SELECT set_config('app.current_tenant',$1,false)",connection,Ambient());
            bind.Parameters.AddWithValue(tenantId.ToString("D"));
            await bind.ExecuteScalarAsync(ct);
            changed = true;
            return new DefinitionTenantScope(_db, connection, previous, opened);
        }
        catch
        {
            if (changed) NpgsqlConnection.ClearPool(connection);
            if (opened) await _db.Database.CloseConnectionAsync();
            throw;
        }
    }

    private sealed class DefinitionTenantScope(PlantProcessDbContext db, NpgsqlConnection connection,
        string previous, bool opened) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var restore = new NpgsqlCommand("SELECT set_config('app.current_tenant',$1,false)",connection,
                    db.Database.CurrentTransaction is null ? null : (NpgsqlTransaction)db.Database.CurrentTransaction.GetDbTransaction());
                restore.Parameters.AddWithValue(previous);
                await restore.ExecuteScalarAsync(CancellationToken.None);
            }
            catch
            {
                NpgsqlConnection.ClearPool(connection);
                throw;
            }
            finally { if (opened) await db.Database.CloseConnectionAsync(); }
        }
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

    public async Task<IReadOnlyList<string>> LineageColumnsAsync(
        string schema,
        string relation,
        CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            "SELECT c.column_name FROM information_schema.columns c "
            + "WHERE c.table_schema = $1 AND c.table_name = $2 AND c.column_name = ANY($3) "
            + "ORDER BY c.column_name",
            connection,
            Ambient());

        command.Parameters.Add(new NpgsqlParameter { Value = schema });
        command.Parameters.Add(new NpgsqlParameter { Value = relation });
        command.Parameters.Add(new NpgsqlParameter { Value = TransformationLineageColumns.All.ToArray() });

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
