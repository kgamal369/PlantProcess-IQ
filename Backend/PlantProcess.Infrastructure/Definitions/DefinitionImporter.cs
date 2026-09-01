using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Definitions;

/// <summary>
/// PPIQ T-091. The portable importer.
///
/// AN ORCHESTRATOR, NOT A SECOND WRITER. Every semantic row this class causes
/// is written by ICanonicalDefinitionWriter. Import could insert into
/// definition_store and definition_versions directly and it would be shorter,
/// and it would also mean the canonical validation, semantic hashing and
/// idempotence that T-090 exists to own are enforced on one write path and not
/// the other. Two write paths is two authorities.
///
/// EVERYTHING IS VALIDATED BEFORE ANYTHING IS WRITTEN. Format version, ref
/// uniqueness, kind and surface, typed detail, dependency completeness, cycles
/// and existing-definition conflicts are all decided against the target tenant
/// BEFORE the transaction opens. A package that fails at its seventh definition
/// after six were written is a half-installed package, and the customer has no
/// way to tell which half.
///
/// IMPORT IS NOT UPDATE. An existing definition whose semantics differ from the
/// package is a typed conflict. Silently versioning over it would let a stale
/// artifact rewrite live authored work, and the caller would see success.
/// </summary>
public sealed class DefinitionImporter
{
    private readonly PlantProcessDbContext _dbContext;
    private readonly ICanonicalDefinitionWriter _writer;
    private readonly DefinitionExporter _exporter;

    public DefinitionImporter(PlantProcessDbContext dbContext, ICanonicalDefinitionWriter writer, DefinitionExporter exporter)
    {
        _dbContext = dbContext;
        _writer = writer;
        _exporter = exporter;
    }

