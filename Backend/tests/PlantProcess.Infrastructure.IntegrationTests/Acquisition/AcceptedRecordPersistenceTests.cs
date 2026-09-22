using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Infrastructure.Integration.Acquisition;
using PlantProcess.Infrastructure.IntegrationTests.Definitions;
using PlantProcess.Infrastructure.Jobs.Transformations;

namespace PlantProcess.Infrastructure.IntegrationTests.Acquisition;

[Collection("CanonicalDefinitionStore")]
public sealed class AcceptedRecordPersistenceTests(DefinitionStoreFixture fixture)
{
    private async Task<AcceptedRecordFixture> NewAsync(bool retention=false)
    {
        var f=new AcceptedRecordFixture(fixture);
        await f.InitializeAsync(new Dictionary<string,string>{{"reading","int64"}},retention);return f;
    }
    private static Dictionary<string,object?> Value(long n=17)=>new(){{"reading",n}};

    [Fact]
    public async Task Lost_response_retry_returns_original_receipt_without_double_counting()
    {
        var f=await NewAsync();var batch=await f.OpenAsync();
        var first=await f.AppendAsync(batch,"occurrence-1",Value());
        var retry=await f.AppendAsync(batch,"occurrence-1",Value());
        Assert.True(first.IsAccepted,first.Refusal?.Detail);Assert.True(retry.IsAccepted,retry.Refusal?.Detail);
        Assert.Equal(first.Value,retry.Value);
        var sealedBatch=await f.SealAsync(batch);Assert.Equal(1,sealedBatch.RecordCount);
        var sealedRetry=await f.AppendAsync(batch,"occurrence-1",Value());Assert.Equal(first.Value,sealedRetry.Value);
    }

    [Fact]
    public async Task Conflicting_identity_preserves_record_and_commits_incident()
    {
        var f=await NewAsync();var b=await f.OpenAsync();
        var first=await f.AppendAsync(b,"occurrence",Value());Assert.True(first.IsAccepted,first.Refusal?.Detail);
        var conflict=await f.AppendAsync(b,"occurrence",Value(18));Assert.Equal("AR07",conflict.Refusal?.Code);
        await using var db=f.Db();
        await db.Database.OpenConnectionAsync();
        await using var query=new NpgsqlCommand("SELECT count(*) FROM ppiq_staging.accepted_integrity_conflicts WHERE tenant_id=@t AND receipt_id=@r",(NpgsqlConnection)db.Database.GetDbConnection());
        query.Parameters.AddWithValue("t",f.Tenant);query.Parameters.AddWithValue("r",first.Value!.ReceiptId);
        Assert.Equal(1L,await query.ExecuteScalarAsync());
        var retry=await f.AppendAsync(b,"occurrence",Value());Assert.Equal(first.Value,retry.Value);
    }

    [Fact]
    public async Task Equal_values_and_timestamps_with_distinct_occurrences_are_distinct_records()
    {
        var f=await NewAsync();var b=await f.OpenAsync();
        var a=await f.AppendAsync(b,"a",Value());var c=await f.AppendAsync(b,"b",Value());
        Assert.True(a.IsAccepted,a.Refusal?.Detail);Assert.True(c.IsAccepted,c.Refusal?.Detail);
        Assert.NotEqual(a.Value!.ReceiptId,c.Value!.ReceiptId);Assert.Equal(2,(await f.SealAsync(b)).RecordCount);
    }

