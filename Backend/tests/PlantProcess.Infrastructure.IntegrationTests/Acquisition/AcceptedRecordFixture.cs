using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Definitions;
using PlantProcess.Infrastructure.Integration.Acquisition;
using PlantProcess.Infrastructure.IntegrationTests.Definitions;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.IntegrationTests.Acquisition;

/// <summary>
/// Session commissioning is a runtime fixture boundary. It provisions the actual
/// authoritative row as database owner; the runtime role must not be able to do so.
/// Acceptance, locks, receipts, projection and publication use production code.
/// </summary>
internal sealed class AcceptedRecordFixture
{
    private readonly DefinitionStoreFixture _fixture;
    internal Guid Tenant => _fixture.TenantId;
    internal Guid Dataset { get; private set; }
    internal Guid SourceDataset { get; private set; }
    internal string SourceSystem => "accepted:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(Tenant.ToString("D")+"/"+Dataset.ToString("D")+"/"+Stream))).ToLowerInvariant();
    internal Guid Session { get; } = Guid.NewGuid();
    internal Guid Configuration { get; private set; }
    internal string Stream { get; } = "capture-" + Guid.NewGuid().ToString("N");
    internal Dictionary<string, Guid> Fields { get; } = new();
    internal AcceptedRecordFixture(DefinitionStoreFixture fixture) => _fixture = fixture;
    internal PlantProcessDbContext Db() => _fixture.NewContext();
    internal AcceptedRecordStore Store(PlantProcessDbContext db) =>
        new(db, new TransactionalFenceFixture(db), new UnavailableOriginalBytesPreservationAuthority());

    internal async Task InitializeAsync(IReadOnlyDictionary<string,string> fields, bool retentionRequired = false, JsonElement? typeShape = null)
    {
        await using var db = Db();
        Assert.Contains("acceptance", db.Database.GetDbConnection().Database, StringComparison.Ordinal);
        string code = Guid.NewGuid().ToString("N")[..12];
        var system = new SourceSystemDefinition("AR"+code, "Accepted source", "Csv", false);
        db.SourceSystemDefinitions.Add(system); await db.SaveChangesAsync();
        var profile = new ConnectionProfile(system.Id,"AR"+code,"Accepted profile","Csv",false);
        db.ConnectionProfiles.Add(profile); await db.SaveChangesAsync();
        var dataset = new SourceDatasetDefinition(profile.Id,"AR"+code,"Accepted dataset","CsvFile","accepted_"+code,false);
        db.SourceDatasetDefinitions.Add(dataset); await db.SaveChangesAsync(); SourceDataset=dataset.Id;
        var service = new AcquisitionConfigurationService(db,new CanonicalDefinitionWriter(db));
        var governed = await service.GovernAsync(Tenant,dataset.Id,null,CancellationToken.None);
        Assert.True(governed.IsAccepted,governed.Refusal?.Detail);
        Dataset=governed.Value!.GovernanceId;
        var requests = fields.Select(f => new FieldDeclarationRequest(null,f.Key,f.Key,f.Value,typeShape,null,
            new[]{"payload"},JsonSerializer.SerializeToElement(new {kind="file_column",column=f.Key}),null,null)).ToArray();
        var declared = await service.DeclareFieldsAsync(Tenant,dataset.Id,requests,null,CancellationToken.None);
        Assert.True(declared.IsAccepted,declared.Refusal?.Detail);
        foreach(var f in declared.Value!) Fields.Add(f.FieldKey,f.FieldId);
        var content = JsonSerializer.SerializeToElement(new {
            fields=Fields.Values.Select(id=>new {fieldId=id,revision=1}).ToArray(),
            acquire=new {readStrategy="BoundedRead"},
            recordingGroups=new[]{new {groupKey="capture",memberFieldIds=Fields.Values.ToArray(),requiredConsistency="BoundedReadWindow",
                policy=new {mode="PERIODIC",periodMs=60000,phaseAnchorUtc="2026-01-01T00:00:00Z",capture="FreshRead",missedTick="Gap"}}},
            storage=new {retentionPolicyRef=retentionRequired ? "required-original-bytes" : null}
        });
        var config=await service.CreateVersionAsync(Tenant,_fixture.OwnerId,dataset.Id,content,true,CancellationToken.None);
        Assert.True(config.IsAccepted,config.Refusal?.Detail); Configuration=config.Value!.DefinitionId;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ppiq_staging.accepted_capture_fences(tenant_id,dataset_governance_id,stream_key,session_id,generation,valid_until_utc) VALUES({Tenant},{Dataset},{Stream},{Session},1,now()+interval '1 hour')");
    }

    internal AcceptedBatchRequest Request(long ordinal=1, string? start=null) =>
        new(Tenant,Dataset,Guid.NewGuid(),Session,1,Stream,ordinal,Configuration,1,"capture",100,1048576,start);

    internal async Task<AcceptedBatchRequest> OpenAsync(long ordinal=1,string? start=null)
    {
        var request=Request(ordinal,start);
        await using var db=Db();
        var result=await Store(db).OpenAsync(request,CancellationToken.None);
        Assert.True(result.IsAccepted,result.Refusal?.Detail); return request;
    }

    internal JsonElement Envelope(IReadOnlyDictionary<string,object?> values) => JsonSerializer.SerializeToElement(new {
        values=values.ToDictionary(x=>Fields[x.Key].ToString("D"),x=>x.Value),
        capturedAtUtc="2026-09-21T12:00:00Z",quality="Good",consistency="BoundedReadWindow",conversionVersion="1"
    });

    internal async Task<AcquisitionOutcome<AcceptedRecordReceipt>> AppendAsync(AcceptedBatchRequest batch,string record,IReadOnlyDictionary<string,object?> values)
    {
        await using var db=Db();
        return await Store(db).AcceptAsync(new(Tenant,batch.BatchId,Session,1,record,Envelope(values)),CancellationToken.None);
    }

    internal async Task<AcceptedBatchView> SealAsync(AcceptedBatchRequest batch,string? end=null)
    {
        await using var db=Db(); var store=Store(db);
        var current=await store.GetAsync(Tenant,batch.BatchId,CancellationToken.None);
        Assert.True(current.IsAccepted,current.Refusal?.Detail);
        var result=await store.SealAsync(new(Tenant,batch.BatchId,Session,1,current.Value!.RecordCount,current.Value.PayloadBytes,end),CancellationToken.None);
        Assert.True(result.IsAccepted,result.Refusal?.Detail);return result.Value!;
    }

    internal sealed class TransactionalFenceFixture(PlantProcessDbContext db) : ISessionFencingAuthority
    {
        public async Task<AcquisitionRefusal?> ValidateAsync(Guid tenantId,Guid datasetGovernanceId,string streamKey,
            Guid sessionId,long generation,CancellationToken ct)
        {
            await using var command=new NpgsqlCommand("SELECT ppiq_staging.accepted_lock_fence(@t,@d,@s,@i,@g)",
                (NpgsqlConnection)db.Database.GetDbConnection(),(NpgsqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction());
            command.Parameters.AddWithValue("t",tenantId);command.Parameters.AddWithValue("d",datasetGovernanceId);
            command.Parameters.AddWithValue("s",streamKey);command.Parameters.AddWithValue("i",sessionId);command.Parameters.AddWithValue("g",generation);
            await command.ExecuteScalarAsync(ct);return null;
        }
    }
}