    public async Task<ApplicationResult<DefinitionImportResult>> ImportAsync(
        Guid tenantId,
        Guid ownerId,
        DefinitionArtifact artifact,
        CancellationToken cancellationToken)
    {
        // ---- validation, before any mutation ------------------------------
        var validation = ValidateStructure(artifact);
        if (validation is not null)
        {
            return ApplicationResult<DefinitionImportResult>.Failure(validation);
        }

        var order = TopologicalOrder(artifact);
        if (order is null)
        {
            return ApplicationResult<DefinitionImportResult>.Failure(ApplicationError.Validation(
                "The artifact dependency graph contains a cycle. It is refused before any write; the database " +
                "cycle trigger is the backstop, not the user-facing check."));
        }

        var connection = await OpenAsync(cancellationToken);

        // CONFLICT PREFLIGHT. Decided against the target tenant while nothing
        // has been written, so a refusal costs nothing and leaves nothing.
        var byRef = artifact.Definitions.ToDictionary(d => d.Ref, StringComparer.Ordinal);
        var existing = new Dictionary<string, ExistingDefinition?>(StringComparer.Ordinal);
        var reusable = new Dictionary<string, ImportedDefinitionRef>(StringComparer.Ordinal);

        foreach (var reference in order)
        {
            var definition = byRef[reference];
            var found = await FindExistingAsync(connection, tenantId, definition.DefinitionCode, cancellationToken);
            existing[reference] = found;

            if (found is null) { continue; }

            if (!string.Equals(found.Value.StorageKind, definition.Kind, StringComparison.Ordinal))
            {
                return ApplicationResult<DefinitionImportResult>.Failure(ApplicationError.Conflict(
                    Conflict(definition, DefinitionImportConflictReason.KindMismatch,
                        "existing kind '" + found.Value.StorageKind + "', imported kind '" + definition.Kind + "'")));
            }

            if (!string.Equals(found.Value.Surface, definition.Surface, StringComparison.Ordinal))
            {
                return ApplicationResult<DefinitionImportResult>.Failure(ApplicationError.Conflict(
                    Conflict(definition, DefinitionImportConflictReason.SurfaceMismatch,
                        "existing surface '" + found.Value.Surface + "', imported surface '" + definition.Surface + "'")));
            }

            // Same code, same kind, same surface: reuse only when the PORTABLE
            // semantics already present equal the incoming ones - compared
            // through the same serializer the round-trip gate uses, not through
            // the writer's internal hash, which is derived integrity and is not
            // reproduced across environments (CENTRAL ruling 7).
            var current = await _exporter.ExportDefinitionAsync(
                tenantId, found.Value.DefinitionId, null, cancellationToken);

            if (current.IsFailure)
            {
                return ApplicationResult<DefinitionImportResult>.Failure(ApplicationError.Conflict(
                    Conflict(definition, DefinitionImportConflictReason.SemanticContentConflict,
                        "the target already holds '" + definition.DefinitionCode +
                        "' but it cannot be compared: " + current.Error!.Message)));
            }

            var existingForm = DefinitionArtifactCanonicalizer.ToCanonicalDefinitionJson(current.Value!);
            var incomingForm = DefinitionArtifactCanonicalizer.ToCanonicalDefinitionJson(definition);

            if (!string.Equals(existingForm, incomingForm, StringComparison.Ordinal))
            {
                var differences = DefinitionArtifactCanonicalizer.SemanticDiff(
                    new DefinitionArtifact(artifact.FormatVersion, "x", new[] { current.Value! with { Ref = "x" } }, Array.Empty<ArtifactDependency>(), null),
                    new DefinitionArtifact(artifact.FormatVersion, "x", new[] { definition with { Ref = "x" } }, Array.Empty<ArtifactDependency>(), null));

                return ApplicationResult<DefinitionImportResult>.Failure(ApplicationError.Conflict(
                    Conflict(definition, DefinitionImportConflictReason.SemanticContentConflict,
                        "the target already holds '" + definition.DefinitionCode +
                        "' with different semantic content (existing version " + current.Value.VersionNumber +
                        "). Portable import does not overwrite an established definition. Differences: " +
                        string.Join("; ", differences))));
            }

            reusable[reference] = new ImportedDefinitionRef(
                reference, found.Value.DefinitionId, definition.DefinitionCode, current.Value.VersionNumber, true);
        }

        // ---- one unit of work ---------------------------------------------
        var owned = _dbContext.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;
        if (owned)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        }

