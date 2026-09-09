using System.Data;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Common.Canonical;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Definitions.Canvas;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Definitions.Canvas;

/// <summary>
/// PPIQ T-244. THE ONE IMPLEMENTATION OF THE CANVAS DEFINITION LIFECYCLE.
///
/// TRANSACTION LAW. Every mutation opens the transaction on the DbContext,
/// hands the same connection and transaction to the canonical writer (which
/// requires exactly that) and to the compatibility projection (which can
/// accept nothing else), and commits once. A failure anywhere rolls back
/// everything. There is no second connection, no second transaction and no
/// autocommit statement on the mutation path.
///
/// SAFETY BEFORE PERSISTENCE. Authored SQL is put through the server's own
/// safe-SQL resolver before the transaction opens. An unsafe statement is
/// refused with the resolver's error code and leaves no trace anywhere.
/// Canonical identity does not grant execution trust; both remain required.
///
/// IDENTITY IS RESOLVED PER TENANT. Every lookup goes through
/// FindByCodeAsync with the caller's tenant. A code that belongs to another
/// tenant does not resolve, so it cannot be read, versioned or published.
/// </summary>
public sealed class CanvasDefinitionLifecycleService : ICanvasDefinitionLifecycle
{
    private const string SqlProjectionStatus = "Published";
    private const string TransformationProjectionMode = "declared";

    // T-253. The typed refusal vocabulary for the governed output target. The codes are
    // stable strings because a browser branches on them; the sentence beside each one is
    // what a person reads and may be reworded freely.
    private const string TargetRequiredCode = "OUTPUT_TARGET_REQUIRED";
    private const string TargetNotCanonicalCode = "OUTPUT_TARGET_NOT_CANONICAL";
    private const string TargetMismatchCode = "OUTPUT_TARGET_MISMATCH";

    private readonly PlantProcessDbContext _db;
    private readonly ICanonicalDefinitionWriter _writer;
    private readonly ICanvasCompatibilityProjection _projection;
    private readonly ICanonicalEntityCatalog _canonicalEntities;

