using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Definitions;

/// <summary>
/// PPIQ T-091. The generic portable exporter.
///
/// ONE EXPORTER, NOT SIXTEEN. Every kind-specific fact this class needs comes
/// from DefinitionKindRegistry: which detail table a kind has, which fields are
/// declared on it and how each field is stored. A switch over kinds here would
/// mean every future kind needs an exporter change, and the registry would stop
/// being the authority it was built to be in T-090.
///
/// EVERY EXPORTED VERSION IS EXACT. The root resolves to an explicitly
/// requested version or to the published one. Each dependency resolves to its
/// pinned version when the edge pins one, and otherwise to its published
/// version AT EXPORT TIME, which is then written into the artifact as a pin.
/// An artifact that says "whatever is latest over there" would reproduce a
/// different system on import and would still call itself the same package.
/// </summary>
public sealed class DefinitionExporter
{
    private readonly PlantProcessDbContext _dbContext;
    private readonly ICanonicalDefinitionGraph _graph;

    public DefinitionExporter(PlantProcessDbContext dbContext, ICanonicalDefinitionGraph graph)
    {
        _dbContext = dbContext;
        _graph = graph;
    }

    public async Task<ApplicationResult<DefinitionArtifact>> ExportAsync(
        Guid tenantId,
        Guid definitionId,
        int? versionNumber,
        CancellationToken cancellationToken)
    {
        var closure = await _graph.ResolveClosureAsync(tenantId, definitionId, cancellationToken);
        if (closure.IsFailure)
        {
            return ApplicationResult<DefinitionArtifact>.Failure(closure.Error!);
        }

        var nodes = closure.Value!;
        if (nodes.Count == 0)
        {
            return ApplicationResult<DefinitionArtifact>.Failure(ApplicationError.NotFound(
                "No definition with that identity exists in this tenant."));
        }

        var connection = await OpenAsync(cancellationToken);

        // PINS FIRST. A dependency reached by several edges may be pinned by
        // one of them. Two edges pinning DIFFERENT versions of the same
        // dependency is a contradiction in the source graph, not something to
        // resolve by preferring one: the artifact would silently drop a
        // requirement somebody wrote down.
        var pins = new Dictionary<Guid, int>();
        foreach (var node in nodes)
        {
            foreach (var edge in node.Requires)
            {
                if (!edge.DependsOnVersion.HasValue) { continue; }
                if (pins.TryGetValue(edge.DependsOnDefinitionId, out var existing) && existing != edge.DependsOnVersion.Value)
                {
                    return ApplicationResult<DefinitionArtifact>.Failure(ApplicationError.Conflict(
                        "Dependency '" + edge.DependsOnDefinitionCode + "' is pinned to two different versions (" +
                        existing.ToString(CultureInfo.InvariantCulture) + " and " +
                        edge.DependsOnVersion.Value.ToString(CultureInfo.InvariantCulture) +
                        ") within this closure. The exported artifact cannot be truthful about which one it requires."));
                }

                pins[edge.DependsOnDefinitionId] = edge.DependsOnVersion.Value;
            }
        }

        // DETERMINISTIC PACKAGE REFS. Assigned after ordering the closure by
        // (kind, code, id) so the same graph always produces the same refs, and
        // the refs therefore do not disturb the canonical hash. Environment
        // uuids never take this role.
        var ordered = nodes
            .OrderBy(n => n.Kind)
            .ThenBy(n => n.DefinitionCode, StringComparer.Ordinal)
            .ThenBy(n => n.DefinitionId)
            .ToList();

        var refByDefinition = new Dictionary<Guid, string>();
        for (var index = 0; index < ordered.Count; index++)
        {
            refByDefinition[ordered[index].DefinitionId] = "d" + (index + 1).ToString("D4", CultureInfo.InvariantCulture);
        }

        var definitions = new List<ArtifactDefinition>();
        foreach (var node in ordered)
        {
            var isRoot = node.DefinitionId == definitionId;

            int? requested = isRoot
                ? versionNumber
                : (pins.TryGetValue(node.DefinitionId, out var pinned) ? pinned : null);

            var version = await ResolveVersionAsync(connection, node.DefinitionId, requested, cancellationToken);
            if (version is null)
            {
                return ApplicationResult<DefinitionArtifact>.Failure(ApplicationError.Validation(
                    "DEFINITION_VERSION_NOT_EXPORTABLE: '" + node.DefinitionCode + "' has no " +
                    (requested.HasValue
                        ? "version " + requested.Value.ToString(CultureInfo.InvariantCulture)
                        : "published version") +
                    ". An unpinned dependency with nothing published cannot be exported, and a version will not be invented for it."));
            }

            if (!DefinitionKindRegistry.TryResolve(node.Kind, out var contract))
            {
                return ApplicationResult<DefinitionArtifact>.Failure(ApplicationError.Validation(
                    "No registered kind contract for '" + node.DefinitionCode + "'."));
            }

            var detail = await ReadDetailAsync(connection, contract, version.Value.VersionId, cancellationToken);
            if (detail.IsFailure)
            {
                return ApplicationResult<DefinitionArtifact>.Failure(detail.Error!);
            }

            var outcomes = await ReadOutcomesAsync(connection, version.Value.VersionId, cancellationToken);

            definitions.Add(new ArtifactDefinition(
                Ref: refByDefinition[node.DefinitionId],
                DefinitionCode: node.DefinitionCode,
                Kind: contract.StorageKind,
                Surface: contract.Surface,
                Name: version.Value.Name,
                VersionNumber: version.Value.VersionNumber,
                Status: version.Value.Status,
                ContentJson: version.Value.ContentJson,
                DefinitionHash: version.Value.DefinitionHash,
                Detail: detail.Value!.Count == 0 ? null : detail.Value,
                Outcomes: outcomes.Count == 0 ? null : outcomes,
                SourceDefinitionId: node.DefinitionId,
                SourceVersionId: version.Value.VersionId));
        }

        var exportedVersions = definitions.ToDictionary(d => d.Ref, d => d.VersionNumber, StringComparer.Ordinal);

        // EDGES CARRY RESOLVED VERSIONS. Whether the source edge pinned one or
        // not, the artifact records the exact version this export resolved, so
        // the package is reproducible rather than time-dependent.
        var dependencies = new List<ArtifactDependency>();
        foreach (var node in ordered)
        {
            foreach (var edge in node.Requires)
            {
                if (!refByDefinition.TryGetValue(edge.DependsOnDefinitionId, out var toRef)) { continue; }

                dependencies.Add(new ArtifactDependency(
                    FromRef: refByDefinition[node.DefinitionId],
                    ToRef: toRef,
                    DependencyKind: edge.DependencyKind,
                    IsRequired: edge.IsRequired,
                    DependsOnVersion: edge.DependsOnVersion ?? exportedVersions[toRef]));
            }
        }

        return ApplicationResult<DefinitionArtifact>.Success(new DefinitionArtifact(
            FormatVersion: DefinitionArtifact.CurrentFormatVersion,
            RootRef: refByDefinition[definitionId],
            Definitions: definitions,
            Dependencies: dependencies,
            Metadata: new ArtifactMetadata(DateTime.UtcNow, null, null)));
    }

