using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Application.Jobs.Execution.Transformations;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Jobs.Transformations;

/// <summary>
/// THE ONE COMMISSIONED CANONICAL TARGET FOR GOVERNED TRANSFORMATION EXECUTION.
///
/// MaterialUnit is commissioned because it already holds an enforceable source identity:
/// a filtered unique key over its provenance pair. Every other projection target stays
/// refused until its own identity and idempotency contract is converged by its owner.
///
/// Rows are built through the entity's own constructor and production-window method, so
/// the domain's invariants apply exactly as they do on every other write path. This is not
/// a metadata-driven writer: only the fields listed below have a sanctioned path.
///
/// Semantics, all decided before anything is written:
///   new source identity                         inserted, lineage recorded
///   same identity, same effect, same lineage    unchanged, nothing recorded again
///   same identity, same effect, new lineage     reattributed, row untouched
///   same identity, different effect, Ordinary   typed conflict, nothing written
///   same identity, different effect, Reproject  superseded, same canonical Id
///   a newer generation already current         stale: never reattributed, and a
///                                               reprojection is refused
///   business key held by another identity       typed conflict, nothing written
///
/// ONE UNIT. The canonical rows and their lineage evaluations commit together in the
/// writer's own transaction, or inside a savepoint of the caller's transaction when one
/// exists. Identity rows and their current evaluations are locked before any decision is
/// made, and the database's unique keys decide any race the locks could not see.
///
/// PROVENANCE IS NEVER TAKEN FROM THE REQUEST. The exact version and hash are verified
/// against the definition authority, the tenant is the definition's own tenant, and the
/// tenant scope bound on the connection must be that tenant before anything is written.
/// </summary>
public sealed class MaterialUnitTransformationWriter : ITransformationCanonicalWriter
{
    private static readonly string TargetName = nameof(MaterialUnit);

    private const string SavepointName = "canonical_projection_write";
    private const string CurrentEffectIndex = "ux_canonical_projection_effects_current";

    private static readonly HashSet<string> Writable = new(StringComparer.Ordinal)
    {
        nameof(MaterialUnit.MaterialCode),
        nameof(MaterialUnit.MaterialUnitType),
        nameof(MaterialUnit.SiteId),
        nameof(MaterialUnit.ProductFamily),
        nameof(MaterialUnit.GradeOrRecipe),
        nameof(MaterialUnit.ProductionStartUtc),
        nameof(MaterialUnit.ProductionEndUtc),
        nameof(MaterialUnit.PlantTimeZoneId),
        nameof(MaterialUnit.PlantUtcOffsetMinutes),
    };

    private readonly PlantProcessDbContext _db;

    public MaterialUnitTransformationWriter(PlantProcessDbContext db)
    {
        _db = db;
    }

    public bool IsCommissioned(string targetEntity) =>
        string.Equals(targetEntity, TargetName, StringComparison.Ordinal);

    public string? FirstUnwritableField(string targetEntity, IReadOnlyCollection<string> boundFields)
    {
        if (!IsCommissioned(targetEntity))
        {
            return boundFields.OrderBy(f => f, StringComparer.Ordinal).FirstOrDefault() ?? targetEntity;
        }

        foreach (string field in boundFields.OrderBy(f => f, StringComparer.Ordinal))
        {
            if (!Writable.Contains(field)) { return field; }
        }

        return null;
    }

    public async Task<long> ReserveProjectionGenerationAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT nextval('ppiq_plant.canonical_projection_generation_seq')", connection, Ambient());
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        long generation = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        if (generation <= 0)
        {
            throw new InvalidOperationException("The projection generation authority returned no positive generation.");
        }

