// Industrial acquisition configuration service.
//
// The acquisition configuration is a canonical definition (kind
// AcquisitionConfiguration, surface S1). Its versions, semantic hash, idempotent
// redeclaration and publication belong to ICanonicalDefinitionWriter; this service
// validates authored intent, builds the typed detail projection of the same
// content, and owns the unit of work. It never records runtime state: activation
// answers with the exact resolved target and an honest refusal until a commissioned
// continuous-acquisition runtime exists.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Application.Security.Tenancy;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Integration.Acquisition;

public sealed class AcquisitionConfigurationService
{
    public const string DefinitionCodePrefix = "acquisition_configuration.";

    private readonly PlantProcessDbContext _db;
    private readonly ICanonicalDefinitionWriter _writer;
    private readonly IndustrialAcquisitionStore _store;

    public AcquisitionConfigurationService(PlantProcessDbContext db, ICanonicalDefinitionWriter writer)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _store = new IndustrialAcquisitionStore(db);
    }

    public IndustrialAcquisitionStore Store => _store;

    /// <summary>One acquisition configuration definition per governed dataset.</summary>
    public static string DefinitionCodeFor(Guid sourceDatasetDefinitionId) =>
        DefinitionCodePrefix + sourceDatasetDefinitionId.ToString("N");

    // ------------------------------------------------------------ governance

    public Task<AcquisitionOutcome<DatasetGovernanceView>> GovernAsync(
        Guid tenantId, Guid datasetId, Guid? actor, CancellationToken cancellationToken) =>
        InTransactionAsync(() => _store.GovernAsync(tenantId, datasetId, actor, cancellationToken), cancellationToken);

    // ---------------------------------------------------------------- fields

    public async Task<AcquisitionOutcome<IReadOnlyList<FieldDeclarationResult>>> DeclareFieldsAsync(
        Guid tenantId,
        Guid datasetId,
        IReadOnlyList<FieldDeclarationRequest>? fields,
        Guid? actor,
        CancellationToken cancellationToken)
    {
        var governance = await _store.ResolveGovernanceAsync(tenantId, datasetId, cancellationToken);
        if (!governance.IsAccepted)
        {
            return AcquisitionOutcome<IReadOnlyList<FieldDeclarationResult>>.Refuse(governance.Refusal!);
        }

        var normalized = FieldDeclarationKernel.NormalizeAll(governance.Value!.ProviderType, fields);
        if (!normalized.IsAccepted)
        {
            return AcquisitionOutcome<IReadOnlyList<FieldDeclarationResult>>.Refuse(normalized.Refusal!);
        }

        return await InTransactionAsync(
            () => _store.DeclareFieldsAsync(tenantId, governance.Value, normalized.Value!, actor, cancellationToken),
            cancellationToken);
    }

    public async Task<AcquisitionOutcome<IReadOnlyList<FieldRevisionView>>> ListFieldsAsync(
        Guid tenantId, Guid datasetId, bool includeHistory, CancellationToken cancellationToken)
    {
        var governance = await _store.ResolveGovernanceAsync(tenantId, datasetId, cancellationToken);
        if (!governance.IsAccepted)
        {
            return AcquisitionOutcome<IReadOnlyList<FieldRevisionView>>.Refuse(governance.Refusal!);
        }

        return AcquisitionOutcome<IReadOnlyList<FieldRevisionView>>.Accept(
            await _store.ListFieldsAsync(tenantId, governance.Value!.GovernanceId, includeHistory, cancellationToken));
    }

    // --------------------------------------------------------------- layouts

    public async Task<AcquisitionOutcome<LayoutDeclarationResult>> DeclareLayoutAsync(
        Guid tenantId, Guid datasetId, JsonElement? document, Guid? actor, CancellationToken cancellationToken)
    {
        var governance = await _store.ResolveGovernanceAsync(tenantId, datasetId, cancellationToken);
        if (!governance.IsAccepted)
        {
            return AcquisitionOutcome<LayoutDeclarationResult>.Refuse(governance.Refusal!);
        }

        var layout = RawLayoutKernel.Normalize(document);
        if (!layout.IsAccepted)
        {
            return AcquisitionOutcome<LayoutDeclarationResult>.Refuse(layout.Refusal!);
        }

        var fields = await _store.LoadFieldFactsAsync(tenantId, governance.Value!.GovernanceId, cancellationToken);
        foreach (var member in layout.Value!.Members)
        {
            if (!fields.TryGetValue(member.FieldId, out var fact))
            {
                return AcquisitionOutcome<LayoutDeclarationResult>.Refuse(AcquisitionCodes.LayoutInvalid,
                    "member " + member.FieldId.ToString("D") + " names a field this dataset does not govern.");
            }

            if (fact.LocatorKind != SourceLocatorGrammar.RawMember)
            {
                return AcquisitionOutcome<LayoutDeclarationResult>.Refuse(AcquisitionCodes.LayoutInvalid,
                    "member " + member.FieldId.ToString("D") + " is a typed source item and is never decoded as raw bytes.");
            }

            if (!string.Equals(fact.DeclaredType, member.MemberType, StringComparison.Ordinal))
            {
                return AcquisitionOutcome<LayoutDeclarationResult>.Refuse(AcquisitionCodes.LayoutInvalid,
                    "member " + member.FieldId.ToString("D") + " is placed as " + member.MemberType +
                    " but the field declares " + fact.DeclaredType + ".");
            }
        }

        return await InTransactionAsync(
            () => _store.DeclareLayoutAsync(tenantId, governance.Value, layout.Value, actor, cancellationToken),
            cancellationToken);
    }

    public async Task<AcquisitionOutcome<IReadOnlyList<LayoutRevisionView>>> ListLayoutsAsync(
        Guid tenantId, Guid datasetId, int? revision, CancellationToken cancellationToken)
    {
        var governance = await _store.ResolveGovernanceAsync(tenantId, datasetId, cancellationToken);
        if (!governance.IsAccepted)
        {
            return AcquisitionOutcome<IReadOnlyList<LayoutRevisionView>>.Refuse(governance.Refusal!);
        }

        var rows = await _store.ListLayoutsAsync(tenantId, governance.Value!.GovernanceId, revision, cancellationToken);
        if (revision.HasValue && rows.Count == 0)
        {
            return AcquisitionOutcome<IReadOnlyList<LayoutRevisionView>>.Refuse(AcquisitionCodes.LayoutRevisionUnknown,
                "Layout revision " + revision.Value + " does not exist for this dataset.");
        }

        return AcquisitionOutcome<IReadOnlyList<LayoutRevisionView>>.Accept(rows);
    }

    // -------------------------------------------------------- configurations

    public async Task<AcquisitionOutcome<AcquisitionConfigurationVersionView>> CreateVersionAsync(
        Guid tenantId,
        Guid ownerId,
        Guid datasetId,
        JsonElement? content,
        bool publish,
        CancellationToken cancellationToken)
    {
        if (ownerId == Guid.Empty)
        {
            return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Refuse(
                AcquisitionCodes.ConfigurationInvalid, "A resolved owner identity is required.");
        }

        var governance = await _store.ResolveGovernanceAsync(tenantId, datasetId, cancellationToken);
        if (!governance.IsAccepted)
        {
            return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Refuse(governance.Refusal!);
        }

        var normalized = AcquisitionConfigurationKernel.Normalize(
            governance.Value!.GovernanceId, governance.Value.ProviderType, content);
        if (!normalized.IsAccepted)
        {
            return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Refuse(normalized.Refusal!);
        }

        var configuration = normalized.Value!;
        var fields = await _store.LoadFieldFactsAsync(tenantId, governance.Value.GovernanceId, cancellationToken);
        var layouts = await _store.LoadLayoutFactsAsync(tenantId, governance.Value.GovernanceId, cancellationToken);
        var references = AcquisitionConfigurationKernel.ValidateReferences(configuration, fields, layouts);
        if (references is not null)
        {
            return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Refuse(references);
        }

        return await InTransactionAsync(async () =>
        {
            var written = await _writer.WriteVersionAsync(
                new CanonicalDefinitionWrite(
                    DefinitionKind.AcquisitionConfiguration,
                    tenantId,
                    ownerId,
                    DefinitionCodeFor(datasetId),
                    "Acquisition configuration " + datasetId.ToString("D"),
                    configuration.ContentJson,
                    CanonicalVersionStatus.Draft,
                    DetailOf(configuration)),
                cancellationToken);

            if (written.IsFailure)
            {
                return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Refuse(
                    AcquisitionCodes.ConfigurationInvalid, written.Error!.Message);
            }

            var version = written.Value!;
            if (publish)
            {
                // Publication is a semantic act over portable authored capability.
                // Environment certification is activation/runtime evidence and must not
                // make the immutable authored version machine-dependent.
                var operations = EvaluateOperations(configuration);
                var unexecutable = operations.FirstOrDefault(o => !o.DeclaredExecutable);
                if (unexecutable is not null)
                {
                    return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Refuse(
                        AcquisitionCodes.OperationNotExecutable,
                        "Required source operation '" + unexecutable.Operation + "' is not declared executable: " +
                        unexecutable.DeclaredEvidence + ". The version was not published.");
                }
            }

            if (publish && version.Status is CanonicalVersionStatus.Draft or CanonicalVersionStatus.Validated)
            {
                var published = await _writer.PublishAsync(version.DefinitionId, version.VersionNumber, cancellationToken);
                if (published.IsFailure)
                {
                    return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Refuse(
                        AcquisitionCodes.ConfigurationInvalid, published.Error!.Message);
                }

                version = published.Value!;
            }

            return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Accept(ToView(version));
        }, cancellationToken);
    }

    public async Task<AcquisitionOutcome<IReadOnlyList<AcquisitionConfigurationVersionView>>> ListVersionsAsync(
        Guid tenantId, Guid datasetId, CancellationToken cancellationToken)
    {
        var governance = await _store.ResolveGovernanceAsync(tenantId, datasetId, cancellationToken);
        if (!governance.IsAccepted)
        {
            return AcquisitionOutcome<IReadOnlyList<AcquisitionConfigurationVersionView>>.Refuse(governance.Refusal!);
        }

        var sql = TenantSql.AssertScoped(
            "SELECT v.version_number FROM ppiq_meta.definition_versions v" +
            "  JOIN ppiq_meta.definition_store s ON s.id = v.definition_id" +
            " WHERE s.tenant_id = @tenant_id AND v.tenant_id = @tenant_id" +
            "   AND s.definition_code = @code AND s.definition_kind = 'acquisition_configuration'" +
            "   AND v.is_deleted = false" +
            " ORDER BY v.version_number;");

        var numbers = new List<int>();
        var connection = (NpgsqlConnection)_db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand(sql, connection))
        {
            command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = tenantId });
            command.Parameters.Add(new NpgsqlParameter("code", NpgsqlDbType.Text) { Value = DefinitionCodeFor(datasetId) });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                numbers.Add(reader.GetInt32(0));
            }
        }

        var views = new List<AcquisitionConfigurationVersionView>();
        foreach (var number in numbers)
        {
            var one = await ResolveAsync(tenantId, datasetId, number, cancellationToken);
            if (one.IsAccepted)
            {
                views.Add(one.Value!);
            }
        }

        return AcquisitionOutcome<IReadOnlyList<AcquisitionConfigurationVersionView>>.Accept(views);
    }

    public async Task<AcquisitionOutcome<AcquisitionConfigurationVersionView>> ResolveAsync(
        Guid tenantId, Guid datasetId, int version, CancellationToken cancellationToken)
    {
        var governance = await _store.ResolveGovernanceAsync(tenantId, datasetId, cancellationToken);
        if (!governance.IsAccepted)
        {
            return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Refuse(governance.Refusal!);
        }

        var found = await _writer.FindByCodeAsync(tenantId, DefinitionCodeFor(datasetId), cancellationToken);
        if (found.IsFailure || found.Value is null)
        {
            return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Refuse(AcquisitionCodes.VersionNotFound,
                "No acquisition configuration exists for this dataset.");
        }

        var exact = await _writer.ResolveExactAsync(found.Value.Value, version, cancellationToken);
        if (exact.IsFailure || exact.Value!.Kind != DefinitionKind.AcquisitionConfiguration)
        {
            return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Refuse(AcquisitionCodes.VersionNotFound,
                "Version " + version + " of this acquisition configuration does not exist.");
        }

        return AcquisitionOutcome<AcquisitionConfigurationVersionView>.Accept(ToView(exact.Value));
    }

    /// <summary>
    /// Re-proves an exact stored version against the dataset as it stands now and
    /// against current connector truth. Validation reads; it never changes status.
    /// </summary>
    public async Task<AcquisitionOutcome<AcquisitionValidationReport>> ValidateAsync(
        Guid tenantId, Guid datasetId, int version, CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(tenantId, datasetId, version, cancellationToken);
        if (!resolved.IsAccepted)
        {
            return AcquisitionOutcome<AcquisitionValidationReport>.Refuse(resolved.Refusal!);
        }

        var view = resolved.Value!;
        var refusals = new List<AcquisitionRefusal>();
        var operations = new List<AcquisitionOperationTruth>();

        var stored = AcquisitionConfigurationKernel.FromStored(view.ContentJson);
        if (!stored.IsAccepted)
        {
            refusals.Add(stored.Refusal!);
        }
        else
        {
            var configuration = stored.Value!;
            var governance = await _store.ResolveGovernanceAsync(tenantId, datasetId, cancellationToken);
            if (!governance.IsAccepted || governance.Value!.GovernanceId != configuration.DatasetGovernanceId)
            {
                refusals.Add(new AcquisitionRefusal(AcquisitionCodes.DatasetNotGoverned,
                    "The version names a governance binding this tenant does not hold for this dataset."));
            }
            else
            {
                var fields = await _store.LoadFieldFactsAsync(tenantId, configuration.DatasetGovernanceId, cancellationToken);
                var layouts = await _store.LoadLayoutFactsAsync(tenantId, configuration.DatasetGovernanceId, cancellationToken);
                var references = AcquisitionConfigurationKernel.ValidateReferences(configuration, fields, layouts);
                if (references is not null)
                {
                    refusals.Add(references);
                }
            }

            foreach (var truth in EvaluateOperations(configuration))
            {
                operations.Add(truth);
                if (!truth.Executable)
                {
                    refusals.Add(new AcquisitionRefusal(AcquisitionCodes.OperationNotExecutable,
                        "Required source operation '" + truth.Operation + "' is not executable: " + truth.Evidence));
                }
            }
        }

        return AcquisitionOutcome<AcquisitionValidationReport>.Accept(new AcquisitionValidationReport(
            refusals.Count == 0,
            view.DefinitionId,
            view.Version,
            view.Status,
            view.DefinitionHash,
            operations,
            refusals));
    }

    /// <summary>
    /// The activation request boundary. Resolves the exact published version as a
    /// pinned canonical job target, re-validates it, and then refuses honestly: no
    /// continuous-acquisition runtime is commissioned by this build, so no session,
    /// receipt or running state is ever reported here.
    /// </summary>
    public async Task<AcquisitionOutcome<AcquisitionActivationAdmission>> ActivateAsync(
        Guid tenantId, Guid datasetId, int version, CancellationToken cancellationToken)
    {
        var report = await ValidateAsync(tenantId, datasetId, version, cancellationToken);
        if (!report.IsAccepted)
        {
            return AcquisitionOutcome<AcquisitionActivationAdmission>.Refuse(report.Refusal!);
        }

        var validation = report.Value!;
        var target = JobTargetReference.Pinned(DefinitionKind.AcquisitionConfiguration, validation.DefinitionId, validation.Version);
        var structural = target.Validate();

        string code;
        string detail;
        if (structural is not null)
        {
            code = AcquisitionCodes.ConfigurationInvalid;
            detail = structural;
        }
        else if (!string.Equals(validation.Status, "published", StringComparison.Ordinal))
        {
            code = AcquisitionCodes.VersionNotPublished;
            detail = "Version " + validation.Version + " is " + validation.Status + "; only a published version can be activated.";
        }
        else if (!validation.IsValid)
        {
            code = validation.Refusals[0].Code;
            detail = validation.Refusals[0].Detail;
        }
        else
        {
            code = AcquisitionCodes.RuntimeNotCommissioned;
            detail = "The exact published version resolves as a pinned " + nameof(DefinitionKind.AcquisitionConfiguration) +
                     " target, but no continuous-acquisition runtime is commissioned in this build. Nothing was started.";
        }

        return AcquisitionOutcome<AcquisitionActivationAdmission>.Accept(new AcquisitionActivationAdmission(
            false,
            code,
            detail,
            validation.DefinitionId,
            validation.Version,
            validation.DefinitionHash,
            nameof(DefinitionKind.AcquisitionConfiguration),
            validation.Operations));
    }

    // --------------------------------------------------------------- helpers

    /// <summary>Connector truth for every operation the configuration requires. Read only.</summary>
    private static IReadOnlyList<AcquisitionOperationTruth> EvaluateOperations(
        NormalizedAcquisitionConfiguration configuration) =>
        configuration.RequiredOperations
            .Select(operation => AcquisitionCapabilityTruth.Evaluate(configuration.ProviderType, operation))
            .ToList();

    private static IReadOnlyDictionary<string, object?> DetailOf(NormalizedAcquisitionConfiguration configuration) =>
        new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dataset_governance_id"] = configuration.DatasetGovernanceId,
            ["provider_type"] = configuration.ProviderType,
            ["layout_revision"] = configuration.LayoutRevision,
            ["field_references"] = configuration.FieldReferencesJson,
            ["recording_groups"] = configuration.RecordingGroupsJson,
            ["source_requirements"] = configuration.SourceRequirementsJson,
            ["source_time_reference"] = configuration.SourceTimeReferenceJson,
            ["storage_references"] = configuration.StorageReferencesJson,
            ["accepted_record_contract"] = configuration.AcceptedRecordContractJson,
        };

    private static AcquisitionConfigurationVersionView ToView(CanonicalDefinitionVersion version) =>
        new(version.DefinitionId,
            version.DefinitionCode,
            version.VersionNumber,
            StatusLiteral(version.Status),
            version.DefinitionHash,
            version.ContentJson,
            DateTime.SpecifyKind(version.CreatedAtUtc, DateTimeKind.Utc));

    private static string StatusLiteral(CanonicalVersionStatus status) => status switch
    {
        CanonicalVersionStatus.Draft => "draft",
        CanonicalVersionStatus.Validated => "validated",
        CanonicalVersionStatus.Published => "published",
        CanonicalVersionStatus.PausedByDrift => "paused_by_drift",
        CanonicalVersionStatus.RolledBack => "rolled_back",
        CanonicalVersionStatus.Superseded => "superseded",
        _ => "unknown"
    };

    private async Task<AcquisitionOutcome<T>> InTransactionAsync<T>(
        Func<Task<AcquisitionOutcome<T>>> work,
        CancellationToken cancellationToken)
    {
        if (_db.Database.CurrentTransaction is not null)
        {
            return await work();
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        AcquisitionOutcome<T> result;
        try
        {
            result = await work();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        if (!result.IsAccepted)
        {
            await transaction.RollbackAsync(cancellationToken);
            return result;
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }
}
