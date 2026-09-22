using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Infrastructure.Integration.Acquisition;
using PlantProcess.Infrastructure.IntegrationTests.Definitions;

namespace PlantProcess.Infrastructure.IntegrationTests.Acquisition;

[Collection("CanonicalDefinitionStore")]
public sealed class AcceptedDatasetContractTests(DefinitionStoreFixture fixture)
{
    private async Task<AcceptedRecordFixture> NewAsync(string type="int64", JsonElement? shape=null,string key="reading")
    {
        var f=new AcceptedRecordFixture(fixture);
        await f.InitializeAsync(new Dictionary<string,string>{{key,type}},false,shape);return f;
    }
    private static JsonElement Shape(object value)=>JsonSerializer.SerializeToElement(value);
    private static JsonElement Envelope(AcceptedRecordFixture f,string raw)
    {
        using var doc=JsonDocument.Parse("{\"values\":{\""+f.Fields.Values.Single()+"\":"+raw+"},\"capturedAtUtc\":\"2026-09-22T00:00:00Z\",\"quality\":\"Good\",\"consistency\":\"BoundedReadWindow\",\"conversionVersion\":\"1\"}");
        return doc.RootElement.Clone();
    }
    private static async Task<AcceptedRecordReceipt> Append(AcceptedRecordFixture f,AcceptedBatchRequest b,string id,string raw)
    {
        await using var db=f.Db();var r=await f.Store(db).AcceptAsync(new(f.Tenant,b.BatchId,f.Session,1,id,Envelope(f,raw)),CancellationToken.None);
        Assert.True(r.IsAccepted,r.Refusal?.Detail);return r.Value!;
    }
    private static async Task<NpgsqlConnection> Connection(AcceptedRecordFixture f)
    {
        await using var db=f.Db();var c=new NpgsqlConnection(db.Database.GetConnectionString());await c.OpenAsync();
        await using var q=new NpgsqlCommand("SELECT set_config('app.current_tenant',@t,false)",c);
        q.Parameters.AddWithValue("t",f.Tenant.ToString());await q.ExecuteScalarAsync();return c;
    }
    private static NpgsqlCommand AppendCommand(AcceptedRecordFixture f,AcceptedBatchRequest b,NpgsqlConnection c,NpgsqlTransaction tx,string id="crash")
    {
        var q=new NpgsqlCommand("SELECT ppiq_staging.accepted_append(@t,@b,@s,1,@id,@e)::text",c,tx);
        q.Parameters.AddWithValue("t",f.Tenant);q.Parameters.AddWithValue("b",b.BatchId);q.Parameters.AddWithValue("s",f.Session);
        q.Parameters.AddWithValue("id",id);q.Parameters.AddWithValue("e",NpgsqlDbType.Jsonb,Envelope(f,"17").GetRawText());return q;
    }

    [Fact]
    public async Task Every_declared_scalar_and_array_type_survives_typed_preview_without_numeric_rounding()
    {
        var cases=new Dictionary<string,string>{["boolean"]="true",["int8"]="-128",["uint8"]="255",["int16"]="-32768",["uint16"]="65535",["int32"]="-2147483648",["uint32"]="4294967295",["int64"]="-9223372036854775808",["uint64"]="18446744073709551615",["decimal"]="123456789012345678901234567890123456.78",["float32"]="1.25",["float64"]="1.25",["string"]="\"exact\"",["bytes"]="\"YWJj\"",["datetime"]="\"2026-09-22T12:00:00Z\"",["date"]="\"2026-09-22\"",["time"]="\"12:30:00\"",["duration"]="\"01:30:00\"",["guid"]="\"00000000-0000-0000-0000-000000000123\""};
        Assert.Equal(FieldDeclarationKernel.DeclaredTypes.OrderBy(x=>x),cases.Keys.OrderBy(x=>x));
        foreach(var entry in cases) foreach(var array in new[]{false,true})
        {
            var shape=new Dictionary<string,int>();if(array)shape["arrayLength"]=2;
            if(entry.Key=="decimal"){shape["precision"]=38;shape["scale"]=2;}
            var f=await NewAsync(entry.Key,Shape(shape));var b=await f.OpenAsync();
            await Append(f,b,"one",array?"["+entry.Value+",null]":entry.Value);await f.SealAsync(b);
            await using var db=f.Db();var result=await new AcceptedDatasetReader(db).PreviewAsync(f.Tenant,f.SourceDataset,b.BatchId,1,CancellationToken.None);
            Assert.True(result.IsAccepted,result.Refusal?.Detail);Assert.Single(result.Value!.Records);
            var value=Assert.IsType<JsonElement>(result.Value.Records[0]["reading"]);
            if(array){Assert.Equal(2,value.GetArrayLength());Assert.Equal(JsonValueKind.Null,value[1].ValueKind);value=value[0];}
            if(entry.Key is "decimal" or "uint64" or "int64")Assert.Equal(entry.Value,value.GetRawText());
        }
    }