        try
        {
            var map = new Dictionary<string, ImportedDefinitionRef>(StringComparer.Ordinal);
            var written = 0;
            var reused = 0;

            // Dependencies before dependents, so an edge never references a
            // definition that does not exist yet.
            foreach (var reference in order)
            {
                var definition = byRef[reference];

                // Proven equivalent in preflight: the existing published
                // version IS this definition. Nothing is written for it - not
                // even a writer call - so a second identical import produces
                // zero definitions, zero versions, and its edges hit the
                // uniqueness constraint's DO NOTHING.
                if (reusable.TryGetValue(reference, out var reuse))
                {
                    map[reference] = reuse;
                    reused++;
                    continue;
                }

                var wasExisting = existing[reference] is not null;

                var write = new CanonicalDefinitionWrite(
                    Kind: KindOf(definition.Kind),
                    TenantId: tenantId,
                    OwnerId: ownerId,
                    DefinitionCode: definition.DefinitionCode,
                    Name: definition.Name,
                    ContentJson: definition.ContentJson,

                    // Draft first, always. Publication is applied after the
                    // whole graph exists, so a half-installed package is never
                    // visible as published truth.
                    Status: CanonicalVersionStatus.Draft,
                    Detail: DetailFor(definition),
                    Outcomes: OutcomesFor(definition));

                var result = await _writer.WriteVersionAsync(write, cancellationToken);
                if (result.IsFailure)
                {
                    return await FailAsync<DefinitionImportResult>(transaction, result.Error!, cancellationToken);
                }

                var version = result.Value!;

                // The canonical writer returns the existing version unchanged
                // when the semantic hash matches, so a second identical import
                // reuses rather than duplicating. That behaviour is consumed
                // here, never reimplemented with a second hash.
                var isReuse = wasExisting;
                if (isReuse) { reused++; } else { written++; }

                map[reference] = new ImportedDefinitionRef(
                    reference, version.DefinitionId, version.DefinitionCode, version.VersionNumber, isReuse);
            }

            var edges = 0;
            foreach (var dependency in artifact.Dependencies
                         .OrderBy(d => d.FromRef, StringComparer.Ordinal)
                         .ThenBy(d => d.ToRef, StringComparer.Ordinal)
                         .ThenBy(d => d.DependencyKind, StringComparer.Ordinal))
            {
                var from = map[dependency.FromRef];
                var to = map[dependency.ToRef];

                edges += await UpsertEdgeAsync(
                    connection, tenantId, ownerId, from.DefinitionId, to.DefinitionId,
                    dependency.DependencyKind, dependency.IsRequired,
                    ResolveEdgeVersion(dependency, to), cancellationToken);
            }

            // Publication last, dependency-first, once the semantic graph is
            // whole.
            foreach (var reference in order)
            {
                var definition = byRef[reference];
                if (!string.Equals(definition.Status, "published", StringComparison.OrdinalIgnoreCase)) { continue; }

                var imported = map[reference];
                if (imported.Reused) { continue; }

                var published = await _writer.PublishAsync(imported.DefinitionId, imported.VersionNumber, cancellationToken);
                if (published.IsFailure)
                {
                    return await FailAsync<DefinitionImportResult>(transaction, published.Error!, cancellationToken);
                }
            }

            var root = map[artifact.RootRef];

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                await transaction.DisposeAsync();
            }

