using System.Data;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Relationships;

namespace PlantProcess.Infrastructure.Relationships;

/// <summary>
/// T-057. The ONLY code in the product that knows where relationships are
/// physically kept in M1.
///
/// These tables are compatibility persistence, not the final model. The
/// canonical home is the three ppiq_meta relationship tables and T-095 owns the
/// convergence. Nothing above IRelationshipStore names a table, which is what
/// makes that convergence a change of one file rather than a change of a
/// contract - and why no test in this task may contain a table name either.
/// </summary>
public sealed class NpgsqlRelationshipStore : IRelationshipStore
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlRelationshipStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<Guid> UpsertAsync(
        Guid tenantId,
        RelationshipDeclaration declaration,
        Guid sourceDefinitionId,
        int sourceDefinitionVersion,
        DateTime effectiveFromUtc,
        CancellationToken cancellationToken)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        Guid id;
        await using (var insert = conn.CreateCommand())
        {
            insert.Transaction = tx;
            insert.CommandText =
                "INSERT INTO public.ppiq_plant_relationships " +
                "(tenant_id, relationship_code, left_entity, right_entity, join_type, cardinality, " +
                " grain_left, grain_right, attribution_rule, attribution_expression, is_preferred_path, " +
                " ambiguity_state, validation_state, source_definition_id, source_definition_version, " +
                " effective_from_utc) " +
                "VALUES (@tenant, @code, @left, @right, @join, @card, @grainLeft, @grainRight, " +
                "        @attrRule, @attrExpr, @preferred, @ambiguity, @validation, @defId, @defVersion, @from) " +
                "RETURNING id";

            insert.Parameters.AddWithValue("tenant", tenantId);
            insert.Parameters.AddWithValue("code", declaration.RelationshipCode);
            insert.Parameters.AddWithValue("left", declaration.LeftEntity);
            insert.Parameters.AddWithValue("right", declaration.RightEntity);
            insert.Parameters.AddWithValue("join", declaration.JoinType);
            insert.Parameters.AddWithValue("card", declaration.Cardinality);
            insert.Parameters.AddWithValue("grainLeft", declaration.GrainLeft);
            insert.Parameters.AddWithValue("grainRight", declaration.GrainRight);
            insert.Parameters.AddWithValue("attrRule", (object?)declaration.AttributionRule ?? DBNull.Value);
            insert.Parameters.AddWithValue("attrExpr", (object?)declaration.AttributionExpression ?? DBNull.Value);
            insert.Parameters.AddWithValue("preferred", declaration.IsPreferredPath);

            // A newly emitted relationship is unambiguous until a second path
            // appears, and UNPROVEN until it has been run against real data.
            // Publishing does not prove anything, so it must not claim to.
            insert.Parameters.AddWithValue("ambiguity", RelationshipAmbiguityStates.Unambiguous);
            insert.Parameters.AddWithValue("validation", RelationshipValidationStates.Unproven);

            insert.Parameters.AddWithValue("defId", sourceDefinitionId);
            insert.Parameters.AddWithValue("defVersion", sourceDefinitionVersion);
            insert.Parameters.AddWithValue("from", effectiveFromUtc);

            id = (Guid)(await insert.ExecuteScalarAsync(cancellationToken))!;
        }

        foreach (var member in declaration.Members.OrderBy(m => m.MemberOrder))
        {
            await using var memberCmd = conn.CreateCommand();
            memberCmd.Transaction = tx;
            memberCmd.CommandText =
                "INSERT INTO public.ppiq_plant_relationship_members " +
                "(relationship_id, left_column, right_column, member_order, comparison) " +
                "VALUES (@rel, @left, @right, @order, @comparison)";
            memberCmd.Parameters.AddWithValue("rel", id);
            memberCmd.Parameters.AddWithValue("left", member.LeftColumn);
            memberCmd.Parameters.AddWithValue("right", member.RightColumn);
            memberCmd.Parameters.Add("order", NpgsqlDbType.Smallint).Value = member.MemberOrder;
            memberCmd.Parameters.AddWithValue("comparison", string.IsNullOrWhiteSpace(member.Comparison) ? "=" : member.Comparison);
            await memberCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return id;
    }

    public async Task<IReadOnlyList<RelationshipDto>> ReadPublishedAsync(
        Guid tenantId, string? entity, CancellationToken cancellationToken)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        var rows = new List<RelationshipRow>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                SelectColumns +
                "WHERE r.tenant_id = @tenant AND r.retired_at_utc IS NULL " +
                "  AND (@entity IS NULL OR r.left_entity = @entity OR r.right_entity = @entity) " +
                "ORDER BY r.relationship_code";
            cmd.Parameters.AddWithValue("tenant", tenantId);
            // W1-T057-02. Bound with an explicit type rather than AddWithValue.
            // A DBNull carries no type, and '@entity IS NULL' gives the server no
            // column to infer one from, so PostgreSQL refuses to plan the
            // statement with 42P08. The type must travel with the value whether
            // or not the value is null.
            cmd.Parameters.Add("entity", NpgsqlDbType.Text).Value = (object?)entity ?? DBNull.Value;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) rows.Add(Read(reader));
        }

        return await AttachMembersAsync(conn, rows, cancellationToken);
    }

    public async Task<RelationshipDto?> ReadByIdAsync(
        Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);

        var rows = new List<RelationshipRow>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = SelectColumns + "WHERE r.tenant_id = @tenant AND r.id = @id AND r.retired_at_utc IS NULL";
            cmd.Parameters.AddWithValue("tenant", tenantId);
            cmd.Parameters.AddWithValue("id", id);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) rows.Add(Read(reader));
        }

        var withMembers = await AttachMembersAsync(conn, rows, cancellationToken);
        return withMembers.Count == 0 ? null : withMembers[0];
    }

    public async Task<int> RetireByDefinitionAsync(
        Guid tenantId, Guid sourceDefinitionId, DateTime retiredAtUtc, CancellationToken cancellationToken)
    {
        await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();

        // Deactivated, never deleted: a finding computed under this relationship
        // must stay explainable after the model moves on.
        cmd.CommandText =
            "UPDATE public.ppiq_plant_relationships SET retired_at_utc = @retired " +
            "WHERE tenant_id = @tenant AND source_definition_id = @defId AND retired_at_utc IS NULL";
        cmd.Parameters.AddWithValue("tenant", tenantId);
        cmd.Parameters.AddWithValue("defId", sourceDefinitionId);
        cmd.Parameters.AddWithValue("retired", retiredAtUtc);

        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string SelectColumns =
        "SELECT r.id, r.relationship_code, r.left_entity, r.right_entity, r.join_type, r.cardinality, " +
        "       r.grain_left, r.grain_right, r.is_grain_converting, r.attribution_rule, r.attribution_expression, " +
        "       r.is_preferred_path, r.ambiguity_state, r.validation_state, r.source_definition_id, " +
        "       r.source_definition_version, r.effective_from_utc, r.retired_at_utc " +
        "FROM public.ppiq_plant_relationships r ";

    private sealed record RelationshipRow(
        Guid Id, string Code, string Left, string Right, string Join, string Card,
        string GrainLeft, string GrainRight, bool Converting, string? AttrRule, string? AttrExpr,
        bool Preferred, string Ambiguity, string Validation, Guid DefId, int DefVersion,
        DateTime From, DateTime? Retired);

    private static RelationshipRow Read(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
        reader.GetBoolean(8),
        reader.IsDBNull(9) ? null : reader.GetString(9),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.GetBoolean(11), reader.GetString(12), reader.GetString(13),
        reader.GetGuid(14), reader.GetInt32(15), reader.GetDateTime(16),
        reader.IsDBNull(17) ? null : reader.GetDateTime(17));

    private static async Task<IReadOnlyList<RelationshipDto>> AttachMembersAsync(
        NpgsqlConnection conn, List<RelationshipRow> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0) return Array.Empty<RelationshipDto>();

        var members = new Dictionary<Guid, List<RelationshipMemberDto>>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT relationship_id, left_column, right_column, member_order, comparison " +
                "FROM public.ppiq_plant_relationship_members " +
                "WHERE relationship_id = ANY(@ids) ORDER BY relationship_id, member_order";
            cmd.Parameters.AddWithValue("ids", rows.Select(r => r.Id).ToArray());

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var relationshipId = reader.GetGuid(0);
                if (!members.TryGetValue(relationshipId, out var list))
                {
                    list = new List<RelationshipMemberDto>();
                    members[relationshipId] = list;
                }

                list.Add(new RelationshipMemberDto(
                    reader.GetString(1), reader.GetString(2), reader.GetInt16(3), reader.GetString(4)));
            }
        }

        return rows.Select(r => new RelationshipDto(
            r.Id, r.Code, r.Left, r.Right, r.Join, r.Card, r.GrainLeft, r.GrainRight, r.Converting,
            r.AttrRule, r.AttrExpr, r.Preferred, r.Ambiguity, r.Validation, r.DefId, r.DefVersion,
            r.From, r.Retired,
            members.TryGetValue(r.Id, out var m) ? m : new List<RelationshipMemberDto>())).ToList();
    }
}