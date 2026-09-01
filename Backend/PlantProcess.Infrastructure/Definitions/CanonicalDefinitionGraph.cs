using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Definitions;

/// <summary>
/// PPIQ T-091. The impact and closure read model over ppiq_meta.definition_dependencies.
///
/// NO SECOND REGISTRY. T-089 owns dependency persistence and its cycle trigger.
/// This class reads that graph and holds nothing: no cache table, no mirrored
/// edges, no materialised impact. A second copy of a dependency graph is a
/// second answer to the same question and it is wrong the moment an edge moves.
///
/// TENANT SCOPING IS IN THE RECURSION, NOT AROUND IT. The tenant predicate sits
/// inside the recursive term as well as the seed. Filtering only the seed would
/// let one hop cross into another tenant and every later hop inherit it, which
/// is precisely how a customer learns that a similarly named definition exists
/// somewhere else.
///
/// BOUNDED DEFENSIVELY. The store rejects cycles, so this walk should terminate
/// on its own. It carries a depth ceiling anyway: a traversal whose termination
/// depends on a trigger being correct is one defect away from not terminating.
/// Reaching the ceiling is reported as Truncated rather than silently trimmed.
/// </summary>
public sealed class CanonicalDefinitionGraph : ICanonicalDefinitionGraph
{
    private const int MaximumDepth = 32;

    private readonly PlantProcessDbContext _dbContext;

    public CanonicalDefinitionGraph(PlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationResult<DefinitionImpact>> PreviewImpactAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return ApplicationResult<DefinitionImpact>.Failure(ApplicationError.Validation(
                "Impact preview requires an authenticated tenant identity."));
        }

        var connection = await OpenAsync(cancellationToken);

        var root = await ReadDefinitionAsync(connection, tenantId, definitionId, cancellationToken);
        if (root is null)
        {
            return ApplicationResult<DefinitionImpact>.Failure(ApplicationError.NotFound(
                "No definition with that identity exists in this tenant."));
        }

        // REVERSE WALK. The seed selects edges whose TARGET is the definition
        // being changed, and each recursive hop does the same again. This is
        // the "who consumes me" direction and it is not interchangeable with
        // the closure walk below.
        const string sql = """
            WITH RECURSIVE consumers(definition_id, depth, dependency_kind, is_required, depends_on_version) AS (
                SELECT d.definition_id, 1, d.dependency_kind, d.is_required, d.depends_on_version
                  FROM ppiq_meta.definition_dependencies d
                 WHERE d.depends_on_definition_id = @root
                   AND d.tenant_id = @tenant
                   AND d.is_deleted = false
                UNION ALL
                SELECT d.definition_id, c.depth + 1, d.dependency_kind, d.is_required, d.depends_on_version
                  FROM ppiq_meta.definition_dependencies d
                  JOIN consumers c ON c.definition_id = d.depends_on_definition_id
                 WHERE d.tenant_id = @tenant
                   AND d.is_deleted = false
                   AND c.depth < @max_depth
            ),
            shallowest AS (
                SELECT definition_id,
                       min(depth) AS depth,
                       min(dependency_kind) AS dependency_kind,
                       bool_or(is_required) AS is_required,
                       max(depends_on_version) AS depends_on_version
                  FROM consumers
                 GROUP BY definition_id
            )
            SELECT s.definition_id,
                   st.definition_code,
                   st.definition_kind,
                   st.surface,
                   st.current_version,
                   s.depth,
                   s.dependency_kind,
                   s.is_required,
                   s.depends_on_version,
                   (SELECT v.version_number FROM ppiq_meta.definition_versions v
                     WHERE v.definition_id = s.definition_id AND v.status = 'published'
                     ORDER BY v.version_number DESC LIMIT 1) AS published_version,
                   (SELECT v.id FROM ppiq_meta.definition_versions v
                     WHERE v.definition_id = s.definition_id AND v.status = 'published'
                     ORDER BY v.version_number DESC LIMIT 1) AS published_version_id
              FROM shallowest s
              JOIN ppiq_meta.definition_store st ON st.id = s.definition_id
             WHERE st.tenant_id = @tenant
             ORDER BY s.depth, st.definition_kind, st.definition_code, s.definition_id;
            """;