            return ApplicationResult<DefinitionImportResult>.Success(new DefinitionImportResult(
                root.DefinitionId, root.VersionNumber, written, reused, edges,
                order.Select(r => map[r]).ToList()));
        }
        catch (PostgresException exception)
        {
            // The database backstops remain: a cycle or a constraint the
            // application checks missed still refuses, and still leaves nothing.
            return await FailAsync<DefinitionImportResult>(
                transaction,
                ApplicationError.Validation(
                    "The canonical store refused this artifact (" + exception.SqlState + "): " + exception.MessageText),
                cancellationToken);
        }
    }

    /// <summary>
    /// Format version, ref uniqueness, kind and surface, typed detail shape and
    /// dependency completeness. Everything decidable from the package alone.
    /// </summary>
    private static ApplicationError? ValidateStructure(DefinitionArtifact artifact)
    {
        if (artifact.FormatVersion != DefinitionArtifact.CurrentFormatVersion)
        {
            return ApplicationError.Validation(
                "Artifact format version " + artifact.FormatVersion.ToString(CultureInfo.InvariantCulture) +
                " is not supported by this build (expected " +
                DefinitionArtifact.CurrentFormatVersion.ToString(CultureInfo.InvariantCulture) +
                "). An unknown format is refused rather than partially guessed.");
        }

        if (artifact.Definitions.Count == 0)
        {
            return ApplicationError.Validation("The artifact declares no definitions.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in artifact.Definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.Ref) || !seen.Add(definition.Ref))
            {
                return ApplicationError.Validation(
                    "Duplicate or missing package reference '" + definition.Ref + "'.");
            }

            if (!DefinitionKindRegistry.TryResolveStorageKind(definition.Kind, out var contract))
            {
                return ApplicationError.Validation(
                    "Unknown definition kind '" + definition.Kind + "' on '" + definition.DefinitionCode + "'.");
            }

            if (!string.Equals(contract.Surface, definition.Surface, StringComparison.Ordinal))
            {
                return ApplicationError.Validation(
                    "Kind '" + definition.Kind + "' belongs to surface '" + contract.Surface +
                    "', but the artifact declares '" + definition.Surface + "'.");
            }

            if (definition.Detail is not null)
            {
                foreach (var pair in definition.Detail)
                {
                    if (!contract.TryField(pair.Key, out var field))
                    {
                        return ApplicationError.Validation(
                            "Field '" + pair.Key + "' is not declared on kind '" + definition.Kind + "'.");
                    }

                    if (field.Storage == DefinitionKindRegistry.StorageType.Json &&
                        pair.Value is not null && !IsJson(pair.Value))
                    {
                        return ApplicationError.Validation(
                            "Field '" + pair.Key + "' on '" + definition.DefinitionCode +
                            "' is declared as JSON storage but the artifact carries a value that is not JSON.");
                    }
                }
            }

            try { _ = JsonDocument.Parse(string.IsNullOrWhiteSpace(definition.ContentJson) ? "{}" : definition.ContentJson); }
            catch (JsonException)
            {
                return ApplicationError.Validation(
                    "Definition '" + definition.DefinitionCode + "' carries malformed content JSON.");
            }
        }

        if (!seen.Contains(artifact.RootRef))
        {
            return ApplicationError.Validation(
                "The artifact root reference '" + artifact.RootRef + "' names no definition in the package.");
        }

        foreach (var dependency in artifact.Dependencies)
        {
            if (!seen.Contains(dependency.FromRef) || !seen.Contains(dependency.ToRef))
            {
                return ApplicationError.Validation(
                    "Dependency edge " + dependency.FromRef + " -> " + dependency.ToRef +
                    " references a definition the package does not carry.");
            }

            if (string.Equals(dependency.FromRef, dependency.ToRef, StringComparison.Ordinal))
            {
                return ApplicationError.Validation("A definition cannot depend on itself.");
            }
        }

        return null;
    }

    /// <summary>
    /// Dependencies first. Returns null when the package graph cannot be
    /// ordered, which is exactly the cycle case.
    /// </summary>
    private static List<string>? TopologicalOrder(DefinitionArtifact artifact)
    {
        var refs = artifact.Definitions
            .Select(d => d.Ref)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        var requires = refs.ToDictionary(r => r, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var dependency in artifact.Dependencies)
        {
            requires[dependency.FromRef].Add(dependency.ToRef);
        }

        var ordered = new List<string>();
        var placed = new HashSet<string>(StringComparer.Ordinal);

        while (ordered.Count < refs.Count)
        {
            // Deterministic: among everything currently satisfiable, the
            // ordinally smallest ref goes next, so two runs order identically.
            var next = refs.FirstOrDefault(r => !placed.Contains(r) && requires[r].All(placed.Contains));
            if (next is null) { return null; }

            ordered.Add(next);
            placed.Add(next);
        }

        return ordered;
    }

    private static string Conflict(ArtifactDefinition definition, DefinitionImportConflictReason reason, string detail) =>
        "IMPORT_CONFLICT[" + reason + "] ref=" + definition.Ref +
        " code=" + definition.DefinitionCode +
        " importedHash=" + definition.DefinitionHash +
        " version=" + definition.VersionNumber.ToString(CultureInfo.InvariantCulture) +
        ": " + detail;

    private static int? ResolveEdgeVersion(ArtifactDependency dependency, ImportedDefinitionRef target) =>
        dependency.DependsOnVersion.HasValue ? target.VersionNumber : null;

    private static DefinitionKind KindOf(string storageKind)
    {
        DefinitionKindRegistry.TryResolveStorageKind(storageKind, out var contract);
        return contract.Kind;
    }

    private static IReadOnlyDictionary<string, object?>? DetailFor(ArtifactDefinition definition)
    {
        if (definition.Detail is null || definition.Detail.Count == 0) { return null; }

        // Symmetric with the exporter: a null-valued key in a hand-authored
        // artifact is treated as an undeclared field, because declaring
        // "key = null" would hash differently from the absence the source was
        // hashed with. (W1-T091-NULLDETAIL-01)
        var detail = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in definition.Detail)
        {
            if (pair.Value is null) { continue; }
            detail[pair.Key] = pair.Value;
        }

        return detail.Count == 0 ? null : detail;
    }

    private static IReadOnlyList<CanonicalOutcomeDeclaration>? OutcomesFor(ArtifactDefinition definition)
    {
        if (definition.Outcomes is null || definition.Outcomes.Count == 0) { return null; }

        return definition.Outcomes
            .Select(o => new CanonicalOutcomeDeclaration(
                o.OutcomeCode, o.OutcomeType, o.ClassTaxonomyRef, o.OrdinalRankMapJson, o.GrainCode,
                o.DetectionPositionCode, o.DetectionTimestampField, o.Direction, o.UnitCode, o.CensoringPolicy))
            .ToList();
    }

    private static bool IsJson(string value)
    {
        try { _ = JsonDocument.Parse(value); return true; }
        catch (JsonException) { return false; }
    }

    private async Task<ApplicationResult<T>> FailAsync<T>(
        IDbContextTransaction? transaction, ApplicationError error, CancellationToken cancellationToken)
    {
        if (transaction is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        return ApplicationResult<T>.Failure(error);
    }

    private async Task<ExistingDefinition?> FindExistingAsync(
        NpgsqlConnection connection, Guid tenantId, string definitionCode, CancellationToken cancellationToken)
    {
        await using var command = Command(
            """
            SELECT id, definition_kind, surface FROM ppiq_meta.definition_store
             WHERE tenant_id = @tenant_id AND definition_code = @definition_code
             LIMIT 1;
            """, connection);

        command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId });
        command.Parameters.Add(new NpgsqlParameter("definition_code", NpgsqlDbType.Text) { Value = definitionCode });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) { return null; }

        return new ExistingDefinition(reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
    }

    /// <summary>
    /// Writes one dependency edge into the existing T-089 authority. Not a new
    /// registry: the same table, the same uniqueness, the same cycle trigger.
    /// ON CONFLICT DO NOTHING is what makes a second identical import add no
    /// second edge.
    /// </summary>
    private async Task<int> UpsertEdgeAsync(
        NpgsqlConnection connection,
        Guid tenantId,
        Guid ownerId,
        Guid definitionId,
        Guid dependsOnDefinitionId,
        string dependencyKind,
        bool isRequired,
        int? dependsOnVersion,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            """
            INSERT INTO ppiq_meta.definition_dependencies
                (tenant_id, definition_id, depends_on_definition_id, depends_on_version,
                 dependency_kind, is_required, created_by)
            VALUES (@tenant_id, @definition_id, @depends_on_definition_id, @depends_on_version,
                    @dependency_kind, @is_required, @created_by)
            ON CONFLICT (definition_id, depends_on_definition_id, dependency_kind) DO NOTHING;
            """, connection);

        command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId });
        command.Parameters.Add(new NpgsqlParameter("definition_id", NpgsqlDbType.Uuid) { Value = definitionId });
        command.Parameters.Add(new NpgsqlParameter("depends_on_definition_id", NpgsqlDbType.Uuid) { Value = dependsOnDefinitionId });
        command.Parameters.Add(new NpgsqlParameter("dependency_kind", NpgsqlDbType.Text) { Value = dependencyKind });
        command.Parameters.Add(new NpgsqlParameter("is_required", NpgsqlDbType.Boolean) { Value = isRequired });
        command.Parameters.Add(new NpgsqlParameter("created_by", NpgsqlDbType.Uuid) { Value = ownerId });
        command.Parameters.Add(new NpgsqlParameter("depends_on_version", NpgsqlDbType.Integer)
        {
            Value = dependsOnVersion.HasValue ? dependsOnVersion.Value : (object)DBNull.Value
        });

        return await command.ExecuteNonQueryAsync(cancellationToken);
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

    private readonly record struct ExistingDefinition(Guid DefinitionId, string StorageKind, string Surface);
}
