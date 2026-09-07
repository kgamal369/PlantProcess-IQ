using Npgsql;

namespace PlantProcess.Api.IntegrationTests.Relationships;

/// <summary>
/// A relationship now carries a real foreign key to the definition that emitted
/// it, so a certification that publishes one has to publish it FROM something.
///
/// This seeds the minimum lawful definition identity and takes it away again.
/// It is deliberately not a fixture or a builder: the certifications assert the
/// relationship contract, and a definition here is scaffolding, not a subject.
/// Rows are marked synthetic because they are.
/// </summary>
internal static class RelationshipCertificationDefinitions
{
    public static async Task SeedAsync(NpgsqlDataSource dataSource, Guid tenantId, params Guid[] definitionIds)
    {
        await using var conn = await dataSource.OpenConnectionAsync();

        foreach (var definitionId in definitionIds)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "INSERT INTO ppiq_meta.definition_store " +
                "(id, tenant_id, definition_code, surface, definition_kind, name, owner_id, " +
                " current_version, is_locked, created_at_utc, is_deleted, is_synthetic) " +
                "VALUES (@id, @tenant, @code, 'S1', 'transformation', @name, @owner, 1, false, now(), false, true) " +
                "ON CONFLICT (id) DO NOTHING";

            cmd.Parameters.AddWithValue("id", definitionId);
            cmd.Parameters.AddWithValue("tenant", tenantId);
            cmd.Parameters.AddWithValue("code", "cert_def_" + definitionId.ToString("N"));
            cmd.Parameters.AddWithValue("name", "Relationship certification definition");
            cmd.Parameters.AddWithValue("owner", tenantId);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// Removed after the relationships that reference it, never before: the
    /// foreign key restricts, and a cleanup that fights its own constraint is a
    /// cleanup that leaves rows behind.
    /// </summary>
    public static async Task RemoveAsync(NpgsqlDataSource dataSource, params Guid[] definitionIds)
    {
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ppiq_meta.definition_store WHERE id = ANY(@ids)";
        cmd.Parameters.AddWithValue("ids", definitionIds);
        await cmd.ExecuteNonQueryAsync();
    }
}
