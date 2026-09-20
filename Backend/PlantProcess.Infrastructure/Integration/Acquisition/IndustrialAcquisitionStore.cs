// Industrial acquisition store: governance, stable fields and layout revisions.
//
// Every write goes through the governed database function that owns its rule, so
// the database refuses what the contract refuses even for a writer that bypasses
// this class. Every read carries the tenant predicate. The store never opens a
// transaction of its own; mutating callers own the unit of work.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Application.Security.Tenancy;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Integration.Acquisition;

public sealed class IndustrialAcquisitionStore
{
    private const string GovernanceColumns =
        "id, tenant_id, source_dataset_definition_id, connection_profile_id, provider_type, governed_at_utc";

    private const string FieldColumns =
        "r.field_id, r.revision, (r.revision = i.current_revision) AS is_current, r.provider_type, r.locator_kind, " +
        "r.source_locator::text, r.field_key, r.display_name, r.declared_type, r.type_shape::text, r.source_unit, " +
        "r.roles, r.layout_revision, r.change_kind, r.semantic_hash, r.created_at_utc";

    private readonly PlantProcessDbContext _db;

    public IndustrialAcquisitionStore(PlantProcessDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    // ------------------------------------------------------------- governance

    public async Task<AcquisitionOutcome<DatasetGovernanceView>> GovernAsync(
        Guid tenantId,
        Guid sourceDatasetDefinitionId,
        Guid? actor,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || sourceDatasetDefinitionId == Guid.Empty)
        {
            return AcquisitionOutcome<DatasetGovernanceView>.Refuse(
                AcquisitionCodes.DatasetNotFound, "A tenant and a dataset are required.");
        }

        var connection = await OpenAsync(cancellationToken);
        await using var command = Command(
            "SELECT ppiq_meta.govern_source_dataset(@tenant_id, @dataset_id, @actor);", connection);
        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "dataset_id", NpgsqlDbType.Uuid, sourceDatasetDefinitionId);
        Add(command, "actor", NpgsqlDbType.Uuid, actor);

        try
        {
            await command.ExecuteScalarAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            return AcquisitionOutcome<DatasetGovernanceView>.Refuse(AcquisitionCodes.CodeOf(ex.MessageText), ex.MessageText);
        }

