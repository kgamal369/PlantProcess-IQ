// Industrial acquisition configuration and stable field authority, proven against a
// real canonical database built by the runner that owns its lifecycle.
//
// There is no fallback connection: without that database these tests skip, and a
// skip is never counted as evidence. Every tenant created here is INACTIVE, so the
// canonical baseline's single active tenant is untouched.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Definitions;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Application.Jobs.Targeting;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Integration.Acquisition;
using PlantProcess.Infrastructure.Persistence;
using PlantProcess.TestSupport;

namespace PlantProcess.Infrastructure.IntegrationTests.Acquisition;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IndustrialAcquisitionLiveCollection
{
    public const string Name = "IndustrialAcquisitionLive";
}

[Collection(IndustrialAcquisitionLiveCollection.Name)]
public sealed class IndustrialAcquisitionPersistenceTests
{
    private static string Connection()
    {
        var available = TestDatabaseTarget.TryResolve(
            TestDatabaseTarget.IntegrationVariable, out var connectionString, out var reason);
        Skip.IfNot(available, reason);

        var database = TestDatabaseTarget.DatabaseNameOf(connectionString) ?? string.Empty;
        if (!database.Contains("acceptance", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Refusing to mutate '" + database + "': this suite runs only on a runner-owned disposable acceptance database.");
        }

        return connectionString;
    }

    private static PlantProcessDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<PlantProcessDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    private static AcquisitionConfigurationService NewService(PlantProcessDbContext db) =>
        new(db, new CanonicalDefinitionWriter(db));

    private static async Task<Guid> NewInactiveTenantAsync(string connectionString)
    {
        var code = "IAC-" + Guid.NewGuid().ToString("N")[..12];
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var insert = new NpgsqlCommand(
            "INSERT INTO ppiq_meta.tenants (id, tenant_code, display_name, is_active) " +
            "VALUES (@id, @code, @name, false) RETURNING id;", connection);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("code", code);
        insert.Parameters.AddWithValue("name", "Acquisition tenant " + code);
        return (Guid)(await insert.ExecuteScalarAsync())!;
    }

    /// <summary>A legacy connection and dataset, created through the entities that own them.</summary>
    private static async Task<Guid> NewDatasetAsync(string connectionString, string providerType, string datasetKind)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        await using var db = NewContext(connectionString);

        var system = new SourceSystemDefinition("SRC" + suffix, "Source " + suffix, providerType, false);
        db.SourceSystemDefinitions.Add(system);
        await db.SaveChangesAsync();

        var profile = new ConnectionProfile(system.Id, "CONN" + suffix, "Connection " + suffix, providerType, false);
        db.ConnectionProfiles.Add(profile);
        await db.SaveChangesAsync();

        var dataset = new SourceDatasetDefinition(
            profile.Id, "DS" + suffix, "Dataset " + suffix, datasetKind, "object_" + suffix, false);
        db.SourceDatasetDefinitions.Add(dataset);
        await db.SaveChangesAsync();

        return dataset.Id;
    }

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static FieldDeclarationRequest FileField(
        string key, string name, string column, string type = "float64", Guid? fieldId = null, string? reconcile = null) =>
        new(fieldId, key, name, type, null, null, new[] { "payload" },
            Json("{\"kind\":\"file_column\",\"column\":\"" + column + "\"}"), null, reconcile);

    private static FieldDeclarationRequest OpcField(string key, string name, string identifier, string type = "float32") =>
        new(null, key, name, type, null, null, new[] { "payload" },
            Json("{\"kind\":\"opc_node\",\"namespaceUri\":\"urn:acceptance:source\",\"identifierType\":\"s\",\"identifier\":\"" + identifier + "\"}"),
            null, null);

    private static FieldDeclarationRequest RawField(string key, string name, string member, string type = "int32") =>
        new(null, key, name, type, null, null, new[] { "payload" },
            Json("{\"kind\":\"raw_member\",\"region\":\"block1\",\"path\":\"" + member + "\"}"), null, null);

    /// <summary>A trigger group whose capture strategy needs a latched source record.</summary>
    private static string LatchedContent(Guid fieldId, int revision) =>
        "{\"fields\":[{\"fieldId\":\"" + fieldId.ToString("D") + "\",\"revision\":" + revision + "}]," +
        "\"acquire\":{\"readStrategy\":\"BoundedRead\"}," +
        "\"recordingGroups\":[{\"groupKey\":\"g1\",\"memberFieldIds\":[\"" + fieldId.ToString("D") + "\"]," +
        "\"requiredConsistency\":\"SourceLatchedRecord\",\"policy\":{\"mode\":\"TRIGGERED\"," +
        "\"triggerFieldId\":\"" + fieldId.ToString("D") + "\",\"condition\":\"Change\"," +
        "\"captureStrategy\":\"SourceLatchedRecord\",\"startup\":\"BaselineOnly\"}}]}";

