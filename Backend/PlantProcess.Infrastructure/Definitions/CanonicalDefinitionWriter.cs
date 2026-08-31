using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
/// PPIQ T-090. THE ONE IMPLEMENTATION OF THE DEFINITION VERSION LIFECYCLE.
///
/// AMBIENT TRANSACTION, REQUIRED NOT ASSUMED. Every mutation calls
/// RequireTransaction and refuses when the caller has not opened one. The first
/// draft only preferred a transaction, and that was wrong in a way worth
/// recording: under autocommit, PublishAsync could supersede the current
/// published version and then fail to publish its replacement, leaving a
/// definition with no published truth at all. The comment claimed the caller
/// owned the unit of work; nothing enforced it.
///
/// IDENTITY IS RESOLVED BY THE CALLER, NEVER SYNTHESISED HERE. An earlier draft
/// derived tenant ids by hashing a tenant code, quietly creating a second
/// tenant-identity namespace unrelated to ppiq_meta.tenants - the exact hidden
/// dual authority this task exists to remove.
///
/// HASH WHAT WILL BE PERSISTED. The canonical representation is built first,
/// hashed second, stored third, from one object. A hash taken from the incoming
/// request could otherwise describe a payload the detail table never received.
/// </summary>
public sealed class CanonicalDefinitionWriter : ICanonicalDefinitionWriter
{
    private readonly PlantProcessDbContext _db;

    public CanonicalDefinitionWriter(PlantProcessDbContext db)
    {
        _db = db;
    }

    public async Task<ApplicationResult<CanonicalDefinitionVersion>> WriteVersionAsync(
        CanonicalDefinitionWrite write,
        CancellationToken cancellationToken)
    {
        if (!DefinitionKindRegistry.TryResolve(write.Kind, out var contract))
        {
            return Refuse("Definition kind '" + write.Kind + "' is not declared by the canonical store.");
        }

        var invalid = ValidateWrite(write, contract);
        if (invalid is not null)
        {
            return ApplicationResult<CanonicalDefinitionVersion>.Failure(invalid);
        }

        var canonical = Canonicalise(write, contract);

        var connection = await OpenAsync(cancellationToken);
        var transaction = RequireTransaction();

        var parent = await ResolveParentAsync(connection, transaction, write, contract, cancellationToken);
        if (parent.IsFailure)
        {
            return ApplicationResult<CanonicalDefinitionVersion>.Failure(parent.Error!);
        }

        var definitionId = parent.Value;

        // Serialise writers per definition, not globally. Everything from the
        // hash lookup to the current_version update runs under this lock, so two
        // concurrent writers cannot both compute the same next version number.
        await LockParentAsync(connection, transaction, definitionId, cancellationToken);

        var existing = await FindByHashAsync(
            connection, transaction, definitionId, canonical.Hash, cancellationToken);
        if (existing is not null)
        {
            // Identical redeclaration is not a new version. Without this,
            // EnsureSystemTemplatesAsync would fork a version per application
            // start and the immutable history would record boots, not decisions.
            return ApplicationResult<CanonicalDefinitionVersion>.Success(existing);
        }

        var nextNumber = await NextVersionNumberAsync(
            connection, transaction, definitionId, cancellationToken);

        var versionId = await InsertVersionAsync(
            connection, transaction, write, definitionId, nextNumber, canonical, cancellationToken);

        if (contract.DetailTable is not null && canonical.Detail.Count > 0)
        {
            await WriteDetailAsync(
                connection, transaction, contract, versionId, canonical.Detail, cancellationToken);
        }

        if (canonical.Outcomes.Count > 0)
        {
            await WriteOutcomesAsync(
                connection, transaction, versionId, canonical.Outcomes, cancellationToken);
        }

        await SetCurrentVersionAsync(
            connection, transaction, definitionId, nextNumber, cancellationToken);

        return await ResolveExactInternalAsync(
            connection, transaction, definitionId, nextNumber, cancellationToken);
    }