    [Fact]
    public async Task Long_technical_key_keeps_field_identity_with_a_stable_sql_column()
    {
        string key="Pressure.Line-"+new string('a',150);var f=await NewAsync(key:key);var b=await f.OpenAsync();await Append(f,b,"one","17");await f.SealAsync(b);
        await using var db=f.Db();var r=await new AcceptedDatasetReader(db).PreviewAsync(f.Tenant,f.SourceDataset,b.BatchId,1,CancellationToken.None);
        Assert.True(r.IsAccepted,r.Refusal?.Detail);Assert.Equal(key,r.Value!.Fields[0].GetProperty("key").GetString());
        var column="field_"+f.Fields[key].ToString("N");Assert.Equal(column,r.Value.Fields[0].GetProperty("sourceColumn").GetString());
        Assert.Equal(17,Assert.IsType<JsonElement>(r.Value.Records[0][column]).GetInt32());
    }

    [Fact]
    public async Task Arrays_refuse_wrong_length_wrong_member_type_and_overflow_atomically()
    {
        var f=await NewAsync("uint8",Shape(new{arrayLength=2}));var b=await f.OpenAsync();await using var db=f.Db();
        foreach(var value in new[]{"[1]","[1,256]","[1,\"2\"]","[1,-1]"})
        {var r=await f.Store(db).AcceptAsync(new(f.Tenant,b.BatchId,f.Session,1,"bad",Envelope(f,value)),CancellationToken.None);Assert.Equal("AR12",r.Refusal?.Code);}
        Assert.Equal(0,(await f.Store(db).GetAsync(f.Tenant,b.BatchId,CancellationToken.None)).Value!.RecordCount);
    }

    [Fact]
    public async Task Logical_dataset_grows_only_on_seal_and_reopens_identically_after_physical_reorder()
    {
        var f=await NewAsync();var a=await f.OpenAsync();var b=await f.OpenAsync(2);await Append(f,a,"one","17");await Append(f,b,"two","18");await f.SealAsync(a);
        await using var db=f.Db();var catalogue=new AcceptedDatasetCatalogue(db);
        var relations=await catalogue.RelationsAsync(f.Tenant,f.SourceDataset,10,0,CancellationToken.None);Assert.True(relations.IsAccepted,relations.Refusal?.Detail);
        var relation=relations.Value[0].GetProperty("relation_name").GetString()!;
        Assert.Matches("^accepted_dataset_[a-f0-9]{40}$",relation);
        await using var c=await Connection(f);
        async Task<string> Read(){await using var q=new NpgsqlCommand("SELECT jsonb_agg(to_jsonb(v) ORDER BY source_record_id)::text FROM ppiq_staging."+relation+" v",c);return (string)(await q.ExecuteScalarAsync())!;}
        using(var one=JsonDocument.Parse(await Read()))Assert.Equal(1,one.RootElement.GetArrayLength());
        await f.SealAsync(b);string before=await Read();
        await using(var reorder=new NpgsqlCommand("CLUSTER ppiq_staging.accepted_records USING ix_accepted_records_batch",c))await reorder.ExecuteNonQueryAsync();
        Assert.Equal(before,await Read());using var two=JsonDocument.Parse(before);Assert.Equal(2,two.RootElement.GetArrayLength());
        var foreign=await catalogue.RelationsAsync(Guid.NewGuid(),f.SourceDataset,10,0,CancellationToken.None);Assert.Empty(foreign.Value.EnumerateArray());
    }