    /// <summary>
    /// One definition's portable form, read exactly the way the full export
    /// reads it. The importer uses this to compare an incoming definition with
    /// what the target already holds - same serializer both sides, so the
    /// conflict decision and the round-trip gate cannot disagree.
    /// </summary>
    public async Task<ApplicationResult<ArtifactDefinition>> ExportDefinitionAsync(
        Guid tenantId,
        Guid definitionId,
        int? versionNumber,
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);

        await using var store = Command(
            "SELECT definition_code, definition_kind, surface FROM ppiq_meta.definition_store " +
            "WHERE id = @id AND tenant_id = @tenant LIMIT 1;", connection);
        store.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = definitionId });
        store.Parameters.Add(new NpgsqlParameter("tenant", NpgsqlDbType.Uuid) { Value = tenantId });

        string code; string storageKind; string surface;
        await using (var reader = await store.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
            {
                return ApplicationResult<ArtifactDefinition>.Failure(ApplicationError.NotFound(
                    "No definition with that identity exists in this tenant."));
            }

            code = reader.GetString(0); storageKind = reader.GetString(1); surface = reader.GetString(2);
        }

        if (!DefinitionKindRegistry.TryResolveStorageKind(storageKind, out var contract))
        {
            return ApplicationResult<ArtifactDefinition>.Failure(ApplicationError.Validation(
                "No registered kind contract for '" + code + "'."));
        }

        var version = await ResolveVersionAsync(connection, definitionId, versionNumber, cancellationToken);
        if (version is null)
        {
            return ApplicationResult<ArtifactDefinition>.Failure(ApplicationError.Validation(
                "DEFINITION_VERSION_NOT_EXPORTABLE: '" + code + "' has no " +
                (versionNumber.HasValue ? "version " + versionNumber.Value : "published version") + "."));
        }

        var detail = await ReadDetailAsync(connection, contract, version.Value.VersionId, cancellationToken);
        if (detail.IsFailure) { return ApplicationResult<ArtifactDefinition>.Failure(detail.Error!); }

        var outcomes = await ReadOutcomesAsync(connection, version.Value.VersionId, cancellationToken);

        return ApplicationResult<ArtifactDefinition>.Success(new ArtifactDefinition(
            Ref: "existing", DefinitionCode: code, Kind: contract.StorageKind, Surface: surface,
            Name: version.Value.Name, VersionNumber: version.Value.VersionNumber, Status: version.Value.Status,
            ContentJson: version.Value.ContentJson, DefinitionHash: version.Value.DefinitionHash,
            Detail: detail.Value!.Count == 0 ? null : detail.Value,
            Outcomes: outcomes.Count == 0 ? null : outcomes,
            SourceDefinitionId: definitionId, SourceVersionId: version.Value.VersionId));
    }

    private async Task<(Guid VersionId, int VersionNumber, string Status, string ContentJson, string DefinitionHash, string Name)?>
        ResolveVersionAsync(NpgsqlConnection connection, Guid definitionId, int? versionNumber, CancellationToken cancellationToken)
    {
        var sql = versionNumber.HasValue
            ? """
              SELECT v.id, v.version_number, v.status, v.graph_json::text, v.definition_hash, s.name
                FROM ppiq_meta.definition_versions v
                JOIN ppiq_meta.definition_store s ON s.id = v.definition_id
               WHERE v.definition_id = @definition_id AND v.version_number = @version_number
               LIMIT 1;
              """
            : """
              SELECT v.id, v.version_number, v.status, v.graph_json::text, v.definition_hash, s.name
                FROM ppiq_meta.definition_versions v
                JOIN ppiq_meta.definition_store s ON s.id = v.definition_id
               WHERE v.definition_id = @definition_id AND v.status = 'published'
               ORDER BY v.version_number DESC
               LIMIT 1;
              """;

        await using var command = Command(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("definition_id", NpgsqlDbType.Uuid) { Value = definitionId });
        if (versionNumber.HasValue)
        {
            command.Parameters.Add(new NpgsqlParameter("version_number", NpgsqlDbType.Integer) { Value = versionNumber.Value });
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) { return null; }

        return (reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3),
                reader.GetString(4), reader.GetString(5));
    }

    /// <summary>
    /// Reads the declared detail fields for a kind, driven entirely by the
    /// registry. Values become strings in a stable representation so the
    /// canonical form is stable; a storage type the exporter cannot represent
    /// is a typed refusal, never a silently dropped field.
    /// </summary>
    private async Task<ApplicationResult<IReadOnlyDictionary<string, string?>>> ReadDetailAsync(
        NpgsqlConnection connection,
        DefinitionKindRegistry.KindContract contract,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var empty = (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.Ordinal);
        if (contract.DetailTable is null || contract.WritableFields.Count == 0)
        {
            return ApplicationResult<IReadOnlyDictionary<string, string?>>.Success(empty);
        }

        foreach (var field in contract.WritableFields)
        {
            if (IsEnvironmentSecret(field.Name))
            {
                return ApplicationResult<IReadOnlyDictionary<string, string?>>.Failure(ApplicationError.Validation(
                    "Field '" + field.Name + "' on kind '" + contract.StorageKind +
                    "' names environment-local secret material and must not travel inside a portable artifact."));
            }

            if (field.Storage is not (DefinitionKindRegistry.StorageType.Text
                or DefinitionKindRegistry.StorageType.Json
                or DefinitionKindRegistry.StorageType.Integer
                or DefinitionKindRegistry.StorageType.Uuid
                or DefinitionKindRegistry.StorageType.Boolean))
            {
                return ApplicationResult<IReadOnlyDictionary<string, string?>>.Failure(ApplicationError.Validation(
                    "Storage type '" + field.Storage + "' on field '" + field.Name +
                    "' has no portable representation. The exporter refuses rather than dropping the field."));
            }
        }

        var columns = string.Join(", ", contract.WritableFields
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => f.Storage == DefinitionKindRegistry.StorageType.Json
                ? f.Name + "::text AS " + f.Name
                : f.Name + "::text AS " + f.Name));

        await using var command = Command(
            "SELECT " + columns + " FROM ppiq_meta." + contract.DetailTable +
            " WHERE definition_version_id = @version_id LIMIT 1;", connection);
        command.Parameters.Add(new NpgsqlParameter("version_id", NpgsqlDbType.Uuid) { Value = versionId });

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                // ABSENT STAYS ABSENT. A NULL column means the field was never
                // declared on this version, and the canonical hash rightly
                // distinguishes "key absent" from "key = null". Materialising
                // the null here put a fifth key into an artifact whose source
                // was hashed over four, and the imported twin could never hash
                // equal to its original. (W1-T091-NULLDETAIL-01)
                if (reader.IsDBNull(index)) { continue; }
                values[reader.GetName(index)] = reader.GetString(index);
            }
        }

        return ApplicationResult<IReadOnlyDictionary<string, string?>>.Success(values);
    }

    private async Task<IReadOnlyList<ArtifactOutcome>> ReadOutcomesAsync(
        NpgsqlConnection connection,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            """
            SELECT outcome_code, outcome_type, class_taxonomy_ref, ordinal_rank_map::text,
                   grain_code, detection_position_code, detection_timestamp_field,
                   direction, unit_code, censoring_policy
              FROM ppiq_meta.outcome_details
             WHERE definition_version_id = @version_id
             ORDER BY outcome_code;
            """, connection);
        command.Parameters.Add(new NpgsqlParameter("version_id", NpgsqlDbType.Uuid) { Value = versionId });

        var outcomes = new List<ArtifactOutcome>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            outcomes.Add(new ArtifactOutcome(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9)));
        }

        return outcomes;
    }

    private static bool IsEnvironmentSecret(string fieldName)
    {
        foreach (var marker in new[] { "password", "secret", "token", "credential", "connection_string", "vault" })
        {
            if (fieldName.Contains(marker, StringComparison.OrdinalIgnoreCase)) { return true; }
        }

        return false;
    }

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
}