    /// <summary>
    /// Validates the target completely before touching the version that
    /// currently holds publication. A bad target must not cost a definition its
    /// existing runtime truth.
    /// </summary>
    public async Task<ApplicationResult<CanonicalDefinitionVersion>> PublishAsync(
        Guid definitionId,
        int versionNumber,
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);
        var transaction = RequireTransaction();

        await LockParentAsync(connection, transaction, definitionId, cancellationToken);

        var target = await ResolveExactInternalAsync(
            connection, transaction, definitionId, versionNumber, cancellationToken);
        if (target.IsFailure)
        {
            return target;
        }

        if (target.Value!.Status is not (CanonicalVersionStatus.Draft or CanonicalVersionStatus.Validated))
        {
            return Refuse("Version " + versionNumber + " is " + target.Value.Status +
                          " and only a draft or validated version may be published.");
        }

        var incomplete = await ValidateOutcomeCompletenessAsync(
            connection, transaction, target.Value.VersionId, cancellationToken);
        if (incomplete is not null)
        {
            return ApplicationResult<CanonicalDefinitionVersion>.Failure(incomplete);
        }

        await using (var supersede = Command(
            """
            UPDATE ppiq_meta.definition_versions
               SET status = 'superseded', updated_at_utc = now()
             WHERE definition_id = @definition_id
               AND status = 'published'
               AND version_number <> @version_number;
            """, connection, transaction))
        {
            supersede.Parameters.AddWithValue("definition_id", definitionId);
            supersede.Parameters.AddWithValue("version_number", versionNumber);
            await supersede.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var publish = Command(
            """
            UPDATE ppiq_meta.definition_versions
               SET status = 'published', published_at_utc = now(), updated_at_utc = now()
             WHERE definition_id = @definition_id
               AND version_number = @version_number
               AND status IN ('draft', 'validated');
            """, connection, transaction))
        {
            publish.Parameters.AddWithValue("definition_id", definitionId);
            publish.Parameters.AddWithValue("version_number", versionNumber);

            if (await publish.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                return Refuse("Version " + versionNumber + " could not be published.");
            }
        }