    [Fact]
    public async Task Exact_original_bytes_are_immutable_separate_artifacts_with_content_aware_receipts()
    {
        var f=await NewAsync();var b=await f.OpenAsync();byte[] bytes=Encoding.UTF8.GetBytes(" {\"a\": 1}\r\n");var hash=Convert.ToHexString(SHA256.HashData(bytes));var id=Guid.NewGuid();
        await using var db=f.Db();var store=f.Store(db);var artifact=await store.PutAsync(f.Tenant,id,bytes,"application/json",hash,CancellationToken.None);
        Assert.True(artifact.IsAccepted,artifact.Refusal?.Detail);Assert.Equal(artifact.Value,(await store.PutAsync(f.Tenant,id,bytes,"application/json",hash,CancellationToken.None)).Value);
        byte[] different=Encoding.UTF8.GetBytes("{\"a\":1}");var conflict=await store.PutAsync(f.Tenant,id,different,"application/json",Convert.ToHexString(SHA256.HashData(different)),CancellationToken.None);Assert.Equal("AR07",conflict.Refusal?.Code);
        var envelope=JsonNode.Parse(Envelope(f,"17").GetRawText())!;envelope["originalBytes"]=JsonSerializer.SerializeToNode(new{artifactId=id,sha256=artifact.Value!.Sha256,length=bytes.Length});
        using var document=JsonDocument.Parse(envelope.ToJsonString());var accepted=await store.AcceptAsync(new(f.Tenant,b.BatchId,f.Session,1,"one",document.RootElement),CancellationToken.None);Assert.True(accepted.IsAccepted,accepted.Refusal?.Detail);await f.SealAsync(b);
        await using var c=await Connection(f);await using var q=new NpgsqlCommand("SELECT content FROM ppiq_staging.accepted_source_artifacts WHERE tenant_id=@t AND artifact_id=@id",c);q.Parameters.AddWithValue("t",f.Tenant);q.Parameters.AddWithValue("id",id);Assert.Equal(bytes,(byte[])(await q.ExecuteScalarAsync())!);
        var detail=await new AcceptedDatasetCatalogue(db).RecordAsync(f.Tenant,accepted.Value!.ReceiptId,CancellationToken.None);Assert.True(detail.IsAccepted,detail.Refusal?.Detail);Assert.Equal(id,detail.Value.GetProperty("envelope").GetProperty("originalBytes").GetProperty("artifactId").GetGuid());
    }

