using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Integration.Acquisition;

public sealed record AcceptedDatasetPreview(Guid DatasetId, Guid BatchId, Guid ConfigurationId,
    int ConfigurationVersion, JsonElement Fields, IReadOnlyList<IReadOnlyDictionary<string,object?>> Records);

/// <summary>Bounded preview of the same typed, exact-batch relation used by Transformation.</summary>
public sealed class AcceptedDatasetReader(PlantProcessDbContext db)
{
    public async Task<AcquisitionOutcome<AcceptedDatasetPreview>> PreviewAsync(Guid tenantId, Guid datasetId,
        Guid batchId, int take, CancellationToken ct)
    {
        if (tenantId==Guid.Empty || batchId==Guid.Empty || take<1 || take>100)
            return AcquisitionOutcome<AcceptedDatasetPreview>.Refuse("AR11","An exact batch and a preview limit between 1 and 100 are required.");
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        var connection=(NpgsqlConnection)db.Database.GetDbConnection();
        var native=(NpgsqlTransaction)transaction.GetDbTransaction();
        await using(var scope=new NpgsqlCommand("SELECT set_config('app.current_tenant',$1,true)",connection,native))
        {
            scope.Parameters.AddWithValue(tenantId.ToString("D"));await scope.ExecuteScalarAsync(ct);
        }
        string relation;Guid configuration;int version;JsonElement fields;
        await using(var command=new NpgsqlCommand("SELECT b.relation_name,b.configuration_id,b.configuration_version,b.field_shape::text "
            +"FROM ppiq_staging.accepted_batches b JOIN ppiq_meta.source_dataset_governance g ON g.tenant_id=b.tenant_id AND g.id=b.dataset_governance_id "
            +"WHERE b.tenant_id=$1 AND g.source_dataset_definition_id=$2 AND b.batch_id=$3 AND b.state='Sealed'",connection,native))
        {
            command.Parameters.AddWithValue(tenantId);command.Parameters.AddWithValue(datasetId);command.Parameters.AddWithValue(batchId);
            await using var reader=await command.ExecuteReaderAsync(ct);
            if(!await reader.ReadAsync(ct)) return AcquisitionOutcome<AcceptedDatasetPreview>.Refuse("AR02","A sealed batch was not found for this dataset.");
            relation=reader.GetString(0);configuration=reader.GetGuid(1);version=reader.GetInt32(2);
            using var doc=JsonDocument.Parse(reader.GetString(3));fields=doc.RootElement.Clone();
        }
        if(relation!="accepted_batch_"+batchId.ToString("N"))
            throw new InvalidOperationException("The stored batch relation identity is inconsistent.");
        await using var rows=new NpgsqlCommand("SELECT row_to_json(v)::text FROM ppiq_staging.\""+relation+"\" v ORDER BY source_record_id LIMIT $1",connection,native);
        rows.Parameters.AddWithValue(take);
        var records=new List<IReadOnlyDictionary<string,object?>>();
        await using(var reader=await rows.ExecuteReaderAsync(ct))
        {
            while(await reader.ReadAsync(ct))
            {
                var record=new Dictionary<string,object?>(StringComparer.Ordinal);
                using var document=JsonDocument.Parse(reader.GetString(0));
                foreach(var property in document.RootElement.EnumerateObject()) record.Add(property.Name,property.Value.Clone());
                records.Add(record);
            }
        }
        await transaction.CommitAsync(ct);
        return AcquisitionOutcome<AcceptedDatasetPreview>.Accept(new(datasetId,batchId,configuration,version,fields,records));
    }
}