        var consumers = new List<ImpactedConsumer>();
        var deepest = 0;

        await using (var command = Command(sql, connection))
        {
            Bind(command, "root", definitionId);
            Bind(command, "tenant", tenantId);
            command.Parameters.Add(new NpgsqlParameter("max_depth", NpgsqlDbType.Integer) { Value = MaximumDepth });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var storageKind = reader.GetString(2);
                if (!DefinitionKindRegistry.TryResolveStorageKind(storageKind, out var contract))
                {
                    continue;
                }

                var depth = reader.GetInt32(5);
                if (depth > deepest) { deepest = depth; }

                var pinned = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8);

                consumers.Add(new ImpactedConsumer(
                    DefinitionId: reader.GetGuid(0),
                    DefinitionCode: reader.GetString(1),
                    Kind: contract.Kind,
                    Surface: reader.GetString(3),
                    PublishedVersionNumber: reader.IsDBNull(9) ? null : reader.GetInt32(9),
                    PublishedVersionId: reader.IsDBNull(10) ? null : reader.GetGuid(10),
                    CurrentVersionNumber: reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    Relationship: depth == 1 ? ImpactRelationship.Direct : ImpactRelationship.Transitive,
                    Depth: depth,
                    DependencyKind: reader.GetString(6),
                    IsRequired: reader.GetBoolean(7),
                    PinnedDependsOnVersion: pinned,

                    // The only compatibility claim the canonical store can
                    // actually support: a consumer pinned to a specific version
                    // will not follow a newer one. Everything else stays
                    // NotEvaluated until a compatibility authority exists.
                    CompatibilityRisk: pinned.HasValue && depth == 1
                        ? CompatibilityRisk.PinnedToExistingVersion
                        : CompatibilityRisk.NotEvaluated));
            }
        }

        var summary = consumers
            .GroupBy(c => c.Kind)
            .Select(g => new ImpactSummaryEntry(
                g.Key,
                g.Count(),
                g.Count(c => c.CompatibilityRisk == CompatibilityRisk.PinnedToExistingVersion)))
            .OrderBy(e => e.Kind)
            .ToList();

        return ApplicationResult<DefinitionImpact>.Success(new DefinitionImpact(
            DefinitionId: definitionId,
            DefinitionCode: root.Value.Code,
            Kind: root.Value.Kind,
            Surface: root.Value.Surface,
            Consumers: consumers,
            SummaryByKind: summary,
            MaximumDepthReached: deepest,
            Truncated: deepest >= MaximumDepth));
    }

    public async Task<ApplicationResult<IReadOnlyList<DefinitionClosureNode>>> ResolveClosureAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return ApplicationResult<IReadOnlyList<DefinitionClosureNode>>.Failure(ApplicationError.Validation(
                "Closure resolution requires an authenticated tenant identity."));
        }

        var connection = await OpenAsync(cancellationToken);

        var root = await ReadDefinitionAsync(connection, tenantId, definitionId, cancellationToken);
        if (root is null)
        {
            return ApplicationResult<IReadOnlyList<DefinitionClosureNode>>.Failure(ApplicationError.NotFound(
                "No definition with that identity exists in this tenant."));
        }

        // FORWARD WALK. Seed selects edges whose SOURCE is the definition being
        // exported: "what do I need". The mirror image of PreviewImpactAsync,
        // and the two are never substituted for one another.
        const string sql = """
            WITH RECURSIVE required(definition_id, depth) AS (
                SELECT @root, 0
                UNION ALL
                SELECT d.depends_on_definition_id, r.depth + 1
                  FROM ppiq_meta.definition_dependencies d
                  JOIN required r ON r.definition_id = d.definition_id
                 WHERE d.tenant_id = @tenant
                   AND d.is_deleted = false
                   AND r.depth < @max_depth
            ),
            shallowest AS (
                SELECT definition_id, min(depth) AS depth FROM required GROUP BY definition_id
            )
            SELECT s.definition_id, st.definition_code, st.definition_kind, st.surface, s.depth
              FROM shallowest s
              JOIN ppiq_meta.definition_store st ON st.id = s.definition_id
             WHERE st.tenant_id = @tenant
             ORDER BY s.depth, st.definition_kind, st.definition_code, s.definition_id;
            """;

        var nodes = new List<(Guid Id, string Code, DefinitionKind Kind, string Surface, int Depth)>();

        await using (var command = Command(sql, connection))
        {
            Bind(command, "root", definitionId);
            Bind(command, "tenant", tenantId);
            command.Parameters.Add(new NpgsqlParameter("max_depth", NpgsqlDbType.Integer) { Value = MaximumDepth });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!DefinitionKindRegistry.TryResolveStorageKind(reader.GetString(2), out var contract))
                {
                    continue;
                }

                nodes.Add((reader.GetGuid(0), reader.GetString(1), contract.Kind, reader.GetString(3), reader.GetInt32(4)));
            }
        }

        var edges = await ReadEdgesAsync(connection, tenantId, nodes.Select(n => n.Id).ToList(), cancellationToken);

        var result = nodes
            .Select(n => new DefinitionClosureNode(
                n.Id, n.Code, n.Kind, n.Surface, n.Depth,
                edges.TryGetValue(n.Id, out var outgoing) ? outgoing : Array.Empty<DefinitionClosureEdge>()))
            .ToList();

        return ApplicationResult<IReadOnlyList<DefinitionClosureNode>>.Success(result);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<DefinitionClosureEdge>>> ReadEdgesAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        IReadOnlyList<Guid> definitionIds,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<Guid, IReadOnlyList<DefinitionClosureEdge>>();
        if (definitionIds.Count == 0) { return map; }

        await using var command = Command(
            """
            SELECT d.definition_id, d.depends_on_definition_id, st.definition_code,
                   d.dependency_kind, d.is_required, d.depends_on_version
              FROM ppiq_meta.definition_dependencies d
              JOIN ppiq_meta.definition_store st ON st.id = d.depends_on_definition_id
             WHERE d.tenant_id = @tenant
               AND d.is_deleted = false
               AND d.definition_id = ANY(@ids)
               AND d.depends_on_definition_id = ANY(@ids)
             ORDER BY d.definition_id, st.definition_code, d.dependency_kind;
            """, connection);

        Bind(command, "tenant", tenantId);
        command.Parameters.Add(new NpgsqlParameter("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = definitionIds.ToArray()
        });

        var buffer = new Dictionary<Guid, List<DefinitionClosureEdge>>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var from = reader.GetGuid(0);
            if (!buffer.TryGetValue(from, out var list))
            {
                list = new List<DefinitionClosureEdge>();
                buffer[from] = list;
            }

            list.Add(new DefinitionClosureEdge(
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5)));
        }

        foreach (var pair in buffer) { map[pair.Key] = pair.Value; }
        return map;
    }

    private async Task<(string Code, DefinitionKind Kind, string Surface)?> ReadDefinitionAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            """
            SELECT definition_code, definition_kind, surface
              FROM ppiq_meta.definition_store
             WHERE id = @id AND tenant_id = @tenant
             LIMIT 1;
            """, connection);

        Bind(command, "id", definitionId);
        Bind(command, "tenant", tenantId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) { return null; }
        if (!DefinitionKindRegistry.TryResolveStorageKind(reader.GetString(1), out var contract)) { return null; }

        return (reader.GetString(0), contract.Kind, reader.GetString(2));
    }

    // The read model joins the caller's ambient connection when one is open, so
    // a preview taken inside a caller's transaction sees that transaction. It
    // opens nothing of its own beyond the context connection and never begins a
    // transaction: this contract has no write.
    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private NpgsqlCommand Command(string sql, NpgsqlConnection connection)
    {
        var command = new NpgsqlCommand(sql, connection);
        var transaction = _dbContext.Database.CurrentTransaction;
        if (transaction is not null)
        {
            command.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        }

        return command;
    }

    private static void Bind(NpgsqlCommand command, string name, Guid value) =>
        command.Parameters.Add(new NpgsqlParameter(name, NpgsqlDbType.Uuid) { Value = value });
}