        return await ResolveExactInternalAsync(
            connection, transaction, definitionId, versionNumber, cancellationToken);
    }

    public async Task<ApplicationResult<CanonicalDefinitionVersion>> ResolveExactAsync(
        Guid definitionId,
        int versionNumber,
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);
        return await ResolveExactInternalAsync(
            connection, AmbientTransaction(), definitionId, versionNumber, cancellationToken);
    }

    /// <summary>
    /// Reads by STATUS, never by definition_store.current_version, which records
    /// the newest version that exists - a number a draft raises without becoming
    /// truth.
    /// </summary>
    public async Task<ApplicationResult<CanonicalDefinitionVersion>> ResolvePublishedAsync(
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);

        await using var command = Command(
            SelectVersionSql + """
             WHERE v.definition_id = @definition_id
               AND v.status = 'published'
               AND v.is_deleted = false
             ORDER BY v.version_number DESC
             LIMIT 1;
            """, connection, AmbientTransaction());

        command.Parameters.AddWithValue("definition_id", definitionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ApplicationResult<CanonicalDefinitionVersion>.Success(ReadVersion(reader))
            : ApplicationResult<CanonicalDefinitionVersion>.Failure(
                ApplicationError.NotFound("That definition has no published version."));
    }

    /// <summary>
    /// Withdraws publication, keeping every version. Runs under the parent lock
    /// and the caller's transaction like any other mutation, and succeeds when
    /// nothing was published so a boot-time convergence path can call it
    /// unconditionally.
    /// </summary>
    public async Task<ApplicationResult> RetireAsync(
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);
        var transaction = RequireTransaction();

        await LockParentAsync(connection, transaction, definitionId, cancellationToken);

        await using var command = Command(
            """
            UPDATE ppiq_meta.definition_versions
               SET status = 'superseded', updated_at_utc = now()
             WHERE definition_id = @definition_id
               AND status = 'published';
            """, connection, transaction);

        command.Parameters.AddWithValue("definition_id", definitionId);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<Guid?>> FindByCodeAsync(
        Guid tenantId,
        string definitionCode,
        CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken);

        await using var command = Command(
            """
            SELECT id FROM ppiq_meta.definition_store
             WHERE tenant_id = @tenant_id AND definition_code = @definition_code
             LIMIT 1;
            """, connection, AmbientTransaction());

        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("definition_code", definitionCode.Trim());

        var found = await command.ExecuteScalarAsync(cancellationToken);
        return ApplicationResult<Guid?>.Success(found is Guid id ? id : null);
    }

    // ------------------------------------------------------------- validation

    private static ApplicationError? ValidateWrite(
        CanonicalDefinitionWrite write,
        DefinitionKindRegistry.KindContract contract)
    {
        if (write.TenantId == Guid.Empty)
        {
            return ApplicationError.Validation(
                "A resolved tenant identity is required. The canonical store does not synthesise one.");
        }

        if (write.OwnerId == Guid.Empty)
        {
            return ApplicationError.Validation(
                "A resolved owner identity is required. The canonical store does not synthesise one.");
        }

        if (string.IsNullOrWhiteSpace(write.DefinitionCode))
        {
            return ApplicationError.Validation("A definition code is required.");
        }

        if (write.Outcomes is { Count: > 0 } && contract.StorageKind != "transformation")
        {
            return ApplicationError.Validation(
                "Outcome semantics may only be declared on an S1 transformation definition.");
        }

        if (write.Detail is { Count: > 0 })
        {
            if (contract.DetailTable is null)
            {
                return ApplicationError.Validation(
                    "Kind '" + contract.StorageKind + "' is payload-only and declares no detail fields.");
            }

            // Unknown fields REFUSE. Dropping them silently would let the
            // semantic hash describe a payload the detail table never received.
            var unknown = write.Detail.Keys
                .Where(key => !contract.TryField(key, out _))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();

            if (unknown.Length > 0)
            {
                return ApplicationError.Validation(
                    "Detail field(s) not declared by " + contract.DetailTable + ": " +
                    string.Join(", ", unknown) + ".");
            }

            // A field the registry declares as JSON must BE json. Storing
            // unparseable text would only postpone the failure to PostgreSQL
            // and would give the writer two hashing modes - canonicalised for
            // valid JSON, opaque for invalid - which breaks idempotence.
            var malformed = new List<string>();
            foreach (var pair in write.Detail)
            {
                if (!contract.TryField(pair.Key, out var declared) ||
                    declared.Storage != DefinitionKindRegistry.StorageType.Json)
                {
                    continue;
                }

                if (pair.Value is null)
                {
                    continue;
                }

                if (!IsParseableJson(Convert.ToString(pair.Value, CultureInfo.InvariantCulture)))
                {
                    malformed.Add(pair.Key);
                }
            }

            if (malformed.Count > 0)
            {
                return ApplicationError.Validation(
                    "Detail field(s) declared as JSON by " + contract.DetailTable +
                    " did not contain valid JSON: " +
                    string.Join(", ", malformed.OrderBy(x => x, StringComparer.Ordinal)) + ".");
            }
        }

        foreach (var outcome in write.Outcomes ?? Array.Empty<CanonicalOutcomeDeclaration>())
        {
            if (outcome.OrdinalRankMapJson is not null && !IsParseableJson(outcome.OrdinalRankMapJson))
            {
                return ApplicationError.Validation(
                    "Outcome '" + outcome.OutcomeCode + "' declares an ordinal rank map that is not valid JSON.");
            }
        }

        foreach (var outcome in write.Outcomes ?? Array.Empty<CanonicalOutcomeDeclaration>())
        {
            if (string.IsNullOrWhiteSpace(outcome.OutcomeCode))
            {
                return ApplicationError.Validation("Every declared outcome requires an outcome code.");
            }
        }

        return null;
    }

    /// <summary>
    /// SM-06 completeness, checked at publication rather than at write. A
    /// migration legitimately produces an incomplete draft; what it may never do
    /// is let that draft become published runtime semantics, because a published
    /// version is what every downstream leakage gate treats as fact.
    /// </summary>
    private static async Task<ApplicationError?> ValidateOutcomeCompletenessAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            """
            SELECT outcome_code, outcome_type, class_taxonomy_ref, ordinal_rank_map::text,
                   grain_code, detection_position_code, detection_timestamp_field,
                   direction, censoring_policy
              FROM ppiq_meta.outcome_details
             WHERE definition_version_id = @definition_version_id;
            """, connection, transaction);

        command.Parameters.AddWithValue("definition_version_id", versionId);

        var incomplete = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var code = reader.GetString(0);
            var type = reader.GetString(1);
            var required = new List<(int Ordinal, string Field)>
            {
                (4, "grain_code"),
                (5, "detection_position_code"),
                (6, "detection_timestamp_field"),
                (7, "direction"),
                (8, "censoring_policy")
            };

            if (type is "categorical" or "ordinal")
            {
                required.Add((2, "class_taxonomy_ref"));
            }

            if (type == "ordinal")
            {
                required.Add((3, "ordinal_rank_map"));
            }

            foreach (var (ordinal, field) in required)
            {
                var value = reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

                // Exact sentinel, never substring. A customer-authored opaque
                // identifier such as legacy_migrated_unknown_mapping_v2 is a
                // legitimate value; refusing it because the sentinel text
                // appears inside it would be lexical contamination, not a
                // semantic rule.
                if (string.IsNullOrWhiteSpace(value) ||
                    DefinitionKindRegistry.IsUnknownSentinel(value))
                {
                    incomplete.Add(code + "." + field);
                }
            }
        }

        if (incomplete.Count == 0)
        {
            return null;
        }

        return ApplicationError.Validation(
            "This version carries incomplete outcome semantics and cannot be published: " +
            string.Join(", ", incomplete.OrderBy(x => x, StringComparer.Ordinal)) +
            ". Supply the real values and publish a new version.");
    }

    // ---------------------------------------------------------- canonical form

    private sealed record CanonicalForm(
        string ContentJson,
        IReadOnlyDictionary<string, object?> Detail,
        IReadOnlyList<CanonicalOutcomeDeclaration> Outcomes,
        string Hash);

    /// <summary>
    /// One normalised representation, hashed and then persisted. JSON is
    /// reparsed with deterministic property ordering, numbers formatted
    /// invariantly and nulls explicit, so two semantically identical
    /// declarations differing only in whitespace or key order resolve to the
    /// same immutable version.
    /// </summary>
    private static CanonicalForm Canonicalise(
        CanonicalDefinitionWrite write,
        DefinitionKindRegistry.KindContract contract)
    {
        var content = NormaliseJson(write.ContentJson);

        // Normalisation is TYPE-DRIVEN from the registry, never "try to parse
        // every string and fall back". A Text field stays opaque; a Json field
        // is reordered deterministically. Validation has already refused any
        // Json field that could not parse, so this cannot silently degrade.
        var detail = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in write.Detail ?? new Dictionary<string, object?>())
        {
            if (contract.TryField(pair.Key, out var declared) &&
                declared.Storage == DefinitionKindRegistry.StorageType.Json &&
                pair.Value is not null)
            {
                detail[pair.Key] = NormaliseJson(
                    Convert.ToString(pair.Value, CultureInfo.InvariantCulture));
                continue;
            }

            detail[pair.Key] = NormaliseValue(pair.Value);
        }

        var outcomes = (write.Outcomes ?? Array.Empty<CanonicalOutcomeDeclaration>())
            .OrderBy(o => o.OutcomeCode, StringComparer.Ordinal)
            .Select(o => o with
            {
                OrdinalRankMapJson = o.OrdinalRankMapJson is null
                    ? null
                    : NormaliseJson(o.OrdinalRankMapJson)
            })
            .ToList();

        var builder = new StringBuilder();
        builder.Append(contract.StorageKind).Append('\u001f');
        builder.Append(contract.Surface).Append('\u001f');
        builder.Append(write.DefinitionCode.Trim()).Append('\u001f');
        builder.Append(content).Append('\u001f');

        foreach (var pair in detail)
        {
            builder.Append(pair.Key).Append('=')
                   .Append(pair.Value is null ? "\u0000" : pair.Value.ToString())
                   .Append('\u001e');
        }

        foreach (var outcome in outcomes)
        {
            builder.Append(string.Join('\u001d', new[]
            {
                outcome.OutcomeCode,
                outcome.OutcomeType,
                outcome.ClassTaxonomyRef ?? "\u0000",
                outcome.OrdinalRankMapJson ?? "\u0000",
                outcome.GrainCode,
                outcome.DetectionPositionCode,
                outcome.DetectionTimestampField,
                outcome.Direction,
                outcome.UnitCode ?? "\u0000",
                outcome.CensoringPolicy
            })).Append('\u001e');
        }

        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();

        return new CanonicalForm(content, detail, outcomes, hash);
    }

    private static bool IsParseableJson(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return true;
        }

        try
        {
            using var _ = JsonDocument.Parse(candidate);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string NormaliseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
            {
                WriteOrdered(document.RootElement, writer);
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }
        catch (JsonException exception)
        {
            // Unreachable for detail fields: ValidateWrite refuses malformed
            // JSON before this runs. Throwing rather than falling back keeps
            // that guarantee honest - a silent fallback here would restore the
            // two-hashing-modes defect through the back door.
            throw new InvalidOperationException(
                "Canonicalisation received content declared as JSON that does not parse. " +
                "Validation should have refused it before mutation.", exception);
        }
    }

    private static void WriteOrdered(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject()
                             .OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteOrdered(property.Value, writer);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                // Array order is semantically relevant and is preserved.
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteOrdered(item, writer);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.Number:
                writer.WriteRawValue(
                    element.GetDecimal().ToString("0.################", CultureInfo.InvariantCulture));
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static object? NormaliseValue(object? value) => value switch
    {
        null => null,
        string text => text,
        bool flag => flag ? "true" : "false",
        decimal number => number.ToString("0.################", CultureInfo.InvariantCulture),
        double number => number.ToString("0.################", CultureInfo.InvariantCulture),
        float number => number.ToString("0.################", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    // ---------------------------------------------------------------- plumbing

    private const string SelectVersionSql =
        """
        SELECT v.definition_id, v.id, s.definition_code, s.definition_kind, s.surface,
               v.version_number, v.status, COALESCE(v.graph_json::text, '{}'),
               v.definition_hash, v.created_at_utc, v.created_by
          FROM ppiq_meta.definition_versions v
          JOIN ppiq_meta.definition_store s ON s.id = v.definition_id
        """;

    private static ApplicationResult<CanonicalDefinitionVersion> Refuse(string message) =>
        ApplicationResult<CanonicalDefinitionVersion>.Failure(ApplicationError.Validation(message));

    private NpgsqlTransaction? AmbientTransaction() =>
        _db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

    private NpgsqlTransaction RequireTransaction()
    {
        return AmbientTransaction() ?? throw new InvalidOperationException(
            "A canonical definition mutation requires the caller's transaction. Open one " +
            "before writing so the canonical version and any serving projection commit together.");
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private static NpgsqlCommand Command(
        string sql, NpgsqlConnection connection, NpgsqlTransaction? transaction)
    {
        var command = new NpgsqlCommand(sql, connection);
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        return command;
    }

    /// <summary>
    /// Two concurrent first writers may race to create the same parent. A row
    /// lock cannot help before the row exists, so the unique constraint decides
    /// the winner: both insert with ON CONFLICT DO NOTHING, both then read the
    /// surviving row. Kind and surface are identity-level and are compared, not
    /// overwritten - an established widget definition must never quietly become
    /// a model definition because a caller reused its code.
    /// </summary>
    private static async Task<ApplicationResult<Guid>> ResolveParentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CanonicalDefinitionWrite write,
        DefinitionKindRegistry.KindContract contract,
        CancellationToken cancellationToken)
    {
        var code = write.DefinitionCode.Trim();

        await using (var insert = Command(
            """
            INSERT INTO ppiq_meta.definition_store
                (tenant_id, definition_code, surface, definition_kind, name, owner_id, current_version)
            VALUES (@tenant_id, @definition_code, @surface, @definition_kind, @name, @owner_id, 0)
            ON CONFLICT (tenant_id, definition_code) DO NOTHING;
            """, connection, transaction))
        {
            insert.Parameters.AddWithValue("tenant_id", write.TenantId);
            insert.Parameters.AddWithValue("definition_code", code);
            insert.Parameters.AddWithValue("surface", contract.Surface);
            insert.Parameters.AddWithValue("definition_kind", contract.StorageKind);
            insert.Parameters.AddWithValue("owner_id", write.OwnerId);
            insert.Parameters.AddWithValue("name",
                string.IsNullOrWhiteSpace(write.Name) ? code : write.Name.Trim());

            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var read = Command(
            """
            SELECT id, surface, definition_kind FROM ppiq_meta.definition_store
             WHERE tenant_id = @tenant_id AND definition_code = @definition_code
             LIMIT 1;
            """, connection, transaction);

        read.Parameters.AddWithValue("tenant_id", write.TenantId);
        read.Parameters.AddWithValue("definition_code", code);

        await using var reader = await read.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound(
                "Definition '" + code + "' could not be resolved after creation."));
        }

        var id = reader.GetGuid(0);
        var surface = reader.GetString(1);
        var kind = reader.GetString(2);

        if (!string.Equals(surface, contract.Surface, StringComparison.Ordinal) ||
            !string.Equals(kind, contract.StorageKind, StringComparison.Ordinal))
        {
            return ApplicationResult<Guid>.Failure(ApplicationError.Conflict(
                "Definition '" + code + "' is already established as surface " + surface +
                " kind " + kind + " and cannot be rewritten as surface " + contract.Surface +
                " kind " + contract.StorageKind + "."));
        }

        return ApplicationResult<Guid>.Success(id);
    }

    private static async Task LockParentAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            "SELECT id FROM ppiq_meta.definition_store WHERE id = @definition_id FOR UPDATE;",
            connection, transaction);

        command.Parameters.AddWithValue("definition_id", definitionId);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<CanonicalDefinitionVersion?> FindByHashAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        string hash,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            SelectVersionSql + """
             WHERE v.definition_id = @definition_id
               AND v.definition_hash = @definition_hash
               AND v.is_deleted = false
             ORDER BY v.version_number DESC
             LIMIT 1;
            """, connection, transaction);

        command.Parameters.AddWithValue("definition_id", definitionId);
        command.Parameters.AddWithValue("definition_hash", hash);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadVersion(reader) : null;
    }

    private static async Task<int> NextVersionNumberAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            """
            SELECT COALESCE(MAX(version_number), 0) + 1
              FROM ppiq_meta.definition_versions
             WHERE definition_id = @definition_id;
            """, connection, transaction);

        command.Parameters.AddWithValue("definition_id", definitionId);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<Guid> InsertVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CanonicalDefinitionWrite write,
        Guid definitionId,
        int versionNumber,
        CanonicalForm canonical,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            """
            INSERT INTO ppiq_meta.definition_versions
                (tenant_id, definition_id, version_number, status, mode,
                 graph_json, definition_hash, created_by, published_at_utc)
            VALUES (@tenant_id, @definition_id, @version_number, @status, 'block',
                    @graph_json, @definition_hash, @created_by,
                    CASE WHEN @status = 'published' THEN now() ELSE NULL END)
            RETURNING id;
            """, connection, transaction);

        command.Parameters.AddWithValue("tenant_id", write.TenantId);
        command.Parameters.AddWithValue("definition_id", definitionId);
        command.Parameters.AddWithValue("version_number", versionNumber);
        command.Parameters.AddWithValue("status", StatusLiteral(write.Status));
        command.Parameters.AddWithValue("definition_hash", canonical.Hash);
        command.Parameters.AddWithValue("created_by", write.OwnerId);
        command.Parameters.Add(new NpgsqlParameter("graph_json", NpgsqlDbType.Jsonb)
        {
            Value = canonical.ContentJson
        });

        return (Guid)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>
    /// Column names come from the registry contract, already validated, so this
    /// never consults the physical catalogue to decide what may be written. A
    /// separate architecture gate proves registry and schema agree.
    /// </summary>
    private static async Task WriteDetailAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DefinitionKindRegistry.KindContract contract,
        Guid versionId,
        IReadOnlyDictionary<string, object?> detail,
        CancellationToken cancellationToken)
    {
        var detailTable = contract.DetailTable!;
        var chosen = detail.Keys.OrderBy(key => key, StringComparer.Ordinal).ToList();
        var names = string.Join(", ", chosen);
        var values = string.Join(", ", chosen.Select(key => "@" + key));

        await using var insert = Command(
            "INSERT INTO ppiq_meta." + detailTable +
            " (definition_version_id, " + names + ") VALUES (@definition_version_id, " + values + ");",
            connection, transaction);

        insert.Parameters.AddWithValue("definition_version_id", versionId);

        // BOUND BY DECLARED STORAGE, NOT BY C# TYPE. AddWithValue sends every
        // string as text, and PostgreSQL refuses text against a jsonb, uuid or
        // integer column (42804). The registry already states each field's
        // storage; the parameter type follows it. (W1-T090-DETAIL-01)
        foreach (var key in chosen)
        {
            insert.Parameters.Add(TypedDetailParameter(contract, key, detail[key]));
        }

        await insert.ExecuteNonQueryAsync(cancellationToken);
    }

    private static NpgsqlParameter TypedDetailParameter(
        DefinitionKindRegistry.KindContract contract,
        string key,
        object? value)
    {
        // Unknown fields were refused by ValidateWrite before any write began.
        contract.TryField(key, out var declared);

        var type = declared.Storage switch
        {
            DefinitionKindRegistry.StorageType.Json => NpgsqlDbType.Jsonb,
            DefinitionKindRegistry.StorageType.Uuid => NpgsqlDbType.Uuid,
            DefinitionKindRegistry.StorageType.Integer => NpgsqlDbType.Integer,
            DefinitionKindRegistry.StorageType.Boolean => NpgsqlDbType.Boolean,
            _ => NpgsqlDbType.Text,
        };

        object bound = value is null
            ? DBNull.Value
            : declared.Storage switch
            {
                DefinitionKindRegistry.StorageType.Uuid =>
                    value is Guid guid ? guid : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!),
                DefinitionKindRegistry.StorageType.Integer =>
                    value is int number ? number : Convert.ToInt32(value, CultureInfo.InvariantCulture),
                DefinitionKindRegistry.StorageType.Boolean =>
                    value is bool flag ? flag : Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            };

        return new NpgsqlParameter(key, type) { Value = bound };
    }

    private static async Task WriteOutcomesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid versionId,
        IReadOnlyList<CanonicalOutcomeDeclaration> outcomes,
        CancellationToken cancellationToken)
    {
        foreach (var outcome in outcomes)
        {
            await using var command = Command(
                """
                INSERT INTO ppiq_meta.outcome_details
                    (definition_version_id, outcome_code, outcome_type, class_taxonomy_ref,
                     ordinal_rank_map, grain_code, detection_position_code,
                     detection_timestamp_field, direction, unit_code, censoring_policy)
                VALUES (@definition_version_id, @outcome_code, @outcome_type, @class_taxonomy_ref,
                        @ordinal_rank_map, @grain_code, @detection_position_code,
                        @detection_timestamp_field, @direction, @unit_code, @censoring_policy);
                """, connection, transaction);

            command.Parameters.AddWithValue("definition_version_id", versionId);
            command.Parameters.AddWithValue("outcome_code", outcome.OutcomeCode);
            command.Parameters.AddWithValue("outcome_type", outcome.OutcomeType);
            command.Parameters.AddWithValue("grain_code", outcome.GrainCode);
            command.Parameters.AddWithValue("detection_position_code", outcome.DetectionPositionCode);
            command.Parameters.AddWithValue("detection_timestamp_field", outcome.DetectionTimestampField);
            command.Parameters.AddWithValue("direction", outcome.Direction);
            command.Parameters.AddWithValue("censoring_policy", outcome.CensoringPolicy);
            command.Parameters.AddWithValue("class_taxonomy_ref",
                (object?)outcome.ClassTaxonomyRef ?? DBNull.Value);
            command.Parameters.AddWithValue("unit_code",
                (object?)outcome.UnitCode ?? DBNull.Value);
            command.Parameters.Add(new NpgsqlParameter("ordinal_rank_map", NpgsqlDbType.Jsonb)
            {
                Value = (object?)outcome.OrdinalRankMapJson ?? DBNull.Value
            });

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task SetCurrentVersionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        int versionNumber,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            """
            UPDATE ppiq_meta.definition_store
               SET current_version = @version_number, updated_at_utc = now()
             WHERE id = @definition_id;
            """, connection, transaction);

        command.Parameters.AddWithValue("definition_id", definitionId);
        command.Parameters.AddWithValue("version_number", versionNumber);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<ApplicationResult<CanonicalDefinitionVersion>> ResolveExactInternalAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        Guid definitionId,
        int versionNumber,
        CancellationToken cancellationToken)
    {
        await using var command = Command(
            SelectVersionSql + """
             WHERE v.definition_id = @definition_id
               AND v.version_number = @version_number
             LIMIT 1;
            """, connection, transaction);

        command.Parameters.AddWithValue("definition_id", definitionId);
        command.Parameters.AddWithValue("version_number", versionNumber);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ApplicationResult<CanonicalDefinitionVersion>.Success(ReadVersion(reader))
            : ApplicationResult<CanonicalDefinitionVersion>.Failure(ApplicationError.NotFound(
                "Version " + versionNumber + " of that definition was never written."));
    }

    private static CanonicalDefinitionVersion ReadVersion(NpgsqlDataReader reader)
    {
        return new CanonicalDefinitionVersion(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            KindOfStorage(reader.GetString(3)),
            reader.GetString(4),
            reader.GetInt32(5),
            StatusOf(reader.GetString(6)),
            reader.GetString(7),
            reader.GetString(8),
            reader.GetDateTime(9),
            reader.IsDBNull(10) ? null : reader.GetGuid(10));
    }

    private static DefinitionKind KindOfStorage(string storageKind)
    {
        if (DefinitionKindRegistry.TryResolveStorageKind(storageKind, out var contract))
        {
            return contract.Kind;
        }

        throw new InvalidOperationException(
            "The store holds definition kind '" + storageKind +
            "' which the registry does not declare. The registry and script 831 have diverged.");
    }

    private static string StatusLiteral(CanonicalVersionStatus status) => status switch
    {
        CanonicalVersionStatus.Draft => "draft",
        CanonicalVersionStatus.Validated => "validated",
        CanonicalVersionStatus.Published => "published",
        CanonicalVersionStatus.PausedByDrift => "paused_by_drift",
        CanonicalVersionStatus.RolledBack => "rolled_back",
        CanonicalVersionStatus.Superseded => "superseded",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown version status.")
    };

    private static CanonicalVersionStatus StatusOf(string literal) => literal switch
    {
        "draft" => CanonicalVersionStatus.Draft,
        "validated" => CanonicalVersionStatus.Validated,
        "published" => CanonicalVersionStatus.Published,
        "paused_by_drift" => CanonicalVersionStatus.PausedByDrift,
        "rolled_back" => CanonicalVersionStatus.RolledBack,
        "superseded" => CanonicalVersionStatus.Superseded,
        _ => throw new InvalidOperationException(
            "The store holds version status '" + literal + "' which script 831 does not declare.")
    };
}
