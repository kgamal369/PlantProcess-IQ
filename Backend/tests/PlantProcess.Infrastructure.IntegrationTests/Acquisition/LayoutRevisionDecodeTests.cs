// The seam, against a real governed dataset in a runner-owned disposable database.
//
// The layout and the fields are declared through the T-268 authority, exactly as a
// customer would; this suite only reads them back and decodes. The fixture bytes and
// their expected values were authored outside the decoder.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Application.Integration.Acquisition.Decoding;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Integration.Acquisition;
using PlantProcess.Infrastructure.Persistence;
using Npgsql;
using PlantProcess.TestSupport;

namespace PlantProcess.Infrastructure.IntegrationTests.Acquisition;

[Collection(IndustrialAcquisitionLiveCollection.Name)]
public sealed class LayoutRevisionDecodeTests
{
    // >I 1234 | <f -12.5 | bit 3 | ascii "AB12" NUL padded | 0xFF  (Python struct)
    private const string Fixture = "000004d2000048c108414231320000ff";

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

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static FieldDeclarationRequest RawField(string key, string name, string member, string type) =>
        new(null, key, name, type, null, null, new[] { "payload" },
            Json("{\"kind\":\"raw_member\",\"region\":\"block1\",\"path\":\"" + member + "\"}"), null, null);

    private static async Task<Guid> NewInactiveTenantAsync(string connectionString)
    {
        var code = "IAD-" + Guid.NewGuid().ToString("N")[..12];
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var insert = new NpgsqlCommand(
            "INSERT INTO ppiq_meta.tenants (id, tenant_code, display_name, is_active) " +
            "VALUES (@id, @code, @name, false) RETURNING id;", connection);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("code", code);
        insert.Parameters.AddWithValue("name", "Decoder tenant " + code);
        return (Guid)(await insert.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> NewDatasetAsync(string connectionString)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        await using var db = NewContext(connectionString);

        var system = new SourceSystemDefinition("SRC" + suffix, "Source " + suffix, "OpcUaHistorian", false);
        db.SourceSystemDefinitions.Add(system);
        await db.SaveChangesAsync();

        var profile = new ConnectionProfile(system.Id, "CONN" + suffix, "Connection " + suffix, "OpcUaHistorian", false);
        db.ConnectionProfiles.Add(profile);
        await db.SaveChangesAsync();

        var dataset = new SourceDatasetDefinition(profile.Id, "DS" + suffix, "Dataset " + suffix, "ApiEndpoint", "block_" + suffix, false);
        db.SourceDatasetDefinitions.Add(dataset);
        await db.SaveChangesAsync();

        return dataset.Id;
    }

    [SkippableFact]
    public async Task A_stored_revision_decodes_its_payload_and_no_other_revision_is_substituted()
    {
        var connectionString = Connection();
        var tenantId = await NewInactiveTenantAsync(connectionString);
        var otherTenantId = await NewInactiveTenantAsync(connectionString);
        var datasetId = await NewDatasetAsync(connectionString);

        await using var db = NewContext(connectionString);
        var acquisition = new AcquisitionConfigurationService(db, new CanonicalDefinitionWriter(db));
        Assert.True((await acquisition.GovernAsync(tenantId, datasetId, null, CancellationToken.None)).IsAccepted);

        var fields = await acquisition.DeclareFieldsAsync(tenantId, datasetId, new[]
        {
            RawField("counter", "Counter", "block1.counter", "uint32"),
            RawField("status", "Status", "block1.status", "uint8"),
        }, null, CancellationToken.None);
        Assert.True(fields.IsAccepted, fields.Refusal?.Detail);

        var counterId = fields.Value!.Single(f => f.FieldKey == "counter").FieldId;
        var statusId = fields.Value.Single(f => f.FieldKey == "status").FieldId;

        var layout = "{\"layoutKind\":\"raw_block\",\"regionBytes\":16,\"members\":[" +
            "{\"fieldId\":\"" + counterId.ToString("D") + "\",\"type\":\"uint32\",\"byteOffset\":0,\"byteOrder\":\"big\"}," +
            "{\"fieldId\":\"" + statusId.ToString("D") + "\",\"type\":\"uint8\",\"byteOffset\":15}]}";

        var declared = await acquisition.DeclareLayoutAsync(tenantId, datasetId, Json(layout), null, CancellationToken.None);
        Assert.True(declared.IsAccepted, declared.Refusal?.Detail);
        var revision = declared.Value!.Revision;

        var service = new LayoutRevisionDecodeService(db);
        var payload = Convert.FromHexString(Fixture);
        var delivery = new DeliveryAttempt("frame-1", 1, DateTime.UtcNow);

        var decoded = await service.DecodeAsync(
            tenantId, datasetId, revision, payload, new[] { counterId }, delivery, null, CancellationToken.None);

        Assert.True(decoded.IsAccepted, decoded.Refusal?.Detail);
        Assert.Equal(revision, decoded.Value!.LayoutRevision);
        Assert.Equal("1234", decoded.Value.Decoded.Values.Single(v => v.FieldId == counterId).CanonicalText);
        Assert.Equal("255", decoded.Value.Decoded.Values.Single(v => v.FieldId == statusId).CanonicalText);
        Assert.Equal(declared.Value.SemanticHash, decoded.Value.LayoutSemanticHash);

        // A replay under the same revision is the same occurrence, whatever the delivery says.
        var replay = await service.DecodeAsync(
            tenantId, datasetId, revision, payload, new[] { counterId },
            new DeliveryAttempt("frame-1", 7, DateTime.UtcNow.AddHours(3)), null, CancellationToken.None);
        Assert.Equal(decoded.Value.Identity.Value, replay.Value!.Identity.Value);
        Assert.Equal(decoded.Value.Decoded.ContentDigest, replay.Value.Decoded.ContentDigest);

        // A revision that does not exist is refused; nothing newer is substituted.
        var missing = await service.DecodeAsync(
            tenantId, datasetId, revision + 5, payload, new[] { counterId }, delivery, null, CancellationToken.None);
        Assert.False(missing.IsAccepted);
        Assert.StartsWith("IAD12", missing.Refusal!.Code, StringComparison.Ordinal);

        var noRevision = await service.DecodeAsync(
            tenantId, datasetId, 0, payload, new[] { counterId }, delivery, null, CancellationToken.None);
        Assert.False(noRevision.IsAccepted);
        Assert.StartsWith("IAD12", noRevision.Refusal!.Code, StringComparison.Ordinal);

        // Another tenant reaches nothing, not even to learn that the revision exists.
        var foreign = await service.DecodeAsync(
            otherTenantId, datasetId, revision, payload, new[] { counterId }, delivery, null, CancellationToken.None);
        Assert.False(foreign.IsAccepted);
        Assert.StartsWith("IAG03", foreign.Refusal!.Code, StringComparison.Ordinal);

        // A truncated delivery is refused whole: no partial accepted record.
        var truncated = await service.DecodeAsync(
            tenantId, datasetId, revision, payload.AsMemory(0, 8), new[] { counterId }, delivery, null, CancellationToken.None);
        Assert.False(truncated.IsAccepted);
        Assert.StartsWith("IAD03", truncated.Refusal!.Code, StringComparison.Ordinal);
    }
}
