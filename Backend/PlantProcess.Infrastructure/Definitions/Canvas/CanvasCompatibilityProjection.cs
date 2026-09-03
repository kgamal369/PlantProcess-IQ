using Npgsql;
using NpgsqlTypes;

namespace PlantProcess.Infrastructure.Definitions.Canvas;

/// <summary>
/// PPIQ T-244. THE COMPATIBILITY PROJECTION, SUBORDINATE BY CONSTRUCTION.
///
/// The execution path still reads the legacy tables. This projection writes
/// them, and it can only do so on a connection and transaction the lifecycle
/// service hands it. It has no NpgsqlDataSource, no connection string and no
/// way to open anything of its own, which is what makes "same connection, same
/// transaction, commit once" a property of the types rather than a promise
/// in a comment.
///
/// The direction is fixed: canonical authority -> projection. Nothing here
/// reads a legacy row to decide canonical state.
/// </summary>
public interface ICanvasCompatibilityProjection
{
    Task ProjectGraphVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid sessionId,
        int versionNumber,
        string graphJson,
        string publishedBy,
        CancellationToken cancellationToken);

    Task ProjectSqlVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string mappingCode,
        string? displayName,
        string? canonicalEntity,
        int versionNumber,
        string definitionJson,
        string status,
        CancellationToken cancellationToken);
}

public sealed class CanvasCompatibilityProjection : ICanvasCompatibilityProjection
{
    public async Task ProjectGraphVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid tenantId,
        Guid sessionId,
        int versionNumber,
        string graphJson,
        string publishedBy,
        CancellationToken cancellationToken)
    {
        Require(connection, transaction);

        // ON CONFLICT DO NOTHING: the version number is the canonical version
        // number, so a redeclared save that the writer folded onto an existing
        // canonical version must not raise a duplicate here. The projection
        // follows canonical numbering; it never allocates its own.
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO ppiq_meta.ppiq_visual_mapper_versions
                (tenant_id, session_id, version_number, version_status, mapping_definition, published_by)
            VALUES (@tenant_id, @session_id, @version_number, 'published', @definition::jsonb, @published_by)
            ON CONFLICT DO NOTHING;
            """,
            connection,
            transaction);

        command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId });
        command.Parameters.Add(new NpgsqlParameter("session_id", NpgsqlDbType.Uuid) { Value = sessionId });
        command.Parameters.Add(new NpgsqlParameter("version_number", NpgsqlDbType.Integer) { Value = versionNumber });
        command.Parameters.Add(new NpgsqlParameter("definition", NpgsqlDbType.Text) { Value = graphJson });
        command.Parameters.Add(new NpgsqlParameter("published_by", NpgsqlDbType.Text) { Value = publishedBy });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ProjectSqlVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string mappingCode,
        string? displayName,
        string? canonicalEntity,
        int versionNumber,
        string definitionJson,
        string status,
        CancellationToken cancellationToken)
    {
        Require(connection, transaction);

        await using var command = new NpgsqlCommand(
            """
            INSERT INTO ppiq_meta.ppiq_mapping_versions
                (mapping_code, display_name, canonical_entity, environment, version_number, definition, status)
            VALUES (@code, @name, @entity, 'authoring', @version_number, @definition::jsonb, @status)
            ON CONFLICT DO NOTHING;
            """,
            connection,
            transaction);

        command.Parameters.Add(new NpgsqlParameter("code", NpgsqlDbType.Text) { Value = mappingCode });
        command.Parameters.Add(new NpgsqlParameter("name", NpgsqlDbType.Text) { Value = (object?)displayName ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("entity", NpgsqlDbType.Text) { Value = (object?)canonicalEntity ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("version_number", NpgsqlDbType.Integer) { Value = versionNumber });
        command.Parameters.Add(new NpgsqlParameter("definition", NpgsqlDbType.Text) { Value = definitionJson });
        command.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Text) { Value = status });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Require(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        if (!ReferenceEquals(transaction.Connection, connection))
        {
            throw new InvalidOperationException(
                "The compatibility projection must run on the transaction's own connection. " +
                "A projection on a different connection is not part of the canonical unit of work.");
        }
    }
}