        return await ResolveGovernanceAsync(tenantId, sourceDatasetDefinitionId, cancellationToken);
    }

    /// <summary>
    /// The governance of a dataset for this tenant. A dataset governed by another
    /// tenant answers exactly like an ungoverned one: a read never discloses it.
    /// </summary>
    public async Task<AcquisitionOutcome<DatasetGovernanceView>> ResolveGovernanceAsync(
        Guid tenantId,
        Guid sourceDatasetDefinitionId,
        CancellationToken cancellationToken)
    {
        var sql = TenantSql.AssertScoped(
            "SELECT " + GovernanceColumns + " FROM ppiq_meta.source_dataset_governance" +
            " WHERE tenant_id = @tenant_id AND source_dataset_definition_id = @dataset_id;");

        var connection = await OpenAsync(cancellationToken);
        await using var command = Command(sql, connection);
        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "dataset_id", NpgsqlDbType.Uuid, sourceDatasetDefinitionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return AcquisitionOutcome<DatasetGovernanceView>.Refuse(
                AcquisitionCodes.DatasetNotGoverned,
                "Dataset " + sourceDatasetDefinitionId.ToString("D") + " is not governed by this tenant.");
        }

        return AcquisitionOutcome<DatasetGovernanceView>.Accept(new DatasetGovernanceView(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            reader.GetGuid(3),
            reader.GetString(4),
            DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc)));
    }

    // ----------------------------------------------------------------- fields

    public async Task<AcquisitionOutcome<IReadOnlyList<FieldDeclarationResult>>> DeclareFieldsAsync(
        Guid tenantId,
        DatasetGovernanceView governance,
        IReadOnlyList<NormalizedFieldDeclaration> fields,
        Guid? actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(governance);
        ArgumentNullException.ThrowIfNull(fields);
        RequireTransaction();

        var connection = await OpenAsync(cancellationToken);
        var results = new List<FieldDeclarationResult>();
        foreach (var field in fields)
        {
            await using var command = Command(
                "SELECT out_field_id, out_revision, out_change_kind, out_created " +
                "FROM ppiq_meta.declare_source_field_revision(" +
                "@tenant_id, @governance_id, @field_id, @locator_kind, @source_locator, @locator_identity, " +
                "@field_key, @display_name, @declared_type, @type_shape, @source_unit, @roles, " +
                "@layout_revision, @reconcile, @semantic_hash, @actor);",
                connection);

            Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
            Add(command, "governance_id", NpgsqlDbType.Uuid, governance.GovernanceId);
            Add(command, "field_id", NpgsqlDbType.Uuid, field.FieldId);
            Add(command, "locator_kind", NpgsqlDbType.Text, field.Locator.Kind);
            Add(command, "source_locator", NpgsqlDbType.Jsonb, field.Locator.CanonicalJson);
            Add(command, "locator_identity", NpgsqlDbType.Text, field.Locator.Identity);
            Add(command, "field_key", NpgsqlDbType.Text, field.FieldKey);
            Add(command, "display_name", NpgsqlDbType.Text, field.DisplayName);
            Add(command, "declared_type", NpgsqlDbType.Text, field.DeclaredType);
            Add(command, "type_shape", NpgsqlDbType.Jsonb, field.TypeShapeJson);
            Add(command, "source_unit", NpgsqlDbType.Text, field.SourceUnit);
            Add(command, "roles", NpgsqlDbType.Array | NpgsqlDbType.Text, field.Roles.ToArray());
            Add(command, "layout_revision", NpgsqlDbType.Integer, field.LayoutRevision);
            Add(command, "reconcile", NpgsqlDbType.Text, field.Reconcile);
            Add(command, "semantic_hash", NpgsqlDbType.Text, field.SemanticHash);
            Add(command, "actor", NpgsqlDbType.Uuid, actor);

            try
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException("The field declaration returned no identity.");
                }

                results.Add(new FieldDeclarationResult(
                    reader.GetGuid(0), field.FieldKey, reader.GetInt32(1), reader.GetString(2), reader.GetBoolean(3)));
            }
            catch (PostgresException ex) when (ex.SqlState == "P0001")
            {
                return AcquisitionOutcome<IReadOnlyList<FieldDeclarationResult>>.Refuse(
                    AcquisitionCodes.CodeOf(ex.MessageText), field.FieldKey + ": " + ex.MessageText);
            }
        }

        return AcquisitionOutcome<IReadOnlyList<FieldDeclarationResult>>.Accept(results);
    }

    public async Task<IReadOnlyList<FieldRevisionView>> ListFieldsAsync(
        Guid tenantId,
        Guid governanceId,
        bool includeHistory,
        CancellationToken cancellationToken)
    {
        var sql = TenantSql.AssertScoped(
            "SELECT " + FieldColumns +
            "  FROM ppiq_meta.source_field_identities i" +
            "  JOIN ppiq_meta.source_field_revisions r ON r.field_id = i.field_id AND r.tenant_id = i.tenant_id" +
            " WHERE i.tenant_id = @tenant_id" +
            "   AND i.dataset_governance_id = @governance_id" +
            (includeHistory ? string.Empty : "   AND r.revision = i.current_revision") +
            " ORDER BY r.field_key, r.field_id, r.revision;");

        var connection = await OpenAsync(cancellationToken);
        await using var command = Command(sql, connection);
        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "governance_id", NpgsqlDbType.Uuid, governanceId);

        var rows = new List<FieldRevisionView>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new FieldRevisionView(
                reader.GetGuid(0),
                reader.GetInt32(1),
                reader.GetBoolean(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetFieldValue<string[]>(11),
                reader.IsDBNull(12) ? null : reader.GetInt32(12),
                reader.GetString(13),
                reader.GetString(14),
                DateTime.SpecifyKind(reader.GetDateTime(15), DateTimeKind.Utc)));
        }

        return rows;
    }

    public async Task<IReadOnlyDictionary<Guid, AcquisitionFieldFact>> LoadFieldFactsAsync(
        Guid tenantId,
        Guid governanceId,
        CancellationToken cancellationToken)
    {
        var current = await ListFieldsAsync(tenantId, governanceId, false, cancellationToken);
        return current.ToDictionary(
            f => f.FieldId,
            f => new AcquisitionFieldFact(f.FieldId, f.Revision, f.DeclaredType, f.LocatorKind));
    }

    // ---------------------------------------------------------------- layouts

    public async Task<AcquisitionOutcome<LayoutDeclarationResult>> DeclareLayoutAsync(
        Guid tenantId,
        DatasetGovernanceView governance,
        NormalizedLayout layout,
        Guid? actor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(governance);
        ArgumentNullException.ThrowIfNull(layout);
        RequireTransaction();

        var connection = await OpenAsync(cancellationToken);
        await using var command = Command(
            "SELECT out_revision, out_created FROM ppiq_meta.declare_source_layout_revision(" +
            "@tenant_id, @governance_id, @layout_kind, @region_bytes, @layout_document, @semantic_hash, @actor);",
            connection);
        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "governance_id", NpgsqlDbType.Uuid, governance.GovernanceId);
        Add(command, "layout_kind", NpgsqlDbType.Text, layout.LayoutKind);
        Add(command, "region_bytes", NpgsqlDbType.Integer, layout.RegionBytes);
        Add(command, "layout_document", NpgsqlDbType.Jsonb, layout.DocumentJson);
        Add(command, "semantic_hash", NpgsqlDbType.Text, layout.SemanticHash);
        Add(command, "actor", NpgsqlDbType.Uuid, actor);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("The layout declaration returned no revision.");
            }

            return AcquisitionOutcome<LayoutDeclarationResult>.Accept(
                new LayoutDeclarationResult(reader.GetInt32(0), reader.GetBoolean(1), layout.SemanticHash));
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001")
        {
            return AcquisitionOutcome<LayoutDeclarationResult>.Refuse(AcquisitionCodes.CodeOf(ex.MessageText), ex.MessageText);
        }
    }

    public async Task<IReadOnlyList<LayoutRevisionView>> ListLayoutsAsync(
        Guid tenantId,
        Guid governanceId,
        int? revision,
        CancellationToken cancellationToken)
    {
        var sql = TenantSql.AssertScoped(
            "SELECT revision, layout_kind, region_bytes, layout_document::text, semantic_hash, created_at_utc" +
            "  FROM ppiq_meta.source_layout_revisions" +
            " WHERE tenant_id = @tenant_id AND dataset_governance_id = @governance_id" +
            (revision.HasValue ? " AND revision = @revision" : string.Empty) +
            " ORDER BY revision;");

        var connection = await OpenAsync(cancellationToken);
        await using var command = Command(sql, connection);
        Add(command, "tenant_id", NpgsqlDbType.Uuid, tenantId);
        Add(command, "governance_id", NpgsqlDbType.Uuid, governanceId);
        if (revision.HasValue)
        {
            Add(command, "revision", NpgsqlDbType.Integer, revision.Value);
        }

        var rows = new List<LayoutRevisionView>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new LayoutRevisionView(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetString(3),
                reader.GetString(4),
                DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc)));
        }

        return rows;
    }

    public async Task<IReadOnlyDictionary<int, AcquisitionLayoutFact>> LoadLayoutFactsAsync(
        Guid tenantId,
        Guid governanceId,
        CancellationToken cancellationToken)
    {
        var layouts = await ListLayoutsAsync(tenantId, governanceId, null, cancellationToken);
        var facts = new Dictionary<int, AcquisitionLayoutFact>();
        foreach (var layout in layouts)
        {
            using var document = JsonDocument.Parse(layout.DocumentJson);
            var ids = new HashSet<Guid>();
            foreach (var member in document.RootElement.GetProperty("members").EnumerateArray())
            {
                ids.Add(Guid.Parse(member.GetProperty("fieldId").GetString()!));
            }

            facts[layout.Revision] = new AcquisitionLayoutFact(layout.Revision, ids);
        }

        return facts;
    }

    // --------------------------------------------------------------- plumbing

    private NpgsqlTransaction? AmbientTransaction() =>
        _db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

    private void RequireTransaction()
    {
        if (AmbientTransaction() is null)
        {
            throw new InvalidOperationException(
                "An acquisition declaration requires the caller's transaction, so a multi-field declaration commits whole or not at all.");
        }
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

    private NpgsqlCommand Command(string sql, NpgsqlConnection connection)
    {
        var command = new NpgsqlCommand(sql, connection);
        var transaction = AmbientTransaction();
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        return command;
    }

    private static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object? value)
    {
        command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });
    }
}
