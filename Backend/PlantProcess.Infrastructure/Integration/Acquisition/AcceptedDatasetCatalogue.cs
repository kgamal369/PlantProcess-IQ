using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Integration.Acquisition;

/// <summary>Tenant-bound metadata browsing. Pagination never returns authoritative payloads implicitly.</summary>
public sealed class AcceptedDatasetCatalogue(PlantProcessDbContext db)
{
    public Task<AcquisitionOutcome<JsonElement>> BatchesAsync(Guid tenant, Guid dataset, int take, int skip, CancellationToken ct) =>
        ReadAsync(tenant,take,skip,"SELECT coalesce(jsonb_agg(to_jsonb(x)),'[]'::jsonb)::text FROM ("+
            "SELECT b.batch_id,b.session_id,b.batch_ordinal,b.configuration_id,b.configuration_version,b.configuration_hash,"+
            "b.state,b.record_count,b.payload_bytes,b.opened_at_utc,b.sealed_at_utc,b.relation_name,"+
            "(SELECT count(*) FROM ppiq_staging.accepted_gaps a WHERE a.tenant_id=b.tenant_id AND a.batch_id=b.batch_id) AS gap_count "+
            "FROM ppiq_staging.accepted_batches b JOIN ppiq_meta.source_dataset_governance g ON g.tenant_id=b.tenant_id AND g.id=b.dataset_governance_id "+
            "WHERE b.tenant_id=@t AND g.source_dataset_definition_id=@id ORDER BY b.opened_at_utc,b.batch_id LIMIT @take OFFSET @skip) x",dataset,ct);

    public Task<AcquisitionOutcome<JsonElement>> BatchAsync(Guid tenant, Guid batch, CancellationToken ct) =>
        ReadAsync(tenant,1,0,"SELECT jsonb_build_object('batchId',b.batch_id,'sessionId',b.session_id,'generation',b.generation,"+
            "'datasetGovernanceId',b.dataset_governance_id,'configurationId',b.configuration_id,'configurationVersion',b.configuration_version,"+
            "'configurationHash',b.configuration_hash,'recordingGroupKey',b.recording_group_key,'fields',b.field_shape,'state',b.state,"+
            "'relationName',b.relation_name,'recordCount',b.record_count,'payloadBytes',b.payload_bytes,"+
            "'startPosition',b.start_position,'endPosition',b.end_position,'openedAtUtc',b.opened_at_utc,'sealedAtUtc',b.sealed_at_utc)::text "+
            "FROM ppiq_staging.accepted_batches b WHERE b.tenant_id=@t AND b.batch_id=@id",batch,ct);

    public Task<AcquisitionOutcome<JsonElement>> GapsAsync(Guid tenant, Guid dataset, int take, int skip, CancellationToken ct) =>
        ReadAsync(tenant,take,skip,"SELECT coalesce(jsonb_agg(to_jsonb(x)),'[]'::jsonb)::text FROM ("+
            "SELECT a.gap_id,a.batch_id,a.reason,a.from_position,a.to_position,a.missing_count,a.observed_at_utc "+
            "FROM ppiq_staging.accepted_gaps a JOIN ppiq_staging.accepted_batches b ON b.tenant_id=a.tenant_id AND b.batch_id=a.batch_id "+
            "JOIN ppiq_meta.source_dataset_governance g ON g.tenant_id=b.tenant_id AND g.id=b.dataset_governance_id "+
            "WHERE a.tenant_id=@t AND g.source_dataset_definition_id=@id ORDER BY a.observed_at_utc,a.gap_id LIMIT @take OFFSET @skip) x",dataset,ct);

    public Task<AcquisitionOutcome<JsonElement>> RecordAsync(Guid tenant, Guid receipt, CancellationToken ct) =>
        ReadAsync(tenant,1,0,"SELECT ppiq_staging.accepted_record_detail(@t,@id)::text",receipt,ct);

    public Task<AcquisitionOutcome<JsonElement>> RelationsAsync(Guid tenant, Guid dataset, int take, int skip, CancellationToken ct) =>
        ReadAsync(tenant,take,skip,"SELECT coalesce(jsonb_agg(to_jsonb(x)),'[]'::jsonb)::text FROM ("+
            "SELECT DISTINCT b.configuration_id,b.configuration_version,b.recording_group_key,b.field_shape,"+
            "'ppiq_staging'::text AS schema_name,'accepted_dataset_'||substr(encode(public.digest(convert_to(b.tenant_id::text||'/'||b.dataset_governance_id::text||'/'||b.configuration_version_id::text||'/'||b.recording_group_key,'UTF8'),'sha256'),'hex'),1,40) AS relation_name "+
            "FROM ppiq_staging.accepted_batches b JOIN ppiq_meta.source_dataset_governance g ON g.tenant_id=b.tenant_id AND g.id=b.dataset_governance_id "+
            "WHERE b.tenant_id=@t AND g.source_dataset_definition_id=@id AND b.state='Sealed' "+
            "ORDER BY b.configuration_id,b.configuration_version,b.recording_group_key,b.field_shape LIMIT @take OFFSET @skip) x",dataset,ct);

    private async Task<AcquisitionOutcome<JsonElement>> ReadAsync(Guid tenant,int take,int skip,string sql,Guid id,CancellationToken ct)
    {
        if(tenant==Guid.Empty) return AcquisitionOutcome<JsonElement>.Refuse("AR01","A resolved tenant is required.");
        if(id==Guid.Empty || take<1 || take>100 || skip<0 || skip>100000)
            return AcquisitionOutcome<JsonElement>.Refuse("AR11","An exact identity and bounded page are required.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var connection=(NpgsqlConnection)db.Database.GetDbConnection();var native=(NpgsqlTransaction)tx.GetDbTransaction();
        await using(var scope=new NpgsqlCommand("SELECT set_config('app.current_tenant',@tenant,true)",connection,native))
        {scope.Parameters.AddWithValue("tenant",tenant.ToString("D"));await scope.ExecuteScalarAsync(ct);}
        await using var command=new NpgsqlCommand(sql,connection,native);
        command.Parameters.AddWithValue("t",tenant);command.Parameters.AddWithValue("id",id);
        command.Parameters.AddWithValue("take",take);command.Parameters.AddWithValue("skip",skip);
        var raw=await command.ExecuteScalarAsync(ct);
        if(raw is not string text) return AcquisitionOutcome<JsonElement>.Refuse("AR02","Accepted evidence was not found.");
        using var doc=JsonDocument.Parse(text);var value=doc.RootElement.Clone();
        await tx.CommitAsync(ct);return AcquisitionOutcome<JsonElement>.Accept(value);
    }
}