    [Fact]
    public async Task Full_record_detail_pins_configuration_policy_fields_and_rejects_false_consistency()
    {
        var f=await NewAsync();var b=await f.OpenAsync();await using var db=f.Db();var node=JsonNode.Parse(Envelope(f,"17").GetRawText())!;
        node["sourceEpoch"]="epoch-a";node["sourceRevision"]="revision-a";node["sourceTimestampUtc"]="2026-09-21T23:59:59Z";
        node["fieldMetadata"]=JsonNode.Parse("{\""+f.Fields["reading"]+"\":{\"rawQuality\":\"Good\",\"sourceTimestampUtc\":\"2026-09-21T23:59:59Z\"}}");
        using var doc=JsonDocument.Parse(node.ToJsonString());var r=await f.Store(db).AcceptAsync(new(f.Tenant,b.BatchId,f.Session,1,"one",doc.RootElement),CancellationToken.None);Assert.True(r.IsAccepted,r.Refusal?.Detail);
        node["consistency"]="SourceLatchedRecord";using var bad=JsonDocument.Parse(node.ToJsonString());Assert.Equal("AR11",(await f.Store(db).AcceptAsync(new(f.Tenant,b.BatchId,f.Session,1,"two",bad.RootElement),CancellationToken.None)).Refusal?.Code);
        await f.SealAsync(b);var detail=await new AcceptedDatasetCatalogue(db).RecordAsync(f.Tenant,r.Value!.ReceiptId,CancellationToken.None);Assert.True(detail.IsAccepted,detail.Refusal?.Detail);
        Assert.Equal(f.Configuration,detail.Value.GetProperty("configurationId").GetGuid());Assert.Equal(1,detail.Value.GetProperty("configurationVersion").GetInt32());
        Assert.Equal("PERIODIC",detail.Value.GetProperty("configuration").GetProperty("recordingGroups")[0].GetProperty("policy").GetProperty("mode").GetString());
        Assert.Equal("epoch-a",detail.Value.GetProperty("envelope").GetProperty("sourceEpoch").GetString());
    }

    [Fact]
    public async Task Gap_browsing_is_tenant_scoped_and_does_not_publish_a_false_complete_batch()
    {
        var f=await NewAsync();var b=await f.OpenAsync();await using var db=f.Db();var store=f.Store(db);var id=Guid.NewGuid();var request=new AcceptedGapRequest(f.Tenant,b.BatchId,f.Session,1,id,"source_outage","a","b",null);
        Assert.True((await store.RecordGapAsync(request,CancellationToken.None)).IsAccepted);Assert.Equal(id,(await store.RecordGapAsync(request,CancellationToken.None)).Value);
        Assert.Equal("AR07",(await store.RecordGapAsync(request with{Reason="different"},CancellationToken.None)).Refusal?.Code);
        Assert.Equal("AR16",(await store.SealAsync(new(f.Tenant,b.BatchId,f.Session,1,0,0,null),CancellationToken.None)).Refusal?.Code);
        var catalogue=new AcceptedDatasetCatalogue(db);var gaps=await catalogue.GapsAsync(f.Tenant,f.SourceDataset,10,0,CancellationToken.None);Assert.Single(gaps.Value.EnumerateArray());
        Assert.Empty((await catalogue.GapsAsync(Guid.NewGuid(),f.SourceDataset,10,0,CancellationToken.None)).Value.EnumerateArray());
        Assert.Equal(0,(await store.GetAsync(f.Tenant,b.BatchId,CancellationToken.None)).Value!.CommittedOrdinal);
        Assert.Equal("AR11",(await catalogue.BatchesAsync(f.Tenant,f.SourceDataset,101,0,CancellationToken.None)).Refusal?.Code);
    }

    [Fact]
    public async Task Connection_loss_before_commit_rolls_back_payload_receipt_and_batch_count()
    {
        var f=await NewAsync();var b=await f.OpenAsync();
        await using(var c=await Connection(f)){await using var tx=await c.BeginTransactionAsync();await using var q=AppendCommand(f,b,c,tx);await q.ExecuteScalarAsync();await c.CloseAsync();}
        await using var db=f.Db();Assert.Equal(0,(await f.Store(db).GetAsync(f.Tenant,b.BatchId,CancellationToken.None)).Value!.RecordCount);
        var receipt=await Append(f,b,"crash","17");Assert.Equal(receipt,await Append(f,b,"crash","17"));Assert.Equal(1,(await f.SealAsync(b)).RecordCount);
    }