    public CanvasDefinitionLifecycleService(
        PlantProcessDbContext db,
        ICanonicalDefinitionWriter writer,
        ICanvasCompatibilityProjection projection,
        ICanonicalEntityCatalog canonicalEntities)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        _canonicalEntities = canonicalEntities ?? throw new ArgumentNullException(nameof(canonicalEntities));
    }

    // ------------------------------------------------------------------ SAVE

    public async Task<ApplicationResult<CanvasDefinitionVersion>> SaveGraphAsync(
        CanvasGraphSave save,
        CancellationToken cancellationToken)
    {
        if (save is null) { return Refuse("A graph save is required."); }
        var identity = ValidateIdentity(save.TenantId, save.OwnerId, save.DefinitionCode);
        if (identity is not null) { return Refuse(identity); }

        // T-253. Checked against the canonical projection-target set BEFORE the content
        // is built, so what a client receives carries a code it can branch on rather
        // than a serialisation exception message.
        var targetRefusal = RefuseTargetOrNull(save.OutputTarget);
        if (targetRefusal is not null) { return targetRefusal; }

        var declaredInGraph = CanvasDefinitionContent.TryReadGraphTargetEntity(save.GraphJson);
        if (declaredInGraph is not null
            && !string.Equals(declaredInGraph, save.OutputTarget!.Trim(), StringComparison.Ordinal))
        {
            return RefuseTyped(
                TargetMismatchCode,
                "The graph names '" + declaredInGraph + "' as its target entity while the governed output target is '"
                + save.OutputTarget!.Trim() + "'. One definition cannot carry two output identities.");
        }

        string content;
        try
        {
            content = CanvasDefinitionContent.ForGraph(save.GraphJson, save.OutputTarget!);
        }
        catch (ArgumentException ex)
        {
            return Refuse("The graph cannot be saved: " + ex.Message);
        }

        return await WriteDraftAsync(
            save.TenantId, save.OwnerId, save.DefinitionCode, save.DisplayName, content, cancellationToken);
    }

    public async Task<ApplicationResult<CanvasDefinitionVersion>> SaveSqlAsync(
        CanvasSqlSave save,
        CancellationToken cancellationToken)
    {
        if (save is null) { return Refuse("A SQL save is required."); }
        var identity = ValidateIdentity(save.TenantId, save.OwnerId, save.DefinitionCode);
        if (identity is not null) { return Refuse(identity); }

        if (string.IsNullOrWhiteSpace(save.Sql))
        {
            return Refuse("Authored SQL is required.");
        }

        // T-253. Checked before the validator runs, so an author who has not chosen a
        // target is told that, and not handed a SQL verdict about a different problem.
        var sqlTargetRefusal = RefuseTargetOrNull(save.OutputTarget);
        if (sqlTargetRefusal is not null) { return sqlTargetRefusal; }

        // The server's own safety authority decides. Same function the run
        // path uses, same rules, no second validator - and nothing is written
        // until it has answered.
        var verdict = await ResolveSafeSqlAsync(save.Sql, cancellationToken);
        if (!verdict.IsValid)
        {
            return ApplicationResult<CanvasDefinitionVersion>.Failure(ApplicationError.Validation(
                "Not saved. A definition that cannot run is not a definition. " + verdict.Message,
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["errorCode"] = new[] { verdict.ErrorCode },
                }));
        }

        string content;
        try
        {
            content = CanvasDefinitionContent.ForSql(
                verdict.NormalizedSql, save.ForkedFromGraphJson, save.OutputTarget!);
        }
        catch (ArgumentException ex)
        {
            return Refuse("The SQL definition cannot be saved: " + ex.Message);
        }

        return await WriteDraftAsync(
            save.TenantId, save.OwnerId, save.DefinitionCode, save.DisplayName, content, cancellationToken);
    }

    private async Task<ApplicationResult<CanvasDefinitionVersion>> WriteDraftAsync(
        Guid tenantId,
        Guid ownerId,
        string definitionCode,
        string displayName,
        string content,
        CancellationToken cancellationToken)
    {
        var write = new CanonicalDefinitionWrite(
            DefinitionKind.Transformation,
            tenantId,
            ownerId,
            definitionCode.Trim(),
            string.IsNullOrWhiteSpace(displayName) ? definitionCode.Trim() : displayName.Trim(),
            content,
            CanonicalVersionStatus.Draft,
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["projection_mode"] = TransformationProjectionMode,
            });

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var written = await _writer.WriteVersionAsync(write, cancellationToken);
        if (written.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ApplicationResult<CanvasDefinitionVersion>.Failure(written.Error!);
        }

        await transaction.CommitAsync(cancellationToken);
        return ApplicationResult<CanvasDefinitionVersion>.Success(ToCanvasVersion(written.Value!));
    }

    // --------------------------------------------------------------- PUBLISH

    public async Task<ApplicationResult<CanvasDefinitionVersion>> PublishAsync(
        Guid tenantId,
        string definitionCode,
        int versionNumber,
        CanvasProjectionHandles handles,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) { return Refuse("A tenant is required."); }
        if (string.IsNullOrWhiteSpace(definitionCode)) { return Refuse("A definition code is required."); }
        if (versionNumber <= 0) { return Refuse("A positive version number is required."); }

        var found = await _writer.FindByCodeAsync(tenantId, definitionCode.Trim(), cancellationToken);
        if (found.IsFailure) { return ApplicationResult<CanvasDefinitionVersion>.Failure(found.Error!); }
        if (found.Value is null)
        {
            return ApplicationResult<CanvasDefinitionVersion>.Failure(
                ApplicationError.NotFound("No definition with code '" + definitionCode + "' exists for this tenant."));
        }

        var definitionId = found.Value.Value;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var published = await _writer.PublishAsync(definitionId, versionNumber, cancellationToken);
        if (published.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ApplicationResult<CanvasDefinitionVersion>.Failure(published.Error!);
        }

        var version = published.Value!;
        var representation = CanvasDefinitionContent.Read(version.ContentJson);

        try
        {
            var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
            var npgsqlTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();

            if (representation.Representation == CanvasDefinitionContent.RepresentationGraph)
            {
                if (handles.SessionId is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Refuse("A graph definition cannot be published without its session handle for the execution projection.");
                }

                await _projection.ProjectGraphVersionAsync(
                    connection, npgsqlTransaction,
                    tenantId, handles.SessionId.Value, version.VersionNumber,
                    representation.GraphJson ?? "{}",
                    handles.PublishedBy ?? "canvas",
                    cancellationToken);
            }
            else
            {
                // Built as a JsonObject rather than an anonymous type. The
                // forked graph is already JSON text, and a nullable
                // JsonElement in an anonymous object has no inferable common
                // type with null - it is a value type. Composing the node
                // keeps the absent case genuinely absent instead of emitting
                // a null the projection would have to interpret.
                var projectionNode = new JsonObject
                {
                    ["body"] = "sql",
                    ["sql"] = representation.Sql,
                    ["canonicalDefinitionId"] = definitionId.ToString(),
                    ["canonicalVersion"] = version.VersionNumber,
                };

                if (!string.IsNullOrWhiteSpace(representation.ForkedFromGraphJson))
                {
                    projectionNode["forkedFromGraph"] = JsonNode.Parse(representation.ForkedFromGraphJson);
                }

                var definitionJson = projectionNode.ToJsonString();

                await _projection.ProjectSqlVersionAsync(
                    connection, npgsqlTransaction,
                    definitionCode.Trim(), handles.DisplayName, handles.CanonicalEntity,
                    version.VersionNumber, definitionJson, SqlProjectionStatus,
                    cancellationToken);
            }
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException)
        {
            // The canonical publish succeeded inside this transaction and the
            // projection did not. Neither survives: that is the whole point.
            await transaction.RollbackAsync(cancellationToken);
            return ApplicationResult<CanvasDefinitionVersion>.Failure(ApplicationError.Infrastructure(
                "Published nothing. The execution projection failed and the canonical publish was rolled back with it: " + ex.Message));
        }

        await transaction.CommitAsync(cancellationToken);
        return ApplicationResult<CanvasDefinitionVersion>.Success(ToCanvasVersion(version));
    }

    // ---------------------------------------------------------------- REOPEN

    public async Task<ApplicationResult<CanvasDefinitionVersion>> ReopenAsync(
        Guid tenantId,
        string definitionCode,
        int? versionNumber,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) { return Refuse("A tenant is required."); }
        if (string.IsNullOrWhiteSpace(definitionCode)) { return Refuse("A definition code is required."); }

        var found = await _writer.FindByCodeAsync(tenantId, definitionCode.Trim(), cancellationToken);
        if (found.IsFailure) { return ApplicationResult<CanvasDefinitionVersion>.Failure(found.Error!); }
        if (found.Value is null)
        {
            return ApplicationResult<CanvasDefinitionVersion>.Failure(
                ApplicationError.NotFound("No definition with code '" + definitionCode + "' exists for this tenant."));
        }

        var definitionId = found.Value.Value;
        var target = versionNumber ?? await CurrentVersionNumberAsync(definitionId, cancellationToken);
        if (target <= 0)
        {
            return ApplicationResult<CanvasDefinitionVersion>.Failure(
                ApplicationError.NotFound("Definition '" + definitionCode + "' has no version to reopen."));
        }

        var resolved = await _writer.ResolveExactAsync(definitionId, target, cancellationToken);
        if (resolved.IsFailure) { return ApplicationResult<CanvasDefinitionVersion>.Failure(resolved.Error!); }

        try
        {
            return ApplicationResult<CanvasDefinitionVersion>.Success(ToCanvasVersion(resolved.Value!));
        }
        catch (InvalidOperationException ex)
        {
            return Refuse(ex.Message);
        }
    }

    // ------------------------------------------------------------- T-243 HISTORY

    public async Task<ApplicationResult<IReadOnlyList<CanvasVersionSummary>>> ListVersionsAsync(
        Guid tenantId,
        string definitionCode,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return ApplicationResult<IReadOnlyList<CanvasVersionSummary>>.Failure(
                ApplicationError.Validation("A tenant is required."));
        }
        if (string.IsNullOrWhiteSpace(definitionCode))
        {
            return ApplicationResult<IReadOnlyList<CanvasVersionSummary>>.Failure(
                ApplicationError.Validation("A definition code is required."));
        }

        var found = await _writer.FindByCodeAsync(tenantId, definitionCode.Trim(), cancellationToken);
        if (found.IsFailure)
        {
            return ApplicationResult<IReadOnlyList<CanvasVersionSummary>>.Failure(found.Error!);
        }
        if (found.Value is null)
        {
            return ApplicationResult<IReadOnlyList<CanvasVersionSummary>>.Failure(
                ApplicationError.NotFound("No definition with code '" + definitionCode + "' exists for this tenant."));
        }

        var definitionId = found.Value.Value;
        var current = await CurrentVersionNumberAsync(definitionId, cancellationToken);

        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) { await connection.OpenAsync(cancellationToken); }

        // Tenant is in the predicate as well as the definition id. A definition id that
        // resolved under this tenant cannot carry another tenant's versions, but the
        // predicate says so rather than relying on that being true.
        await using var command = new NpgsqlCommand(
            "SELECT version_number, status, definition_hash, created_at_utc "
            + "FROM ppiq_meta.definition_versions "
            + "WHERE definition_id = @id AND tenant_id = @t AND is_deleted = false "
            + "ORDER BY version_number DESC;", connection);
        command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = definitionId });
        command.Parameters.Add(new NpgsqlParameter("t", NpgsqlDbType.Uuid) { Value = tenantId });

        var versions = new List<CanvasVersionSummary>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var number = reader.GetInt32(0);
                versions.Add(new CanvasVersionSummary(
                    number,
                    reader.IsDBNull(1) ? "unknown" : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.IsDBNull(3) ? DateTime.MinValue : reader.GetDateTime(3),
                    number == current));
            }
        }

        return ApplicationResult<IReadOnlyList<CanvasVersionSummary>>.Success(versions);
    }

    // --------------------------------------------------------------- HELPERS

    private static string? ValidateIdentity(Guid tenantId, Guid ownerId, string definitionCode)
    {
        if (tenantId == Guid.Empty) { return "A tenant is required."; }
        if (ownerId == Guid.Empty) { return "An owner is required."; }
        if (string.IsNullOrWhiteSpace(definitionCode)) { return "A definition code is required."; }
        if (definitionCode.Trim().Length > 128) { return "A definition code is at most 128 characters."; }
        return null;
    }

    private static CanvasDefinitionVersion ToCanvasVersion(CanonicalDefinitionVersion version) =>
        new(
            version.DefinitionId,
            version.VersionId,
            version.DefinitionCode,
            version.VersionNumber,
            version.Status.ToString(),
            version.DefinitionHash,
            CanvasDefinitionContent.Read(version.ContentJson));

    private static ApplicationResult<CanvasDefinitionVersion> Refuse(string message) =>
        ApplicationResult<CanvasDefinitionVersion>.Failure(ApplicationError.Validation(message));

    private static ApplicationResult<CanvasDefinitionVersion> RefuseTyped(string code, string message) =>
        ApplicationResult<CanvasDefinitionVersion>.Failure(ApplicationError.Validation(
            message,
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["errorCode"] = new[] { code },
            }));

    /// <summary>
    /// T-253. The governed output target, or a typed refusal. Null means it is good.
    ///
    /// The catalogue is the authority and it answers from the mapped model plus the
    /// Domain projection-target marker, so this refuses a name the product cannot
    /// resolve AND a mapped entity that is not something a plant authors output into.
    /// It never substitutes a default and never falls back to a physical relation.
    /// </summary>
    private ApplicationResult<CanvasDefinitionVersion>? RefuseTargetOrNull(string? outputTarget)
    {
        if (string.IsNullOrWhiteSpace(outputTarget))
        {
            return RefuseTyped(
                TargetRequiredCode,
                "This definition has no governed output target. Choose one before saving; it is never defaulted.");
        }

        var target = outputTarget.Trim();
        if (!_canonicalEntities.IsProjectionTarget(target))
        {
            return RefuseTyped(
                TargetNotCanonicalCode,
                "'" + target + "' is not a governed output target in this model, so a definition cannot write to it.");
        }

        return null;
    }

    private async Task<int> CurrentVersionNumberAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) { await connection.OpenAsync(cancellationToken); }

        await using var command = new NpgsqlCommand(
            "SELECT current_version FROM ppiq_meta.definition_store WHERE id = @id;", connection);
        command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = definitionId });

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null || scalar is DBNull ? 0 : Convert.ToInt32(scalar);
    }

    private readonly record struct SafeSqlVerdict(bool IsValid, string ErrorCode, string Message, string NormalizedSql);

    private async Task<SafeSqlVerdict> ResolveSafeSqlAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) { await connection.OpenAsync(cancellationToken); }

        await using var command = new NpgsqlCommand(
            "SELECT is_valid, error_code, message, normalized_sql FROM public.ppiq_resolve_safe_sql(@sql, 100, 3000);",
            connection);
        command.Parameters.Add(new NpgsqlParameter("sql", NpgsqlDbType.Text) { Value = sql });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new SafeSqlVerdict(false, "Unknown", "The validator returned no verdict.", sql);
        }

        var isValid = !reader.IsDBNull(0) && reader.GetBoolean(0);
        var errorCode = reader.IsDBNull(1) ? "Unknown" : reader.GetString(1);
        var message = reader.IsDBNull(2) ? "The validator gave no message." : reader.GetString(2);
        var normalized = reader.IsDBNull(3) ? sql : reader.GetString(3);

        return new SafeSqlVerdict(isValid, errorCode, message, normalized);
    }
}