    private static string PeriodicContent(Guid fieldId, int revision, string strategy, int? layoutRevision = null) =>
        "{" + (layoutRevision.HasValue ? "\"layoutRevision\":" + layoutRevision.Value + "," : string.Empty) +
        "\"fields\":[{\"fieldId\":\"" + fieldId.ToString("D") + "\",\"revision\":" + revision + "}]," +
        "\"acquire\":{\"readStrategy\":\"" + strategy + "\"}," +
        "\"recordingGroups\":[{\"groupKey\":\"g1\",\"memberFieldIds\":[\"" + fieldId.ToString("D") + "\"]," +
        "\"requiredConsistency\":\"BoundedReadWindow\",\"policy\":{\"mode\":\"PERIODIC\",\"periodMs\":60000," +
        "\"phaseAnchorUtc\":\"2026-01-01T00:00:00Z\",\"capture\":\"FreshRead\",\"missedTick\":\"Gap\"}}]}";

    // ----------------------------------------------------------- governance

    [SkippableFact]
    public async Task Governance_is_explicit_idempotent_and_owned_by_exactly_one_tenant()
    {
        var connectionString = Connection();
        var tenantA = await NewInactiveTenantAsync(connectionString);
        var tenantB = await NewInactiveTenantAsync(connectionString);
        var datasetId = await NewDatasetAsync(connectionString, "Csv", "CsvFile");

        await using var db = NewContext(connectionString);
        var service = NewService(db);

        var first = await service.GovernAsync(tenantA, datasetId, null, CancellationToken.None);
        var again = await service.GovernAsync(tenantA, datasetId, null, CancellationToken.None);
        var other = await service.GovernAsync(tenantB, datasetId, null, CancellationToken.None);

        Assert.True(first.IsAccepted, first.Refusal?.Detail);
        Assert.True(again.IsAccepted, again.Refusal?.Detail);
        Assert.Equal(first.Value!.GovernanceId, again.Value!.GovernanceId);
        Assert.Equal("Csv", first.Value.ProviderType);

        Assert.False(other.IsAccepted);
        Assert.StartsWith("IAG02", other.Refusal!.Code, StringComparison.Ordinal);

        // A read by the other tenant discloses nothing, not even that it exists.
        var readByOther = await service.ListFieldsAsync(tenantB, datasetId, false, CancellationToken.None);
        Assert.False(readByOther.IsAccepted);
        Assert.StartsWith("IAG03", readByOther.Refusal!.Code, StringComparison.Ordinal);

        // An ungoverned dataset is not governed by anybody.
        var ungoverned = await NewDatasetAsync(connectionString, "Csv", "CsvFile");
        var absent = await service.ListFieldsAsync(tenantA, ungoverned, false, CancellationToken.None);
        Assert.False(absent.IsAccepted);
        Assert.StartsWith("IAG03", absent.Refusal!.Code, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Two_tenants_claiming_one_dataset_at_once_produce_exactly_one_owner()
    {
        var connectionString = Connection();
        var tenantA = await NewInactiveTenantAsync(connectionString);
        var tenantB = await NewInactiveTenantAsync(connectionString);
        var datasetId = await NewDatasetAsync(connectionString, "Csv", "CsvFile");

        async Task<bool> ClaimAsync(Guid tenantId)
        {
            await using var db = NewContext(connectionString);
            var result = await NewService(db).GovernAsync(tenantId, datasetId, null, CancellationToken.None);
            return result.IsAccepted;
        }

        var claims = await Task.WhenAll(ClaimAsync(tenantA), ClaimAsync(tenantB));

        // The database serialises the claim; the race cannot produce two owners.
        Assert.Equal(1, claims.Count(accepted => accepted));
    }

    // --------------------------------------------------------------- fields

    [SkippableFact]
    public async Task A_field_identity_survives_a_refresh_and_only_an_explicit_reconciliation_moves_it()
    {
        var connectionString = Connection();
        var tenantId = await NewInactiveTenantAsync(connectionString);
        var datasetId = await NewDatasetAsync(connectionString, "Csv", "CsvFile");

        await using var db = NewContext(connectionString);
        var service = NewService(db);
        Assert.True((await service.GovernAsync(tenantId, datasetId, null, CancellationToken.None)).IsAccepted);

        var registered = await service.DeclareFieldsAsync(
            tenantId, datasetId, new[] { FileField("temperature", "Temperature", "temp_c") }, null, CancellationToken.None);
        Assert.True(registered.IsAccepted, registered.Refusal?.Detail);
        var fieldId = registered.Value![0].FieldId;
        Assert.Equal("registered", registered.Value[0].ChangeKind);

        // The same source metadata again: the identity holds and no revision is written.
        var refreshed = await service.DeclareFieldsAsync(
            tenantId, datasetId, new[] { FileField("temperature", "Temperature", "temp_c") }, null, CancellationToken.None);
        Assert.Equal(fieldId, refreshed.Value![0].FieldId);
        Assert.Equal("unchanged", refreshed.Value[0].ChangeKind);
        Assert.False(refreshed.Value[0].Created);

        // A label rename keeps the identity and creates a governed revision.
        var renamed = await service.DeclareFieldsAsync(
            tenantId, datasetId, new[] { FileField("temperature", "Inlet temperature", "temp_c") }, null, CancellationToken.None);
        Assert.Equal(fieldId, renamed.Value![0].FieldId);
        Assert.Equal(2, renamed.Value[0].Revision);
        Assert.Equal("metadata", renamed.Value[0].ChangeKind);

        // A type change is never assumed to be the same field.
        var retyped = await service.DeclareFieldsAsync(
            tenantId, datasetId, new[] { FileField("temperature", "Inlet temperature", "temp_c", "float32") }, null, CancellationToken.None);
        Assert.False(retyped.IsAccepted);
        Assert.StartsWith("IAF04", retyped.Refusal!.Code, StringComparison.Ordinal);

        // A changed locator without a reconciliation is refused for a named field.
        var moved = await service.DeclareFieldsAsync(
            tenantId, datasetId,
            new[] { FileField("temperature", "Inlet temperature", "temp_celsius", "float64", fieldId) },
            null, CancellationToken.None);
        Assert.False(moved.IsAccepted);
        Assert.StartsWith("IAF03", moved.Refusal!.Code, StringComparison.Ordinal);

        // With the explicit reconciliation it is the same field at a new revision.
        var reconciled = await service.DeclareFieldsAsync(
            tenantId, datasetId,
            new[] { FileField("temperature", "Inlet temperature", "temp_celsius", "float64", fieldId, "same_field") },
            null, CancellationToken.None);
        Assert.True(reconciled.IsAccepted, reconciled.Refusal?.Detail);
        Assert.Equal(fieldId, reconciled.Value![0].FieldId);
        Assert.Equal(3, reconciled.Value[0].Revision);
        Assert.Equal("reconciled_locator", reconciled.Value[0].ChangeKind);

        // The old locator, presented again with no identity, is a NEW field. It does
        // not inherit the identity that used to read it.
        var rediscovered = await service.DeclareFieldsAsync(
            tenantId, datasetId, new[] { FileField("temperature_legacy", "Temperature (old column)", "temp_c") },
            null, CancellationToken.None);
        Assert.True(rediscovered.IsAccepted, rediscovered.Refusal?.Detail);
        Assert.NotEqual(fieldId, rediscovered.Value![0].FieldId);

        // Two fields cannot share one technical key.
        var duplicateKey = await service.DeclareFieldsAsync(
            tenantId, datasetId, new[] { FileField("temperature", "Clash", "another_column") }, null, CancellationToken.None);
        Assert.False(duplicateKey.IsAccepted);
        Assert.StartsWith("IAF05", duplicateKey.Refusal!.Code, StringComparison.Ordinal);

        // History is readable and immutable.
        var history = await service.ListFieldsAsync(tenantId, datasetId, true, CancellationToken.None);
        Assert.Equal(4, history.Value!.Count(r => r.FieldId == fieldId || r.FieldId == rediscovered.Value[0].FieldId));

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var mutate = new NpgsqlCommand(
            "UPDATE ppiq_meta.source_field_revisions SET display_name = 'rewritten' WHERE field_id = @id;", connection);
        mutate.Parameters.AddWithValue("id", fieldId);
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => mutate.ExecuteNonQueryAsync());
        Assert.Contains("IAR01", refusal.MessageText, StringComparison.Ordinal);
    }

    // -------------------------------------------------------- configuration

    [SkippableFact]
    public async Task A_configuration_version_is_immutable_hashed_by_semantics_and_bindable_as_an_exact_target()
    {
        var connectionString = Connection();
        var tenantId = await NewInactiveTenantAsync(connectionString);
        var datasetId = await NewDatasetAsync(connectionString, "Csv", "CsvFile");
        var ownerId = Guid.NewGuid();

        await using var db = NewContext(connectionString);
        var service = NewService(db);
        Assert.True((await service.GovernAsync(tenantId, datasetId, null, CancellationToken.None)).IsAccepted);

        var fields = await service.DeclareFieldsAsync(
            tenantId, datasetId, new[] { FileField("temperature", "Temperature", "temp_c") }, null, CancellationToken.None);
        var fieldId = fields.Value![0].FieldId;

        var draft = await service.CreateVersionAsync(
            tenantId, ownerId, datasetId, Json(PeriodicContent(fieldId, 1, "BoundedRead")), false, CancellationToken.None);
        Assert.True(draft.IsAccepted, draft.Refusal?.Detail);
        Assert.Equal("draft", draft.Value!.Status);

        // Identical semantics do not fork a version.
        var again = await service.CreateVersionAsync(
            tenantId, ownerId, datasetId,
            Json(PeriodicContent(fieldId, 1, "BoundedRead").Replace("\"periodMs\":60000", "\"periodMs\": 60000 ")),
            false, CancellationToken.None);
        Assert.Equal(draft.Value.Version, again.Value!.Version);
        Assert.Equal(draft.Value.DefinitionHash, again.Value.DefinitionHash);

        // A semantic change is a new immutable version with a different hash.
        var second = await service.CreateVersionAsync(
            tenantId, ownerId, datasetId,
            Json(PeriodicContent(fieldId, 1, "BoundedRead").Replace("\"periodMs\":60000", "\"periodMs\":30000")),
            true, CancellationToken.None);
        Assert.True(second.IsAccepted, second.Refusal?.Detail);
        Assert.Equal(draft.Value.Version + 1, second.Value!.Version);
        Assert.NotEqual(draft.Value.DefinitionHash, second.Value.DefinitionHash);
        Assert.Equal("published", second.Value.Status);

        // Old versions stay readable exactly as written.
        var reread = await service.ResolveAsync(tenantId, datasetId, draft.Value.Version, CancellationToken.None);
        Assert.Equal(draft.Value.ContentJson, reread.Value!.ContentJson);

        // The published version is immutable in the database itself.
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var mutate = new NpgsqlCommand(
                "UPDATE ppiq_meta.definition_versions SET graph_json = '{}'::jsonb " +
                "WHERE definition_id = @id AND version_number = @version;", connection);
            mutate.Parameters.AddWithValue("id", second.Value.DefinitionId);
            mutate.Parameters.AddWithValue("version", second.Value.Version);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => mutate.ExecuteNonQueryAsync());
            Assert.True(failure.SqlState is "23514" or "P0001", failure.ToString());
            Assert.Contains("immut", failure.MessageText, StringComparison.OrdinalIgnoreCase);
        }