    [Fact]
    public async Task Open_batch_is_invisible_and_exact_sealed_relation_preserves_bigint_boundaries()
    {
        var f=await NewAsync();var a=await f.OpenAsync();var b=await f.OpenAsync(2,"a-end");
        Assert.True((await f.AppendAsync(a,"min",Value(long.MinValue))).IsAccepted);
        Assert.True((await f.AppendAsync(a,"max",Value(long.MaxValue))).IsAccepted);
        Assert.True((await f.AppendAsync(b,"foreign-batch",Value(100))).IsAccepted);
        await using var db=f.Db();
        var before=await f.Store(db).GetAsync(f.Tenant,a.BatchId,CancellationToken.None);Assert.Null(before.Value!.RelationName);
        var ready=await f.SealAsync(a,"a-end");await f.SealAsync(b,"b-end");
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.current_tenant',{f.Tenant.ToString()},false)");
        var reader=new NpgsqlTransformationSourceReader(db);
        var rows=await reader.ReadAsync("SELECT reading FROM ppiq_staging."+ready.RelationName+" ORDER BY reading",Array.Empty<object>(),1,CancellationToken.None);
        Assert.Equal(2,rows.Count);Assert.Equal(long.MinValue,rows[0][0]);Assert.Equal(long.MaxValue,rows[1][0]);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.current_tenant',{Guid.NewGuid().ToString()},false)");
        Assert.Equal(0,await reader.CountRowsAsync("ppiq_staging",ready.RelationName!,CancellationToken.None));
    }

    [Fact]
    public async Task Seal_checks_summary_retries_and_refuses_new_records()
    {
        var f=await NewAsync();var b=await f.OpenAsync();Assert.True((await f.AppendAsync(b,"one",Value())).IsAccepted);
        await using var db=f.Db();var store=f.Store(db);
        var wrong=await store.SealAsync(new(f.Tenant,b.BatchId,f.Session,1,2,0,null),CancellationToken.None);
        Assert.Equal("AR09",wrong.Refusal?.Code);
        var ready=await f.SealAsync(b,"end");
        var retry=await store.SealAsync(new(f.Tenant,b.BatchId,f.Session,1,ready.RecordCount,ready.PayloadBytes,"end"),CancellationToken.None);
        Assert.True(retry.IsAccepted,retry.Refusal?.Detail);
        var conflict=await store.SealAsync(new(f.Tenant,b.BatchId,f.Session,1,ready.RecordCount,ready.PayloadBytes,"different"),CancellationToken.None);
        Assert.Equal("AR08",conflict.Refusal?.Code);
        Assert.Equal("AR05",(await f.AppendAsync(b,"two",Value())).Refusal?.Code);
    }

    [Fact]
    public async Task Closing_checkpoint_gap_advances_through_already_sealed_successors_once()
    {
        var f=await NewAsync();var a=await f.OpenAsync();var b=await f.OpenAsync(2,"one");
        var c=await f.OpenAsync(3,"two");var d=await f.OpenAsync(4,"three");
        Assert.Equal(1,(await f.SealAsync(a,"one")).CommittedOrdinal);
        Assert.Equal(2,(await f.SealAsync(b,"two")).CommittedOrdinal);
        Assert.Equal(2,(await f.SealAsync(d,"four")).CommittedOrdinal);
        Assert.Equal(4,(await f.SealAsync(c,"three")).CommittedOrdinal);
        Assert.Equal(4,(await f.SealAsync(c,"three")).CommittedOrdinal);
    }

    [Fact]
    public async Task Missing_fencing_authority_fails_closed_before_batch_creation()
    {
        var f=await NewAsync();await using var db=f.Db();
        var store=new AcceptedRecordStore(db,new UnavailableSessionFencingAuthority(),new UnavailableOriginalBytesPreservationAuthority());
        var request=f.Request();var result=await store.OpenAsync(request,CancellationToken.None);
        Assert.Equal("AR13",result.Refusal?.Code);
        Assert.Equal("AR02",(await store.GetAsync(f.Tenant,request.BatchId,CancellationToken.None)).Refusal?.Code);
    }

    [Fact]
    public async Task Referenced_retention_policy_without_authority_refuses_even_with_artifact_metadata()
    {
        var f=await NewAsync(true);var b=await f.OpenAsync();await using var db=f.Db();
        var envelope=System.Text.Json.Nodes.JsonNode.Parse(f.Envelope(Value()).GetRawText())!;
        envelope["originalBytes"]=new System.Text.Json.Nodes.JsonObject {
            ["uri"]="immutable://fixture/record",["sha256"]=new string('a',64),["length"]=32,["retentionUntilUtc"]="2030-01-01T00:00:00Z"};
        using var doc=System.Text.Json.JsonDocument.Parse(envelope.ToJsonString());
        var result=await f.Store(db).AcceptAsync(new(f.Tenant,b.BatchId,f.Session,1,"one",doc.RootElement),CancellationToken.None);
        Assert.Equal("AR14",result.Refusal?.Code);
        Assert.Equal(0,(await f.Store(db).GetAsync(f.Tenant,b.BatchId,CancellationToken.None)).Value!.RecordCount);
    }