    [Fact]
    public async Task Connection_cut_after_forwarding_commit_converges_by_identity()
    {
        var f=await NewAsync();var b=await f.OpenAsync();await using var db=f.Db();
        var settings=new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        await using var proxy=new AcceptedCommitDisconnectProxy(settings.Host!,settings.Port);
        settings.Host="127.0.0.1";settings.Port=proxy.Port;settings.SslMode=SslMode.Disable;settings.Pooling=false;settings.Timeout=10;settings.CommandTimeout=10;
        await using(var c=new NpgsqlConnection(settings.ConnectionString))
        {
            await c.OpenAsync();await using(var scope=new NpgsqlCommand("SELECT set_config('app.current_tenant',@t,false)",c))
            {scope.Parameters.AddWithValue("t",f.Tenant.ToString());await scope.ExecuteScalarAsync();}
            await using var tx=await c.BeginTransactionAsync();await using var q=AppendCommand(f,b,c,tx);await q.ExecuteScalarAsync();
            await Assert.ThrowsAnyAsync<Exception>(()=>tx.CommitAsync());
        }
        Assert.True(proxy.CommitForwarded);
        var receipt=await Append(f,b,"crash","17");Assert.Equal(receipt,await Append(f,b,"crash","17"));Assert.Equal(1,(await f.SealAsync(b)).RecordCount);
    }

    [Fact]
    public async Task Commit_before_response_loss_returns_the_original_receipt_on_reconnect()
    {
        var f=await NewAsync();var b=await f.OpenAsync();Guid receipt;
        await using(var c=await Connection(f))
        {await using var tx=await c.BeginTransactionAsync();await using var q=AppendCommand(f,b,c,tx);using var doc=JsonDocument.Parse((string)(await q.ExecuteScalarAsync())!);receipt=doc.RootElement.GetProperty("receipt_id").GetGuid();await tx.CommitAsync();}
        Assert.Equal(receipt,(await Append(f,b,"crash","17")).ReceiptId);Assert.Equal(1,(await f.SealAsync(b)).RecordCount);
    }

    [Fact]
    public async Task Failed_seal_transaction_rolls_back_view_state_and_checkpoint_together()
    {
        var f=await NewAsync();var b=await f.OpenAsync();await Append(f,b,"one","17");await using var db=f.Db();var current=(await f.Store(db).GetAsync(f.Tenant,b.BatchId,CancellationToken.None)).Value!;
        await using(var c=await Connection(f))
        {
            await using var tx=await c.BeginTransactionAsync();await using var q=new NpgsqlCommand("SELECT ppiq_staging.accepted_seal(@t,@b,@s,1,@count,@bytes,null)",c,tx);
            q.Parameters.AddWithValue("t",f.Tenant);q.Parameters.AddWithValue("b",b.BatchId);q.Parameters.AddWithValue("s",f.Session);q.Parameters.AddWithValue("count",current.RecordCount);q.Parameters.AddWithValue("bytes",current.PayloadBytes);await q.ExecuteScalarAsync();await c.CloseAsync();
        }
        var after=(await f.Store(db).GetAsync(f.Tenant,b.BatchId,CancellationToken.None)).Value!;Assert.Equal("Open",after.State);Assert.Null(after.RelationName);Assert.Equal(0,after.CommittedOrdinal);
        await using(var c=await Connection(f)){await using var q=new NpgsqlCommand("SELECT to_regclass(@name)::text",c);q.Parameters.AddWithValue("name","ppiq_staging.accepted_batch_"+b.BatchId.ToString("N"));Assert.Equal(DBNull.Value,await q.ExecuteScalarAsync());}
        Assert.Equal(1,(await f.SealAsync(b)).CommittedOrdinal);
    }