        // The canonical version authority resolves the kind like any other: the
        // published version by status, and one exact pinned version by number.
        var definitions = new DefinitionService(db);
        var versions = await definitions.ListVersionsAsync(
            DefinitionKind.AcquisitionConfiguration, second.Value.DefinitionId, CancellationToken.None);
        Assert.True(versions.IsSuccess, versions.Error?.Message);
        Assert.Contains(versions.Value!, v => v.VersionNumber == second.Value.Version && v.IsPublished);
        Assert.DoesNotContain(versions.Value!, v => v.VersionNumber == draft.Value.Version && v.IsPublished);

        // Structural bindability through the existing target channel. T-275 owns the
        // ContinuousAcquisition family and its executor; nothing here invents one.
        var pinned = JobTargetReference.Pinned(
            DefinitionKind.AcquisitionConfiguration, second.Value.DefinitionId, second.Value.Version);
        Assert.Null(pinned.Validate());

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var agreement = new NpgsqlCommand(
                "SELECT ppiq_meta.job_target_kind_normalised(@member) = " +
                "       ppiq_meta.job_target_kind_normalised(s.definition_kind) " +
                "  FROM ppiq_meta.definition_store s WHERE s.id = @id;", connection);
            agreement.Parameters.AddWithValue("member", nameof(DefinitionKind.AcquisitionConfiguration));
            agreement.Parameters.AddWithValue("id", second.Value.DefinitionId);
            Assert.True((bool)(await agreement.ExecuteScalarAsync())!,
                "The job target channel does not recognise the stored kind as the enum member name.");
        }
    }

    [SkippableFact]
    public async Task A_published_configuration_is_revalidated_against_the_dataset_as_it_stands()
    {
        var connectionString = Connection();
        var tenantId = await NewInactiveTenantAsync(connectionString);
        var datasetId = await NewDatasetAsync(connectionString, "Csv", "CsvFile");
        var ownerId = Guid.NewGuid();

        await using var db = NewContext(connectionString);
        var service = NewService(db);
        Assert.True((await service.GovernAsync(tenantId, datasetId, null, CancellationToken.None)).IsAccepted);

        var fields = await service.DeclareFieldsAsync(
            tenantId, datasetId, new[] { FileField("flow", "Flow", "flow_rate") }, null, CancellationToken.None);
        var fieldId = fields.Value![0].FieldId;

        var published = await service.CreateVersionAsync(
            tenantId, ownerId, datasetId, Json(PeriodicContent(fieldId, 1, "BoundedRead")), true, CancellationToken.None);
        Assert.True(published.IsAccepted, published.Refusal?.Detail);

        var valid = await service.ValidateAsync(tenantId, datasetId, published.Value!.Version, CancellationToken.None);
        Assert.True(valid.Value!.IsValid, string.Join("; ", valid.Value.Refusals.Select(r => r.Code + " " + r.Detail)));
        Assert.All(valid.Value.Operations, operation => Assert.True(operation.Executable, operation.Evidence));

        // Activation resolves the exact target and then refuses honestly: no runtime
        // is commissioned, so no session, receipt or running state is reported.
        var activation = await service.ActivateAsync(tenantId, datasetId, published.Value.Version, CancellationToken.None);
        Assert.True(activation.IsAccepted, activation.Refusal?.Detail);
        Assert.False(activation.Value!.Admitted);
        Assert.StartsWith("IAC08", activation.Value.Code, StringComparison.Ordinal);
        Assert.Equal(published.Value.Version, activation.Value.Version);
        Assert.Equal(nameof(DefinitionKind.AcquisitionConfiguration), activation.Value.TargetDefinitionKind);

        // The field moves on; the published configuration now names a stale revision
        // and says so rather than running against something it never described.
        var renamed = await service.DeclareFieldsAsync(
            tenantId, datasetId, new[] { FileField("flow", "Flow rate", "flow_rate") }, null, CancellationToken.None);
        Assert.Equal(2, renamed.Value![0].Revision);

        var stale = await service.ValidateAsync(tenantId, datasetId, published.Value.Version, CancellationToken.None);
        Assert.False(stale.Value!.IsValid);
        Assert.Contains(stale.Value.Refusals, r => r.Code == AcquisitionCodes.FieldRevisionStale);

        var refusedActivation = await service.ActivateAsync(tenantId, datasetId, published.Value.Version, CancellationToken.None);
        Assert.False(refusedActivation.Value!.Admitted);
        Assert.StartsWith("IAC03", refusedActivation.Value.Code, StringComparison.Ordinal);
    }

    /// <summary>
    /// The negative control is deliberately an operation NO provider's truth declares -
    /// a latched source record - so it refuses in every environment and no later
    /// commissioning commit can turn this gate green by accident. The OPC operations
    /// are asserted against current truth rather than against a captured expectation.
    /// </summary>
    [SkippableFact]
    public async Task A_required_operation_the_connector_cannot_execute_refuses_publication_and_activation()
    {
        var connectionString = Connection();
        var tenantId = await NewInactiveTenantAsync(connectionString);
        var datasetId = await NewDatasetAsync(connectionString, "Csv", "CsvFile");
        var ownerId = Guid.NewGuid();

        await using var db = NewContext(connectionString);
        var service = NewService(db);
        Assert.True((await service.GovernAsync(tenantId, datasetId, null, CancellationToken.None)).IsAccepted);

        var fields = await service.DeclareFieldsAsync(
            tenantId, datasetId,
            new[] { FileField("pressure", "Pressure", "pressure_bar", "int32") },
            null, CancellationToken.None);
        Assert.True(fields.IsAccepted, fields.Refusal?.Detail);
        var fieldId = fields.Value![0].FieldId;

        // A latched-record capture strategy requires sourceLatchedRecord, which connector
        // truth declares for no provider at all.
        Assert.False(AcquisitionCapabilityTruth.Evaluate("Csv", AcquisitionCapabilityTruth.SourceLatchedRecord).Executable);
        var content = Json(LatchedContent(fieldId, 1));

        // Publication is a semantic act: an unexecutable operation never reaches
        // Published state, and the refusal leaves no version behind it.
        var publishAttempt = await service.CreateVersionAsync(tenantId, ownerId, datasetId, content, true, CancellationToken.None);
        Assert.False(publishAttempt.IsAccepted);
        Assert.StartsWith("IAC05", publishAttempt.Refusal!.Code, StringComparison.Ordinal);

        var none = await service.ListVersionsAsync(tenantId, datasetId, CancellationToken.None);
        Assert.Empty(none.Value!);

        // A draft may still be authored - authoring intent is not a runtime claim.
        var draft = await service.CreateVersionAsync(tenantId, ownerId, datasetId, content, false, CancellationToken.None);
        Assert.True(draft.IsAccepted, draft.Refusal?.Detail);

        var report = await service.ValidateAsync(tenantId, datasetId, draft.Value!.Version, CancellationToken.None);
        Assert.False(report.Value!.IsValid);
        Assert.Contains(report.Value.Refusals, r => r.Code == AcquisitionCodes.OperationNotExecutable);
        Assert.Contains(report.Value.Operations, o => o.Operation == AcquisitionCapabilityTruth.SourceLatchedRecord && !o.Executable);

        // Every operation in the report answers current connector truth exactly, with no
        // expectation captured in this test.
        foreach (var operation in report.Value.Operations)
        {
            var truth = AcquisitionCapabilityTruth.Evaluate("Csv", operation.Operation);
            Assert.Equal(truth.Executable, operation.Executable);
        }

        var activation = await service.ActivateAsync(tenantId, datasetId, draft.Value.Version, CancellationToken.None);
        Assert.False(activation.Value!.Admitted);
        Assert.StartsWith("IAC0", activation.Value.Code, StringComparison.Ordinal);
    }

    // -------------------------------------------------------------- layouts

    [SkippableFact]
    public async Task A_raw_member_is_decoded_only_through_an_exact_immutable_layout_revision()
    {
        var connectionString = Connection();
        var tenantId = await NewInactiveTenantAsync(connectionString);
        var datasetId = await NewDatasetAsync(connectionString, "OpcUaHistorian", "ApiEndpoint");
        var ownerId = Guid.NewGuid();

        await using var db = NewContext(connectionString);
        var service = NewService(db);
        Assert.True((await service.GovernAsync(tenantId, datasetId, null, CancellationToken.None)).IsAccepted);

        var declared = await service.DeclareFieldsAsync(
            tenantId, datasetId,
            new[] { RawField("counter", "Counter", "block1.counter"), OpcField("pressure", "Pressure", "Line/Pressure") },
            null, CancellationToken.None);
        Assert.True(declared.IsAccepted, declared.Refusal?.Detail);

        var rawFieldId = declared.Value![0].FieldId;
        var typedFieldId = declared.Value[1].FieldId;

        string Layout(Guid id, string type) =>
            "{\"layoutKind\":\"raw_block\",\"regionBytes\":8,\"members\":[{\"fieldId\":\"" + id.ToString("D") +
            "\",\"type\":\"" + type + "\",\"byteOffset\":0,\"byteOrder\":\"big\"}]}";

        // A typed source item is never placed as raw bytes.
        var typedInLayout = await service.DeclareLayoutAsync(tenantId, datasetId, Json(Layout(typedFieldId, "float32")), null, CancellationToken.None);
        Assert.False(typedInLayout.IsAccepted);
        Assert.StartsWith("IAL01", typedInLayout.Refusal!.Code, StringComparison.Ordinal);

        // A placement that contradicts the declared type is refused.
        var wrongType = await service.DeclareLayoutAsync(tenantId, datasetId, Json(Layout(rawFieldId, "int16")), null, CancellationToken.None);
        Assert.False(wrongType.IsAccepted);

        var layout = await service.DeclareLayoutAsync(tenantId, datasetId, Json(Layout(rawFieldId, "int32")), null, CancellationToken.None);
        Assert.True(layout.IsAccepted, layout.Refusal?.Detail);
        Assert.Equal(1, layout.Value!.Revision);
        Assert.True(layout.Value.Created);

        var identical = await service.DeclareLayoutAsync(tenantId, datasetId, Json(Layout(rawFieldId, "int32")), null, CancellationToken.None);
        Assert.Equal(1, identical.Value!.Revision);
        Assert.False(identical.Value.Created);

        // A configuration over a raw member needs the exact layout revision.
        var withoutLayout = await service.CreateVersionAsync(
            tenantId, ownerId, datasetId, Json(PeriodicContent(rawFieldId, 1, "Subscription")), false, CancellationToken.None);
        Assert.False(withoutLayout.IsAccepted);
        Assert.StartsWith("IAC04", withoutLayout.Refusal!.Code, StringComparison.Ordinal);

        var withLayout = await service.CreateVersionAsync(
            tenantId, ownerId, datasetId, Json(PeriodicContent(rawFieldId, 1, "Subscription", 1)), false, CancellationToken.None);
        Assert.True(withLayout.IsAccepted, withLayout.Refusal?.Detail);
    }

    // --------------------------------------------------------- portability

    [SkippableFact]
    public async Task An_exported_configuration_carries_its_meaning_and_no_credential_material()
    {
        var connectionString = Connection();
        var tenantId = await NewInactiveTenantAsync(connectionString);
        var targetTenantId = await NewInactiveTenantAsync(connectionString);
        var datasetId = await NewDatasetAsync(connectionString, "Csv", "CsvFile");
        var ownerId = Guid.NewGuid();

        await using var db = NewContext(connectionString);
        var service = NewService(db);
        Assert.True((await service.GovernAsync(tenantId, datasetId, null, CancellationToken.None)).IsAccepted);

        var fields = await service.DeclareFieldsAsync(
            tenantId, datasetId, new[] { FileField("level", "Level", "level_pct") }, null, CancellationToken.None);
        var published = await service.CreateVersionAsync(
            tenantId, ownerId, datasetId, Json(PeriodicContent(fields.Value![0].FieldId, 1, "BoundedRead")), true, CancellationToken.None);
        Assert.True(published.IsAccepted, published.Refusal?.Detail);

        var exporter = new DefinitionExporter(db, new CanonicalDefinitionGraph(db));
        var exported = await exporter.ExportAsync(tenantId, published.Value!.DefinitionId, published.Value.Version, CancellationToken.None);
        Assert.True(exported.IsSuccess, exported.Error?.Message);

        var artifact = exported.Value!;
        var root = artifact.Definitions.Single();
        Assert.Equal("acquisition_configuration", root.Kind);
        Assert.Equal("S1", root.Surface);
        Assert.Equal(published.Value.ContentJson, root.ContentJson);

        var serialized = JsonSerializer.Serialize(artifact);
        foreach (var secret in new[] { "password", "secret", "connectionString", "token" })
        {
            Assert.DoesNotContain(secret, serialized, StringComparison.OrdinalIgnoreCase);
        }

        // Imported into another tenant, the same semantics arrive: same content, same
        // kind, same surface, and a definition of its own rather than a shared row.
        var importer = new DefinitionImporter(db, new CanonicalDefinitionWriter(db), exporter);
        var imported = await importer.ImportAsync(targetTenantId, ownerId, artifact, CancellationToken.None);
        Assert.True(imported.IsSuccess, imported.Error?.Message);

        var importedRoot = imported.Value!.Imported.Single();
        Assert.NotEqual(published.Value.DefinitionId, importedRoot.DefinitionId);

        var writer = new CanonicalDefinitionWriter(db);
        var importedVersion = await writer.ResolveExactAsync(importedRoot.DefinitionId, importedRoot.VersionNumber, CancellationToken.None);
        Assert.True(importedVersion.IsSuccess, importedVersion.Error?.Message);
        Assert.Equal(published.Value.ContentJson, importedVersion.Value!.ContentJson);
        Assert.Equal(DefinitionKind.AcquisitionConfiguration, importedVersion.Value.Kind);
    }
    // These controls construct valid parents first. A wrong FK, an empty UPDATE or
    // an empty INSERT...SELECT may not masquerade as the intended refusal.
    private static async Task<(Guid Tenant, Guid Dataset, Guid Field, Guid Definition, int Version)>
        GuardFixtureAsync(string connectionString)
    {
        var tenant = await NewInactiveTenantAsync(connectionString);
        var dataset = await NewDatasetAsync(connectionString, "Csv", "CsvFile");
        await using var db = NewContext(connectionString);
        var service = NewService(db);
        var governance = await service.GovernAsync(tenant, dataset, null, CancellationToken.None);
        Assert.True(governance.IsAccepted, governance.Refusal?.Detail);
        var fields = await service.DeclareFieldsAsync(tenant, dataset,
            new[] { FileField("guard_field", "Guard field", "foreign_measure") }, null, CancellationToken.None);
        Assert.True(fields.IsAccepted, fields.Refusal?.Detail);
        var field = fields.Value![0].FieldId;
        var created = await service.CreateVersionAsync(tenant, Guid.NewGuid(), dataset,
            Json(PeriodicContent(field, 1, "BoundedRead")), false, CancellationToken.None);
        Assert.True(created.IsAccepted, created.Refusal?.Detail);
        return (tenant, dataset, field, created.Value!.DefinitionId, created.Value.Version);
    }

    private static async Task<PostgresException> RefusedAsync(NpgsqlConnection connection,
        string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }

    [SkippableFact]
    public async Task Database_negative_controls_fail_for_the_intended_constraint_with_valid_parents()
    {
        var connectionString = Connection();
        var f = await GuardFixtureAsync(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var count = new NpgsqlCommand(
            "SELECT count(*) FROM ppiq_meta.definition_store WHERE id=@id AND tenant_id=@tenant", connection))
        {
            count.Parameters.AddWithValue("id", f.Definition);
            count.Parameters.AddWithValue("tenant", f.Tenant);
            Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
        }

        var invalidKind = await RefusedAsync(connection,
            "INSERT INTO ppiq_meta.definition_store " +
            "(id,tenant_id,definition_code,surface,definition_kind,name,owner_id,current_version) " +
            "SELECT @new,tenant_id,@code,'S1','not_a_kind','Negative kind',owner_id,0 " +
            "FROM ppiq_meta.definition_store WHERE id=@parent",
            ("new", Guid.NewGuid()), ("code", "NG-" + Guid.NewGuid().ToString("N")), ("parent", f.Definition));
        Assert.Equal("23514", invalidKind.SqlState);
        Assert.Equal("ck_definition_store_kind", invalidKind.ConstraintName);

        var missingDataset = await RefusedAsync(connection,
            "SELECT ppiq_meta.govern_source_dataset(@tenant,@absent,NULL)",
            ("tenant", f.Tenant), ("absent", Guid.NewGuid()));
        Assert.Equal("P0001", missingDataset.SqlState);
        Assert.Contains("dataset_not_found", missingDataset.MessageText, StringComparison.OrdinalIgnoreCase);

        await using (var count = new NpgsqlCommand(
            "SELECT count(*) FROM ppiq_meta.source_field_revisions WHERE field_id=@field AND revision=1", connection))
        {
            count.Parameters.AddWithValue("field", f.Field);
            Assert.Equal(1L, (long)(await count.ExecuteScalarAsync())!);
        }
        var badHash = await RefusedAsync(connection,
            "INSERT INTO ppiq_meta.source_field_revisions " +
            "(id,tenant_id,field_id,revision,provider_type,locator_kind,source_locator,locator_identity," +
            "field_key,display_name,declared_type,roles,change_kind,semantic_hash) " +
            "SELECT @new,tenant_id,field_id,999,provider_type,locator_kind,source_locator,locator_identity," +
            "field_key,display_name,declared_type,roles,change_kind,'not-a-hash' " +
            "FROM ppiq_meta.source_field_revisions WHERE field_id=@field AND revision=1",
            ("new", Guid.NewGuid()), ("field", f.Field));
        Assert.Equal("23514", badHash.SqlState);
        Assert.Equal("ck_source_field_revisions_hashes", badHash.ConstraintName);

        var immutable = await RefusedAsync(connection,
            "UPDATE ppiq_meta.source_field_revisions SET display_name='forbidden rewrite' " +
            "WHERE field_id=@field AND revision=1", ("field", f.Field));
        Assert.Equal("23514", immutable.SqlState);
        Assert.Contains("IAR01 revision_immutable", immutable.MessageText, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Acquisition_detail_rejects_an_existing_wrong_kind_parent_not_a_missing_parent()
    {
        var connectionString = Connection();
        var f = await GuardFixtureAsync(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var otherDefinition = Guid.NewGuid();
        var otherVersion = Guid.NewGuid();
        await using (var parents = new NpgsqlCommand(
            "INSERT INTO ppiq_meta.definition_store " +
            "(id,tenant_id,definition_code,surface,definition_kind,name,owner_id,current_version) " +
            "SELECT @other,tenant_id,@code,'S1','transformation','Wrong-kind fixture',owner_id,1 " +
            "FROM ppiq_meta.definition_store WHERE id=@parent; " +
            "INSERT INTO ppiq_meta.definition_versions " +
            "(id,tenant_id,definition_id,version_number,status,mode,graph_json,definition_hash) " +
            "SELECT @version,tenant_id,@other,1,'draft','block',graph_json,definition_hash " +
            "FROM ppiq_meta.definition_versions WHERE definition_id=@parent AND version_number=@number;",
            connection, transaction))
        {
            parents.Parameters.AddWithValue("other", otherDefinition);
            parents.Parameters.AddWithValue("version", otherVersion);
            parents.Parameters.AddWithValue("parent", f.Definition);
            parents.Parameters.AddWithValue("number", f.Version);
            parents.Parameters.AddWithValue("code", "WP-" + Guid.NewGuid().ToString("N"));
            Assert.Equal(2, await parents.ExecuteNonQueryAsync());
        }
        await using (var verify = new NpgsqlCommand(
            "SELECT s.definition_kind FROM ppiq_meta.definition_versions v " +
            "JOIN ppiq_meta.definition_store s ON s.id=v.definition_id WHERE v.id=@version", connection, transaction))
        {
            verify.Parameters.AddWithValue("version", otherVersion);
            Assert.Equal("transformation", (string)(await verify.ExecuteScalarAsync())!);
        }
        await using var detail = new NpgsqlCommand(
            "INSERT INTO ppiq_meta.acquisition_configuration_details(definition_version_id) VALUES(@version)",
            connection, transaction);
        detail.Parameters.AddWithValue("version", otherVersion);
        var refused = await Assert.ThrowsAsync<PostgresException>(() => detail.ExecuteNonQueryAsync());
        Assert.True(refused.SqlState is "23514" or "P0001", refused.ToString());
        Assert.Contains("definition_detail_parent_guard", refused.Where ?? "", StringComparison.Ordinal);
        await transaction.RollbackAsync();
    }

    [SkippableFact]
    public async Task Governance_functions_have_explicit_execute_authority_and_revision_tables_deny_direct_mutation()
    {
        await using var connection = new NpgsqlConnection(Connection());
        await connection.OpenAsync();
        await using (var functions = new NpgsqlCommand(
            "SELECT count(*), count(*) FILTER (WHERE p.prosecdef), " +
            "count(*) FILTER (WHERE has_function_privilege('plantprocess_app',p.oid,'EXECUTE')), " +
            "count(*) FILTER (WHERE NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(p.proacl,acldefault('f',p.proowner))) a " +
            "WHERE a.grantee=0 AND a.privilege_type='EXECUTE')) " +
            "FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace " +
            "WHERE n.nspname='ppiq_meta' AND p.proname IN " +
            "('govern_source_dataset','declare_source_field_revision','declare_source_layout_revision')", connection))
        {
            await using var reader = await functions.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            for (var i = 0; i < 4; i++) Assert.Equal(3L, reader.GetInt64(i));
        }
        foreach (var table in new[] { "source_dataset_governance", "source_field_identities", "source_field_revisions", "source_layout_revisions" })
        {
            await using var grants = new NpgsqlCommand(
                "SELECT has_table_privilege('plantprocess_app',@table,'SELECT'), " +
                "has_table_privilege('plantprocess_app',@table,'INSERT'), " +
                "has_table_privilege('plantprocess_app',@table,'UPDATE'), " +
                "has_table_privilege('plantprocess_app',@table,'DELETE'), " +
                "has_table_privilege('plantprocess_app',@table,'TRUNCATE')", connection);
            grants.Parameters.AddWithValue("table", "ppiq_meta." + table);
            await using var reader = await grants.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetBoolean(0));
            for (var i = 1; i < 5; i++) Assert.False(reader.GetBoolean(i), table + " has unintended direct mutation privilege.");
        }
    }

}