    [Fact]
    public async Task Committed_takeover_refuses_stale_generation_without_acceptance()
    {
        var f=await NewAsync();var b=await f.OpenAsync();
        await using(var db=f.Db())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ppiq_staging.accepted_capture_fences SET generation=2,session_id={Guid.NewGuid()} WHERE tenant_id={f.Tenant} AND dataset_governance_id={f.Dataset} AND stream_key={f.Stream}");
        }
        Assert.Equal("AR04",(await f.AppendAsync(b,"stale",Value())).Refusal?.Code);
    }

    [Fact]
    public async Task Concurrent_duplicate_acceptance_has_one_durable_receipt()
    {
        var f=await NewAsync();var b=await f.OpenAsync();
        var results=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>f.AppendAsync(b,"same",Value())));
        Assert.All(results,r=>Assert.True(r.IsAccepted,r.Refusal?.Detail));
        Assert.Single(results.Select(r=>r.Value!.ReceiptId).Distinct());
        Assert.Equal(1,(await f.SealAsync(b)).RecordCount);
    }
    [Fact]
    public async Task Takeover_waits_for_acceptance_lock_then_fences_the_previous_generation()
    {
        var f=await NewAsync();var b=await f.OpenAsync();
        var entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var acceptance=f.Db();
        var authority=new PausedAuthority(acceptance,entered,release);
        var store=new AcceptedRecordStore(acceptance,authority,new UnavailableOriginalBytesPreservationAuthority());
        var accepting=store.AcceptAsync(new(f.Tenant,b.BatchId,f.Session,1,"before-takeover",f.Envelope(Value())),CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await using var takeover=f.Db();await takeover.Database.OpenConnectionAsync();
        var connection=(NpgsqlConnection)takeover.Database.GetDbConnection();
        await using var change=new NpgsqlCommand("UPDATE ppiq_staging.accepted_capture_fences SET generation=2,session_id=@s WHERE tenant_id=@t AND dataset_governance_id=@d AND stream_key=@k",connection);
        change.Parameters.AddWithValue("s",Guid.NewGuid());change.Parameters.AddWithValue("t",f.Tenant);
        change.Parameters.AddWithValue("d",f.Dataset);change.Parameters.AddWithValue("k",f.Stream);
        var taking=change.ExecuteNonQueryAsync();
        bool blocked=false;
        try
        {
            await using var observer=f.Db();await observer.Database.OpenConnectionAsync();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while(!blocked)
            {
                await using var query=new NpgsqlCommand("SELECT cardinality(pg_blocking_pids(@pid))>0",(NpgsqlConnection)observer.Database.GetDbConnection());
                query.Parameters.AddWithValue("pid",connection.ProcessID);
                blocked=(bool)(await query.ExecuteScalarAsync(timeout.Token))!;
                if(!blocked)await Task.Delay(10,timeout.Token);
            }
        }
        finally { release.TrySetResult(true); }
        Assert.True(blocked);Assert.True((await accepting).IsAccepted);Assert.Equal(1,await taking);
        Assert.Equal("AR04",(await f.AppendAsync(b,"after-takeover",Value())).Refusal?.Code);
    }

    private sealed class PausedAuthority(PlantProcess.Infrastructure.Persistence.PlantProcessDbContext db,
        TaskCompletionSource<bool> entered,TaskCompletionSource<bool> release) : ISessionFencingAuthority
    {
        public async Task<AcquisitionRefusal?> ValidateAsync(Guid tenantId,Guid datasetGovernanceId,string streamKey,
            Guid sessionId,long generation,CancellationToken ct)
        {
            await new AcceptedRecordFixture.TransactionalFenceFixture(db).ValidateAsync(tenantId,datasetGovernanceId,streamKey,sessionId,generation,ct);
            entered.TrySetResult(true);await release.Task.WaitAsync(TimeSpan.FromSeconds(30),ct);return null;
        }
    }

    [Fact]
    public async Task Runtime_role_cannot_write_fences_or_read_raw_unsealed_payloads()
    {
        var f=await NewAsync();var b=await f.OpenAsync();Assert.True((await f.AppendAsync(b,"record",Value())).IsAccepted);
        var ready=await f.SealAsync(b);
        await using var db=f.Db();await db.Database.OpenConnectionAsync();
        await using var connectionScope=db.Database.GetDbConnection().CreateCommand();
        connectionScope.CommandText="SET ROLE plantprocess_app";await connectionScope.ExecuteNonQueryAsync();
        try
        {
            await using var query=new NpgsqlCommand("SELECT has_table_privilege(current_user,'ppiq_staging.accepted_capture_fences','UPDATE') OR has_table_privilege(current_user,'ppiq_staging.accepted_records','SELECT')",(NpgsqlConnection)db.Database.GetDbConnection());
            Assert.False((bool)(await query.ExecuteScalarAsync())!);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.current_tenant',{Guid.NewGuid().ToString()},false)");
            var reader=new NpgsqlTransformationSourceReader(db);
            Assert.Equal(0,await reader.CountRowsAsync("ppiq_staging",ready.RelationName!,CancellationToken.None));
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.current_tenant',{f.Tenant.ToString()},false)");
            Assert.Equal(1,await reader.CountRowsAsync("ppiq_staging",ready.RelationName!,CancellationToken.None));
        }
        finally { connectionScope.CommandText="RESET ROLE";await connectionScope.ExecuteNonQueryAsync(); }
    }

    [Fact]
    public async Task Preview_requires_exact_sealed_batch_and_keeps_field_identity()
    {
        var f=await NewAsync();var b=await f.OpenAsync();await f.AppendAsync(b,"one",Value());
        await using var db=f.Db();var preview=new AcceptedDatasetReader(db);
        Assert.Equal("AR02",(await preview.PreviewAsync(f.Tenant,f.SourceDataset,b.BatchId,25,CancellationToken.None)).Refusal?.Code);
        await f.SealAsync(b);
        var result=await preview.PreviewAsync(f.Tenant,f.SourceDataset,b.BatchId,25,CancellationToken.None);
        Assert.True(result.IsAccepted,result.Refusal?.Detail);Assert.Single(result.Value!.Records);
        Assert.Equal(f.Fields["reading"],result.Value.Fields[0].GetProperty("fieldId").GetGuid());
        Assert.Equal("AR02",(await preview.PreviewAsync(Guid.NewGuid(),f.SourceDataset,b.BatchId,25,CancellationToken.None)).Refusal?.Code);
        Assert.Equal("AR11",(await preview.PreviewAsync(f.Tenant,f.SourceDataset,b.BatchId,101,CancellationToken.None)).Refusal?.Code);
    }

    [Fact]
    public async Task Concurrent_append_and_seal_never_publish_an_incorrect_summary()
    {
        var f=await NewAsync();var b=await f.OpenAsync();
        async Task<AcquisitionOutcome<AcceptedBatchView>> SealEmpty()
        {
            await using var db=f.Db();return await f.Store(db).SealAsync(new(f.Tenant,b.BatchId,f.Session,1,0,0,null),CancellationToken.None);
        }
        var append=f.AppendAsync(b,"racing",Value());var seal=SealEmpty();await Task.WhenAll(append,seal);
        if(seal.Result.IsAccepted)
        {
            Assert.Equal("AR05",append.Result.Refusal?.Code);Assert.Equal(0,seal.Result.Value!.RecordCount);
        }
        else
        {
            Assert.Equal("AR09",seal.Result.Refusal?.Code);Assert.True(append.Result.IsAccepted);
            Assert.Equal(1,(await f.SealAsync(b)).RecordCount);
        }
    }
}