    [Fact]
    public async Task Record_identity_is_independent_of_batch_placement()
    {
        var f=await NewAsync();var a=await f.OpenAsync();var b=await f.OpenAsync(2);var first=await Append(f,a,"same","17");var retry=await Append(f,b,"same","17");Assert.Equal(first,retry);
        Assert.Equal(1,(await f.SealAsync(a)).RecordCount);Assert.Equal(0,(await f.SealAsync(b)).RecordCount);
    }
    [Fact]
    public async Task Preservation_producer_receipt_is_required_and_survives_retry_without_reauthorizing()
    {
        var f=new AcceptedRecordFixture(fixture);await f.InitializeAsync(new Dictionary<string,string>{{"reading","int64"}},true);
        var b=await f.OpenAsync();await using var db=f.Db();byte[] bytes=Encoding.UTF8.GetBytes("original source bytes");var id=Guid.NewGuid();
        var artifact=await f.Store(db).PutAsync(f.Tenant,id,bytes,"application/octet-stream",Convert.ToHexString(SHA256.HashData(bytes)),CancellationToken.None);Assert.True(artifact.IsAccepted);
        var node=JsonNode.Parse(Envelope(f,"17").GetRawText())!;node["originalBytes"]=JsonSerializer.SerializeToNode(new{artifactId=id,sha256=artifact.Value!.Sha256,length=bytes.Length});
        using var doc=JsonDocument.Parse(node.ToJsonString());var request=new AcceptedRecordRequest(f.Tenant,b.BatchId,f.Session,1,"preserved",doc.RootElement);
        var unproven=new AcceptedRecordStore(db,new AcceptedRecordFixture.TransactionalFenceFixture(db),new PreservationProducer(db,false));
        Assert.Equal("AR14",(await unproven.AcceptAsync(request,CancellationToken.None)).Refusal?.Code);
        var approved=new AcceptedRecordStore(db,new AcceptedRecordFixture.TransactionalFenceFixture(db),new PreservationProducer(db,true));
        var result=await approved.AcceptAsync(request,CancellationToken.None);Assert.True(result.IsAccepted,result.Refusal?.Detail);
        // Acceptance has committed. Retrying the same immutable occurrence must not need a new policy decision.
        var retry=await f.Store(db).AcceptAsync(request,CancellationToken.None);Assert.True(retry.IsAccepted,retry.Refusal?.Detail);Assert.Equal(result.Value,retry.Value);
        await f.SealAsync(b);var catalogue=new AcceptedDatasetCatalogue(db);
        var detail=await catalogue.RecordAsync(f.Tenant,result.Value!.ReceiptId,CancellationToken.None);
        Assert.Equal("fixture-policy-v1",detail.Value.GetProperty("originalBytesAdmission").GetProperty("policy_version").GetString());
        await using(var c=await Connection(f))
        {
            await using var q=new NpgsqlCommand("INSERT INTO ppiq_staging.accepted_artifact_admissions(tenant_id,proof_id,batch_id,record_id,artifact_id,policy_reference,policy_version,retention_until_utc) VALUES(@t,@p,@b,'preserved',@a,'required-original-bytes','newer-policy',now()+interval '2 hours')",c);
            q.Parameters.AddWithValue("t",f.Tenant);q.Parameters.AddWithValue("p",Guid.NewGuid());q.Parameters.AddWithValue("b",b.BatchId);q.Parameters.AddWithValue("a",id);await q.ExecuteNonQueryAsync();
        }
        Assert.Equal(detail.Value.GetRawText(),(await catalogue.RecordAsync(f.Tenant,result.Value.ReceiptId,CancellationToken.None)).Value.GetRawText());

    }

    private sealed class PreservationProducer(PlantProcess.Infrastructure.Persistence.PlantProcessDbContext db,bool install) : IOriginalBytesPreservationAuthority
    {
        public async Task<AcquisitionRefusal?> ValidateAsync(AcceptedOriginalBytesRequirement r,CancellationToken ct)
        {
            Assert.Equal("required-original-bytes",r.PolicyReference);Assert.Equal(1,r.ConfigurationVersion);
            if(install)
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ppiq_staging.accepted_artifact_admissions(tenant_id,proof_id,batch_id,record_id,artifact_id,policy_reference,policy_version,retention_until_utc) VALUES({r.TenantId},{Guid.NewGuid()},{r.BatchId},{r.RecordId},{r.Envelope.GetProperty("originalBytes").GetProperty("artifactId").GetGuid()},{r.PolicyReference},{"fixture-policy-v1"},now()+interval '1 hour')",ct);
            return null;
        }
    }

}
