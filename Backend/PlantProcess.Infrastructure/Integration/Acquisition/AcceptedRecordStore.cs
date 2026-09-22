using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using PlantProcess.Application.Integration.Acquisition;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Integration.Acquisition;

/// <summary>
/// Owns one local database transaction per durable acknowledgement. The fencing
/// authority must share this scoped DbContext and hold its authoritative lock until
/// commit. Missing authorities refuse; no client-provided proof bit is accepted.
/// </summary>
public sealed class AcceptedRecordStore : IAcceptedRecordStore
{
    private readonly PlantProcessDbContext _db;
    private readonly ISessionFencingAuthority _fencing;
    private readonly IOriginalBytesPreservationAuthority _preservation;

    public AcceptedRecordStore(PlantProcessDbContext db, ISessionFencingAuthority fencing,
        IOriginalBytesPreservationAuthority preservation)
    {
        _db = db;
        _fencing = fencing;
        _preservation = preservation;
    }

    public Task<AcquisitionOutcome<AcceptedBatchView>> OpenAsync(AcceptedBatchRequest r, CancellationToken ct) =>
        InTransactionAsync(r.TenantId, async () =>
        {
            var refusal = await _fencing.ValidateAsync(r.TenantId, r.DatasetGovernanceId,
                r.StreamKey, r.SessionId, r.Generation, ct);
            if (refusal is not null) return AcquisitionOutcome<AcceptedBatchView>.Refuse(refusal);
            await using var command = Command("SELECT ppiq_staging.accepted_open(@tenant,@dataset,@batch,@session,@generation,@stream,@ordinal,@configuration,@version,@group_key,@max_records,@max_bytes,@start)");
            Add(command, "tenant", NpgsqlDbType.Uuid, r.TenantId);
            Add(command, "dataset", NpgsqlDbType.Uuid, r.DatasetGovernanceId);
            Add(command, "batch", NpgsqlDbType.Uuid, r.BatchId);
            Add(command, "session", NpgsqlDbType.Uuid, r.SessionId);
            Add(command, "generation", NpgsqlDbType.Bigint, r.Generation);
            Add(command, "stream", NpgsqlDbType.Text, r.StreamKey);
            Add(command, "ordinal", NpgsqlDbType.Bigint, r.BatchOrdinal);
            Add(command, "configuration", NpgsqlDbType.Uuid, r.ConfigurationId);
            Add(command, "version", NpgsqlDbType.Integer, r.ConfigurationVersion);
            Add(command, "group_key", NpgsqlDbType.Text, r.RecordingGroupKey);
            Add(command, "max_records", NpgsqlDbType.Bigint, r.MaximumRecords);
            Add(command, "max_bytes", NpgsqlDbType.Bigint, r.MaximumBytes);
            Add(command, "start", NpgsqlDbType.Text, r.StartPosition);
            await command.ExecuteScalarAsync(ct);
            return await ReadBatchAsync(r.TenantId, r.BatchId, ct);
        }, ct);

    public Task<AcquisitionOutcome<AcceptedRecordReceipt>> AcceptAsync(AcceptedRecordRequest r, CancellationToken ct) =>
        InTransactionAsync(r.TenantId, async () =>
        {
            var scope = await BatchScopeAsync(r.TenantId, r.BatchId, ct);
            if (scope is null) return AcquisitionOutcome<AcceptedRecordReceipt>.Refuse("AR02", "Batch not found.");
            var refusal = await _fencing.ValidateAsync(r.TenantId, scope.Value.Dataset,
                scope.Value.Stream, r.SessionId, r.Generation, ct);
            if (refusal is not null) return AcquisitionOutcome<AcceptedRecordReceipt>.Refuse(refusal);
            if (scope.Value.PreservationRequired)
            {
                refusal = await _preservation.ValidateAsync(r.TenantId, r.Envelope, ct);
                if (refusal is not null) return AcquisitionOutcome<AcceptedRecordReceipt>.Refuse(refusal);
            }
            if (r.Envelope.ValueKind != JsonValueKind.Object)
                return AcquisitionOutcome<AcceptedRecordReceipt>.Refuse("AR11", "An object envelope is required.");
            await using var command = Command("SELECT ppiq_staging.accepted_append(@tenant,@batch,@session,@generation,@record,@envelope)::text");
            AddIdentity(command, r.TenantId, r.BatchId, r.SessionId, r.Generation);
            Add(command, "record", NpgsqlDbType.Text, r.RecordId);
            Add(command, "envelope", NpgsqlDbType.Jsonb, r.Envelope.GetRawText());
            var raw = (string)(await command.ExecuteScalarAsync(ct))!;
            using var document = JsonDocument.Parse(raw);
            var value = document.RootElement;
            if (value.TryGetProperty("code", out var code))
                return AcquisitionOutcome<AcceptedRecordReceipt>.Refuse(code.GetString()!, value.GetProperty("detail").GetString()!);
            return AcquisitionOutcome<AcceptedRecordReceipt>.Accept(new(
                value.GetProperty("receipt_id").GetGuid(), value.GetProperty("batch_id").GetGuid(),
                value.GetProperty("record_id").GetString()!, value.GetProperty("content_hash").GetString()!,
                value.GetProperty("durable_position").GetInt64(),
                DateTimeOffset.Parse(value.GetProperty("accepted_at_utc").GetString()!, CultureInfo.InvariantCulture).UtcDateTime));
        }, ct, commitConflictEvidence: true);