        return generation;
    }

    public async Task<ApplicationResult<CanonicalWriteResult>> WriteAsync(
        CanonicalWriteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsCommissioned(request.TargetEntity))
        {
            return Fail(JobExecutionDiagnosticCodes.CanonicalTargetNotCommissioned,
                request.TargetEntity + " has no commissioned write path.");
        }

        if (request.TargetDefinitionId == Guid.Empty
            || request.ResolvedVersion <= 0
            || string.IsNullOrWhiteSpace(request.DefinitionHash)
            || request.JobRunHistoryId == Guid.Empty
            || request.ProjectionGeneration <= 0)
        {
            return Fail(JobExecutionDiagnosticCodes.ExactVersionRequired,
                "A canonical write needs the exact definition, version, immutable hash, run and projection "
                + "generation that produce it, and this request does not carry all of them. Nothing was written.");
        }

        if (request.Rows.Count == 0)
        {
            return ApplicationResult<CanonicalWriteResult>.Success(new CanonicalWriteResult(0, 0));
        }

        var candidates = new List<Candidate>(request.Rows.Count);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (CanonicalWriteRow row in request.Rows)
        {
            if (!identities.Add(row.SourceSystem + "\u001f" + row.SourceRecordId))
            {
                return Fail(JobExecutionDiagnosticCodes.SourceIdentityAmbiguous,
                    "Source identity " + row.SourceSystem + " / " + row.SourceRecordId
                    + " appears more than once in one write. Nothing was written.");
            }

            try
            {
                MaterialUnit unit = Build(row);
                candidates.Add(new Candidate(row, unit, EffectHashOf(unit)));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                          or FormatException or InvalidCastException or OverflowException)
            {
                return Fail(JobExecutionDiagnosticCodes.CanonicalWriteFailed,
                    "Source identity " + row.SourceSystem + " / " + row.SourceRecordId
                    + " cannot become a canonical row: " + ex.Message + " Nothing was written.");
            }
        }

        IDbContextTransaction? ambient = _db.Database.CurrentTransaction;
        IDbContextTransaction? owned = null;
        var touched = new List<MaterialUnit>();
        bool completed = false;

        try
        {
            if (ambient is null)
            {
                owned = await _db.Database.BeginTransactionAsync(cancellationToken);
            } else {
                await ambient.CreateSavepointAsync(SavepointName, cancellationToken);
            }

            ApplicationResult<CanonicalWriteResult> outcome =
                await WriteInUnitAsync(request, candidates, touched, cancellationToken);

            if (outcome.IsFailure)
            {
                await UndoAsync(owned, ambient, touched);
                completed = true;
                return outcome;
            }

            if (owned is not null)
            {
                await owned.CommitAsync(cancellationToken);
            } else {
                await ambient!.ReleaseSavepointAsync(SavepointName, cancellationToken);
            }

            completed = true;
            Detach(touched);
            return outcome;
        }
        catch (Exception ex)
        {
            if (!completed)
            {
                await UndoAsync(owned, ambient, touched);
                completed = true;
            }

            if (ex is OperationCanceledException) { throw; }

            PostgresException? postgres = FindPostgres(ex);
            if (ex is DbUpdateConcurrencyException)
            {
                return Fail(JobExecutionDiagnosticCodes.ProjectionEffectStale,
                    "A canonical row changed underneath this run after it was locked for evaluation. Nothing was written; "
                    + "a rerun evaluates against the effect that is now current.");
            }

            if (postgres is not null && postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                if (string.Equals(postgres.ConstraintName, CurrentEffectIndex, StringComparison.Ordinal))
                {
                    return Fail(JobExecutionDiagnosticCodes.CanonicalIdentityConflict,
                        "A source identity in this write already carries a current canonical evaluation this run is not "
                        + "permitted to see or replace. Nothing was written.");
                }

                return Fail(JobExecutionDiagnosticCodes.ProjectionEffectStale,
                    "A concurrent run committed a canonical effect for the same identity or business key first ("
                    + postgres.ConstraintName + "). Nothing was written; a rerun is idempotent.");
            }

            return Fail(JobExecutionDiagnosticCodes.CanonicalWriteFailed,
                "The canonical write was refused by the database: " + ex.GetBaseException().Message + " Nothing was written.");
        }
        finally
        {
            if (owned is not null)
            {
                await owned.DisposeAsync();
            }
        }
    }

    // ------------------------------------------------------------------ the unit --

    private async Task<ApplicationResult<CanonicalWriteResult>> WriteInUnitAsync(
        CanonicalWriteRequest request,
        IReadOnlyList<Candidate> candidates,
        List<MaterialUnit> touched,
        CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await OpenAsync(cancellationToken);

        // 1. The definition authority, not the request, names the version, hash and tenant.
        Guid? tenant;
        await using (var authority = new NpgsqlCommand(
            "SELECT d.tenant_id FROM ppiq_meta.definition_store d JOIN ppiq_meta.definition_versions v "
            + "ON v.definition_id = d.id AND v.tenant_id = d.tenant_id "
            + "WHERE d.id = $1 AND v.version_number = $2 AND v.definition_hash = $3 "
            + "AND d.definition_kind = 'transformation' AND NOT d.is_deleted AND NOT v.is_deleted",
            connection, Ambient()))
        {
            authority.Parameters.Add(new NpgsqlParameter { Value = request.TargetDefinitionId, NpgsqlDbType = NpgsqlDbType.Uuid });
            authority.Parameters.Add(new NpgsqlParameter { Value = request.ResolvedVersion, NpgsqlDbType = NpgsqlDbType.Integer });
            authority.Parameters.Add(new NpgsqlParameter { Value = request.DefinitionHash, NpgsqlDbType = NpgsqlDbType.Varchar });
            tenant = await authority.ExecuteScalarAsync(cancellationToken) as Guid?;
        }

        if (!tenant.HasValue || tenant.Value == Guid.Empty)
        {
            return Fail(JobExecutionDiagnosticCodes.ExactVersionRequired,
                "Transformation " + request.TargetDefinitionId + " version " + request.ResolvedVersion
                + " with hash " + request.DefinitionHash + " is not a stored immutable version. Nothing was written.");
        }

        // 2. The connection must already be scoped to that tenant. The writer never binds it.
        string bound;
        await using (var scope = new NpgsqlCommand(
            "SELECT coalesce(current_setting('app.current_tenant', true), '')", connection, Ambient()))
        {
            bound = Convert.ToString(await scope.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) ?? string.Empty;
        }

        if (!string.Equals(bound, tenant.Value.ToString("D"), StringComparison.OrdinalIgnoreCase))
        {
            return Fail(JobExecutionDiagnosticCodes.CanonicalWriteFailed,
                "The connection is not scoped to the tenant that owns this Transformation, so no lineage could be "
                + "recorded under it. Nothing was written.");
        }

        foreach (Candidate c in candidates)
        {
            if (c.Row.Lineage?.TenantId is Guid lineageTenant && lineageTenant != tenant.Value)
            {
                return Fail(JobExecutionDiagnosticCodes.SourceIdentityInvalid,
                    "Source identity " + c.Row.SourceSystem + " / " + c.Row.SourceRecordId
                    + " was accepted under another tenant. Nothing was written.");
            }
        }

        string[] systems = candidates.Select(c => c.Row.SourceSystem).Distinct(StringComparer.Ordinal).ToArray();
        string[] records = candidates.Select(c => c.Row.SourceRecordId).Distinct(StringComparer.Ordinal).ToArray();

        // 3. Lock the governed identities before any decision.
        await using (var lockRows = new NpgsqlCommand(
            "SELECT id FROM ppiq_plant.material_units WHERE source_system = ANY($1) AND source_record_id = ANY($2) "
            + "ORDER BY id FOR UPDATE",
            connection, Ambient()))
        {
            lockRows.Parameters.Add(new NpgsqlParameter { Value = systems });
            lockRows.Parameters.Add(new NpgsqlParameter { Value = records });
            await using var reader = await lockRows.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) { }
        }

        // 4. Current values, freshly loaded. A stale tracked copy would carry an old
        //    concurrency token and turn a lawful update into a false refusal.
        foreach (var entry in _db.ChangeTracker.Entries<MaterialUnit>().ToList())
        {
            if (entry.Entity.SourceSystem is not null && systems.Contains(entry.Entity.SourceSystem, StringComparer.Ordinal))
            {
                entry.State = EntityState.Detached;
            }
        }

        List<MaterialUnit> byIdentity = await _db.MaterialUnits
            .IgnoreQueryFilters()
            .Where(x => x.SourceSystem != null && x.SourceRecordId != null
                        && systems.Contains(x.SourceSystem) && records.Contains(x.SourceRecordId))
            .ToListAsync(cancellationToken);
        touched.AddRange(byIdentity);

        Dictionary<Guid, CurrentEffect> current = await CurrentEffectsAsync(
            connection, tenant.Value, systems, records, cancellationToken);

        var sites = candidates.Select(c => c.Unit.SiteId).Distinct().ToList();
        var codes = candidates.Select(c => c.Unit.MaterialCode).Distinct(StringComparer.Ordinal).ToList();

        List<MaterialUnit> byBusinessKey = await _db.MaterialUnits
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => sites.Contains(x.SiteId) && codes.Contains(x.MaterialCode))
            .ToListAsync(cancellationToken);

        // 5. Decide every row before writing anything.
        var inserts = new List<MaterialUnit>();
        var evaluations = new List<Evaluation>();
        var claimedKeys = new HashSet<string>(StringComparer.Ordinal);
        int unchanged = 0;
        int superseded = 0;
        int reattributed = 0;

        foreach (Candidate c in candidates)
        {
            MaterialUnit? existing = byIdentity.FirstOrDefault(x =>
                string.Equals(x.SourceSystem, c.Row.SourceSystem, StringComparison.Ordinal)
                && string.Equals(x.SourceRecordId, c.Row.SourceRecordId, StringComparison.Ordinal));

            string newKey = BusinessKey(c.Unit);

            if (existing is null)
            {
                bool heldElsewhere = byBusinessKey.Any(x => x.SiteId == c.Unit.SiteId
                    && string.Equals(x.MaterialCode, c.Unit.MaterialCode, StringComparison.Ordinal));

                if (heldElsewhere || !claimedKeys.Add(newKey))
                {
                    return BusinessKeyConflict(c.Row);
                }

                inserts.Add(c.Unit);
                evaluations.Add(new Evaluation(c, c.Unit.Id, "Inserted", null));
                continue;
            }

            if (existing.IsDeleted)
            {
                return Fail(JobExecutionDiagnosticCodes.CanonicalIdentityConflict,
                    "Source identity " + c.Row.SourceSystem + " / " + c.Row.SourceRecordId
                    + " belongs to canonical row " + existing.Id + ", which has been removed. Nothing was written.");
            }

            CurrentEffect? cur;
            current.TryGetValue(existing.Id, out cur);
            bool newerIsCurrent = cur is not null && cur.Generation > request.ProjectionGeneration;

            if (SameEffect(existing, c.Unit))
            {
                claimedKeys.Add(BusinessKey(existing));

                if (newerIsCurrent || (cur is not null && SameLineage(cur, request, c)))
                {
                    unchanged++;
                    continue;
                }

                reattributed++;
                evaluations.Add(new Evaluation(c, existing.Id, "Reattributed", cur));
                continue;
            }

            if (request.Mode != CanonicalProjectionMode.Reproject)
            {
                return Fail(JobExecutionDiagnosticCodes.CanonicalIdentityConflict,
                    "Source identity " + c.Row.SourceSystem + " / " + c.Row.SourceRecordId
                    + " already holds canonical row " + existing.Id + " with a different effect. "
                    + "Only a run admitted with target parameter " + CanonicalProjectionParameters.ProjectionKey + "="
                    + CanonicalProjectionParameters.ReprojectValue + " may replace it, so nothing was written.");
            }

            if (newerIsCurrent)
            {
                return Fail(JobExecutionDiagnosticCodes.ProjectionEffectStale,
                    "Source identity " + c.Row.SourceSystem + " / " + c.Row.SourceRecordId
                    + " holds an effect from projection generation " + cur!.Generation.ToString(CultureInfo.InvariantCulture)
                    + ", newer than this run's generation " + request.ProjectionGeneration.ToString(CultureInfo.InvariantCulture)
                    + ". A delayed run never overwrites a later one. Nothing was written.");
            }

            bool keyHeldElsewhere = byBusinessKey.Any(x => x.Id != existing.Id
                && x.SiteId == c.Unit.SiteId
                && string.Equals(x.MaterialCode, c.Unit.MaterialCode, StringComparison.Ordinal));

            if (keyHeldElsewhere || !claimedKeys.Add(newKey))
            {
                return BusinessKeyConflict(c.Row);
            }

            existing.ApplyProjectedEffect(c.Unit);
            superseded++;
            evaluations.Add(new Evaluation(c, existing.Id, "Superseded", cur));
        }

        // 6. Canonical rows, then their lineage, in the same unit.
        if (inserts.Count > 0)
        {
            _db.MaterialUnits.AddRange(inserts);
            touched.AddRange(inserts);
        }

        await _db.SaveChangesAsync(cancellationToken);

        foreach (Evaluation e in evaluations)
        {
            if (e.Replaces is not null)
            {
                await using var retire = new NpgsqlCommand(
                    "UPDATE ppiq_plant.canonical_projection_effects SET is_current = false "
                    + "WHERE effect_id = $1 AND tenant_id = $2 AND is_current",
                    connection, Ambient());
                retire.Parameters.Add(new NpgsqlParameter { Value = e.Replaces.EffectId, NpgsqlDbType = NpgsqlDbType.Uuid });
                retire.Parameters.Add(new NpgsqlParameter { Value = tenant.Value, NpgsqlDbType = NpgsqlDbType.Uuid });
                int retired = await retire.ExecuteNonQueryAsync(cancellationToken);
                if (retired != 1)
                {
                    throw new DbUpdateConcurrencyException(
                        "The current evaluation " + e.Replaces.EffectId + " was retired by another run.");
                }
            }

            await using var insert = new NpgsqlCommand(
                "INSERT INTO ppiq_plant.canonical_projection_effects (tenant_id, target_entity, material_unit_id, "
                + "source_system, source_record_id, definition_id, definition_version, definition_hash, job_run_history_id, "
                + "projection_generation, projection_mode, effect_kind, effect_hash, source_batch_id, source_receipt_id, "
                + "source_content_hash, source_dataset_governance_id, supersedes_effect_id, is_current) "
                + "VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, true)",
                connection, Ambient());

            CanonicalSourceLineage? lineage = e.Candidate.Row.Lineage;
            insert.Parameters.Add(Uuid(tenant.Value));
            insert.Parameters.Add(Text(TargetName));
            insert.Parameters.Add(Uuid(e.CanonicalRowId));
            insert.Parameters.Add(Text(e.Candidate.Row.SourceSystem));
            insert.Parameters.Add(Text(e.Candidate.Row.SourceRecordId));
            insert.Parameters.Add(Uuid(request.TargetDefinitionId));
            insert.Parameters.Add(new NpgsqlParameter { Value = request.ResolvedVersion, NpgsqlDbType = NpgsqlDbType.Integer });
            insert.Parameters.Add(Text(request.DefinitionHash));
            insert.Parameters.Add(Uuid(request.JobRunHistoryId));
            insert.Parameters.Add(new NpgsqlParameter { Value = request.ProjectionGeneration, NpgsqlDbType = NpgsqlDbType.Bigint });
            insert.Parameters.Add(Text(request.Mode.ToString()));
            insert.Parameters.Add(Text(e.Kind));
            insert.Parameters.Add(Text(e.Candidate.EffectHash));
            insert.Parameters.Add(Uuid(lineage?.BatchId));
            insert.Parameters.Add(Uuid(lineage?.ReceiptId));
            insert.Parameters.Add(Text(lineage?.ContentHash));
            insert.Parameters.Add(Uuid(lineage?.DatasetGovernanceId));
            insert.Parameters.Add(Uuid(e.Replaces?.EffectId));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        return ApplicationResult<CanonicalWriteResult>.Success(
            new CanonicalWriteResult(inserts.Count, unchanged, superseded, reattributed));
    }

    private async Task<Dictionary<Guid, CurrentEffect>> CurrentEffectsAsync(
        NpgsqlConnection connection,
        Guid tenant,
        string[] systems,
        string[] records,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT effect_id, material_unit_id, definition_id, definition_version, definition_hash, effect_hash, "
            + "source_receipt_id, projection_generation FROM ppiq_plant.canonical_projection_effects "
            + "WHERE tenant_id = $1 AND target_entity = $2 AND is_current "
            + "AND source_system = ANY($3) AND source_record_id = ANY($4) FOR UPDATE",
            connection, Ambient());
        command.Parameters.Add(Uuid(tenant));
        command.Parameters.Add(Text(TargetName));
        command.Parameters.Add(new NpgsqlParameter { Value = systems });
        command.Parameters.Add(new NpgsqlParameter { Value = records });

        var found = new Dictionary<Guid, CurrentEffect>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var effect = new CurrentEffect(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5).Trim(),
                reader.IsDBNull(6) ? null : reader.GetGuid(6),
                reader.GetInt64(7));
            found[effect.CanonicalRowId] = effect;
        }

        return found;
    }

    private async Task UndoAsync(IDbContextTransaction? owned, IDbContextTransaction? ambient, List<MaterialUnit> touched)
    {
        try
        {
            if (owned is not null)
            {
                await owned.RollbackAsync(CancellationToken.None);
            } else if (ambient is not null) {
                await ambient.RollbackToSavepointAsync(SavepointName, CancellationToken.None);
            }
        }
        catch (Exception)
        {
            // The connection may already be gone. Nothing was committed by this writer,
            // and the original failure is what the caller is told.
        }

        Detach(touched);
    }

    private void Detach(List<MaterialUnit> touched)
    {
        foreach (MaterialUnit unit in touched)
        {
            var entry = _db.Entry(unit);
            if (entry.State != EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }
        }

        touched.Clear();
    }

    // ------------------------------------------------------------------ helpers --

    private sealed record Candidate(CanonicalWriteRow Row, MaterialUnit Unit, string EffectHash);

    private sealed record CurrentEffect(
        Guid EffectId,
        Guid CanonicalRowId,
        Guid DefinitionId,
        int DefinitionVersion,
        string DefinitionHash,
        string EffectHash,
        Guid? ReceiptId,
        long Generation);

    private sealed record Evaluation(Candidate Candidate, Guid CanonicalRowId, string Kind, CurrentEffect? Replaces);

    private static bool SameLineage(CurrentEffect current, CanonicalWriteRequest request, Candidate candidate) =>
        current.DefinitionId == request.TargetDefinitionId
        && current.DefinitionVersion == request.ResolvedVersion
        && string.Equals(current.DefinitionHash, request.DefinitionHash, StringComparison.Ordinal)
        && string.Equals(current.EffectHash, candidate.EffectHash, StringComparison.Ordinal)
        && current.ReceiptId == candidate.Row.Lineage?.ReceiptId;

    private static string BusinessKey(MaterialUnit unit) =>
        unit.SiteId.ToString("N") + "\u001f" + unit.MaterialCode;

    private static ApplicationResult<CanonicalWriteResult> BusinessKeyConflict(CanonicalWriteRow row) =>
        Fail(JobExecutionDiagnosticCodes.CanonicalIdentityConflict,
            "The canonical business key of source identity " + row.SourceSystem + " / "
            + row.SourceRecordId + " is already held by another identity. Nothing was written.");

    /// <summary>
    /// The digest of the deterministic business effect: every field the projection can
    /// write, length-prefixed so no two different effects can serialise alike. Clock and
    /// audit fields are excluded, so a rollback reproduces the earlier digest exactly.
    /// </summary>
    public static string EffectHashOf(MaterialUnit unit)
    {
        var text = new StringBuilder();
        Part(text, unit.MaterialCode);
        Part(text, unit.MaterialUnitType);
        Part(text, unit.SiteId.ToString("D"));
        Part(text, unit.ProductFamily);
        Part(text, unit.GradeOrRecipe);
        Part(text, unit.ProductionStartUtc?.Ticks.ToString(CultureInfo.InvariantCulture));
        Part(text, unit.ProductionEndUtc?.Ticks.ToString(CultureInfo.InvariantCulture));
        Part(text, unit.PlantTimeZoneId);
        Part(text, unit.PlantUtcOffsetMinutes.ToString(CultureInfo.InvariantCulture));
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString()));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static void Part(StringBuilder text, string? value)
    {
        if (value is null)
        {
            text.Append("n;");
            return;
        }

        text.Append('v').Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value).Append(';');
    }

    private static NpgsqlParameter Uuid(Guid? value) =>
        new() { Value = value.HasValue ? value.Value : DBNull.Value, NpgsqlDbType = NpgsqlDbType.Uuid };

    private static NpgsqlParameter Text(string? value) =>
        new() { Value = value is null ? DBNull.Value : value, NpgsqlDbType = NpgsqlDbType.Varchar };

    private static PostgresException? FindPostgres(Exception ex)
    {
        for (Exception? cursor = ex; cursor is not null; cursor = cursor.InnerException)
        {
            if (cursor is PostgresException postgres) { return postgres; }
        }

        return null;
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await _db.Database.OpenConnectionAsync(cancellationToken);
        }

        return connection;
    }

    private NpgsqlTransaction? Ambient() =>
        _db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

    private static MaterialUnit Build(CanonicalWriteRow row)
    {
        IReadOnlyDictionary<string, object?> f = row.Fields;

        string code = TextOf(f, nameof(MaterialUnit.MaterialCode)) ?? string.Empty;
        string unitType = TextOf(f, nameof(MaterialUnit.MaterialUnitType)) ?? string.Empty;
        Guid site = GuidOf(f, nameof(MaterialUnit.SiteId)) ?? Guid.Empty;
        string? family = TextOf(f, nameof(MaterialUnit.ProductFamily));
        string? recipe = TextOf(f, nameof(MaterialUnit.GradeOrRecipe));
        DateTime? start = DateOf(f, nameof(MaterialUnit.ProductionStartUtc));
        DateTime? end = DateOf(f, nameof(MaterialUnit.ProductionEndUtc));
        string? zone = TextOf(f, nameof(MaterialUnit.PlantTimeZoneId));
        int? offset = IntOf(f, nameof(MaterialUnit.PlantUtcOffsetMinutes));

        if (!start.HasValue && (end.HasValue || zone is not null || offset.HasValue))
        {
            throw new InvalidOperationException(
                "a production end, zone or offset was bound without a production start, and the domain "
                + "applies those values only with a production window.");
        }

        var unit = new MaterialUnit(
            code,
            unitType,
            site,
            family,
            recipe,
            isSynthetic: false,
            sourceSystem: row.SourceSystem,
            sourceRecordId: row.SourceRecordId);

        if (start.HasValue)
        {
            unit.SetProductionWindow(
                start.Value,
                end,
                offset.HasValue ? TimeSpan.FromMinutes(offset.Value) : null,
                zone ?? string.Empty);
        }

        return unit;
    }

    private static bool SameEffect(MaterialUnit a, MaterialUnit b) =>
        string.Equals(a.MaterialCode, b.MaterialCode, StringComparison.Ordinal)
        && string.Equals(a.MaterialUnitType, b.MaterialUnitType, StringComparison.Ordinal)
        && a.SiteId == b.SiteId
        && string.Equals(a.ProductFamily, b.ProductFamily, StringComparison.Ordinal)
        && string.Equals(a.GradeOrRecipe, b.GradeOrRecipe, StringComparison.Ordinal)
        && a.ProductionStartUtc == b.ProductionStartUtc
        && a.ProductionEndUtc == b.ProductionEndUtc
        && string.Equals(a.PlantTimeZoneId, b.PlantTimeZoneId, StringComparison.Ordinal)
        && a.PlantUtcOffsetMinutes == b.PlantUtcOffsetMinutes;

    private static object? ValueOf(IReadOnlyDictionary<string, object?> fields, string name)
    {
        object? value;
        if (!fields.TryGetValue(name, out value)) { return null; }
        return value is DBNull ? null : value;
    }

    private static string? TextOf(IReadOnlyDictionary<string, object?> fields, string name)
    {
        object? value = ValueOf(fields, name);
        if (value is null) { return null; }
        if (value is string s) { return s; }
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static Guid? GuidOf(IReadOnlyDictionary<string, object?> fields, string name)
    {
        object? value = ValueOf(fields, name);
        if (value is null) { return null; }
        if (value is Guid g) { return g; }
        if (value is string s) { return Guid.Parse(s.Trim()); }
        throw new InvalidCastException("a value bound to an identifier field is not an identifier.");
    }

    private static DateTime? DateOf(IReadOnlyDictionary<string, object?> fields, string name)
    {
        object? value = ValueOf(fields, name);
        if (value is null) { return null; }
        if (value is DateTime d) { return d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime(); }
        if (value is DateTimeOffset o) { return o.UtcDateTime; }
        if (value is string s)
        {
            return DateTime.Parse(s, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        throw new InvalidCastException("a value bound to an instant field is not an instant.");
    }

    private static int? IntOf(IReadOnlyDictionary<string, object?> fields, string name)
    {
        object? value = ValueOf(fields, name);
        if (value is null) { return null; }
        if (value is int i) { return i; }
        if (value is string s) { return int.Parse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture); }
        if (value is IConvertible) { return Convert.ToInt32(value, CultureInfo.InvariantCulture); }
        throw new InvalidCastException("a value bound to a whole-number field is not a whole number.");
    }

    private static ApplicationResult<CanonicalWriteResult> Fail(string code, string message) =>
        ApplicationResult<CanonicalWriteResult>.Failure(
            new ApplicationError(code, message, ApplicationErrorType.BusinessRule));
}