    public Task<AcquisitionOutcome<AcceptedBatchView>> SealAsync(AcceptedBatchSeal r, CancellationToken ct) =>
        InTransactionAsync(r.TenantId, async () =>
        {
            var scope = await BatchScopeAsync(r.TenantId, r.BatchId, ct);
            if (scope is null) return AcquisitionOutcome<AcceptedBatchView>.Refuse("AR02", "Batch not found.");
            var refusal = await _fencing.ValidateAsync(r.TenantId, scope.Value.Dataset,
                scope.Value.Stream, r.SessionId, r.Generation, ct);
            if (refusal is not null) return AcquisitionOutcome<AcceptedBatchView>.Refuse(refusal);
            await using var command = Command("SELECT ppiq_staging.accepted_seal(@tenant,@batch,@session,@generation,@count,@bytes,@end)");
            AddIdentity(command, r.TenantId, r.BatchId, r.SessionId, r.Generation);
            Add(command, "count", NpgsqlDbType.Bigint, r.ExpectedRecords);
            Add(command, "bytes", NpgsqlDbType.Bigint, r.ExpectedBytes);
            Add(command, "end", NpgsqlDbType.Text, r.EndPosition);
            await command.ExecuteScalarAsync(ct);
            return await ReadBatchAsync(r.TenantId, r.BatchId, ct);
        }, ct);

    public Task<AcquisitionOutcome<AcceptedBatchView>> GetAsync(Guid tenantId, Guid batchId, CancellationToken ct) =>
        InTransactionAsync(tenantId, () => ReadBatchAsync(tenantId, batchId, ct), ct);

    private async Task<AcquisitionOutcome<AcceptedBatchView>> ReadBatchAsync(Guid tenant, Guid batch, CancellationToken ct)
    {
        await using var command = Command("SELECT b.batch_id,b.dataset_governance_id,b.configuration_id,b.configuration_version,b.state,b.relation_name,b.record_count,b.payload_bytes,f.committed_ordinal "
            + "FROM ppiq_staging.accepted_batches b JOIN ppiq_staging.accepted_capture_fences f ON f.tenant_id=b.tenant_id AND f.dataset_governance_id=b.dataset_governance_id AND f.stream_key=b.stream_key "
            + "WHERE b.tenant_id=@tenant AND b.batch_id=@batch");
        Add(command, "tenant", NpgsqlDbType.Uuid, tenant);
        Add(command, "batch", NpgsqlDbType.Uuid, batch);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return AcquisitionOutcome<AcceptedBatchView>.Refuse("AR02", "Batch not found.");
        return AcquisitionOutcome<AcceptedBatchView>.Accept(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2),
            reader.GetInt32(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetInt64(6), reader.GetInt64(7), reader.GetInt64(8)));
    }

    private async Task<(Guid Dataset, string Stream, bool PreservationRequired)?> BatchScopeAsync(Guid tenant, Guid batch, CancellationToken ct)
    {
        await using var command = Command("SELECT dataset_governance_id,stream_key,preservation_required FROM ppiq_staging.accepted_batches WHERE tenant_id=@tenant AND batch_id=@batch");
        Add(command, "tenant", NpgsqlDbType.Uuid, tenant);
        Add(command, "batch", NpgsqlDbType.Uuid, batch);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? (reader.GetGuid(0), reader.GetString(1), reader.GetBoolean(2)) : null;
    }

    private async Task<AcquisitionOutcome<T>> InTransactionAsync<T>(Guid tenant,
        Func<Task<AcquisitionOutcome<T>>> body, CancellationToken ct, bool commitConflictEvidence = false)
    {
        if (tenant == Guid.Empty) return AcquisitionOutcome<T>.Refuse("AR01", "A resolved tenant is required.");
        if (_db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Durable acceptance owns its acknowledgement transaction; an ambient transaction is not supported.");
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await using (var scope = Command("SELECT set_config('app.current_tenant',@tenant,true)"))
            {
                Add(scope, "tenant", NpgsqlDbType.Text, tenant.ToString("D"));
                await scope.ExecuteScalarAsync(ct);
            }
            var result = await body();
            if (result.IsAccepted || (commitConflictEvidence && result.Refusal?.Code == "AR07"))
                await transaction.CommitAsync(ct);
            else await transaction.RollbackAsync(ct);
            return result;
        }
        catch (PostgresException ex) when (ex.SqlState.StartsWith("22", StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return AcquisitionOutcome<T>.Refuse("AR12", "A declared field value or envelope member has an invalid representation.");
        }
        catch (PostgresException ex) when (ex.SqlState == "23505" && ex.ConstraintName == "uq_accepted_batches_stream_ordinal")
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return AcquisitionOutcome<T>.Refuse("AR02", "The stream batch ordinal is already assigned.");
        }
        catch (PostgresException ex) when (ex.SqlState == "P0001" && ex.MessageText.StartsWith("AR", StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            var code = ex.MessageText.Split(' ', 2)[0];
            return AcquisitionOutcome<T>.Refuse(code, ex.MessageText);
        }
    }

    private NpgsqlCommand Command(string sql) => new(sql,
        (NpgsqlConnection)_db.Database.GetDbConnection(),
        (NpgsqlTransaction)(_db.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("A transaction is required.")));

    private static void Add(NpgsqlCommand command, string name, NpgsqlDbType type, object? value) =>
        command.Parameters.Add(new NpgsqlParameter(name, type) { Value = value ?? DBNull.Value });

    private static void AddIdentity(NpgsqlCommand command, Guid tenant, Guid batch, Guid session, long generation)
    {
        Add(command, "tenant", NpgsqlDbType.Uuid, tenant);
        Add(command, "batch", NpgsqlDbType.Uuid, batch);
        Add(command, "session", NpgsqlDbType.Uuid, session);
        Add(command, "generation", NpgsqlDbType.Bigint, generation);
    }
}
