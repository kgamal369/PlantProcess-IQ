using System.Text.Json;

namespace PlantProcess.Application.Integration.Acquisition;

/// <summary>References an exact published configuration, never a moving latest version.</summary>
public sealed record AcceptedBatchRequest(
    Guid TenantId, Guid DatasetGovernanceId, Guid BatchId, Guid SessionId,
    long Generation, string StreamKey, long BatchOrdinal,
    Guid ConfigurationId, int ConfigurationVersion, string RecordingGroupKey,
    long MaximumRecords, long MaximumBytes, string? StartPosition);

/// <summary>
/// RecordId identifies an occurrence within an authorized stream. Values and timestamps
/// must never be used to merge distinct occurrences. Values are keyed by stable field ID.
/// The envelope is persisted once, and its canonical JSONB content defines retry equality.
/// </summary>
public sealed record AcceptedRecordRequest(
    Guid TenantId, Guid BatchId, Guid SessionId, long Generation,
    string RecordId, JsonElement Envelope);

public sealed record AcceptedRecordReceipt(
    Guid ReceiptId, Guid BatchId, string RecordId, string ContentHash,
    long DurablePosition, DateTime AcceptedAtUtc);

public sealed record AcceptedBatchSeal(
    Guid TenantId, Guid BatchId, Guid SessionId, long Generation,
    long ExpectedRecords, long ExpectedBytes, string? EndPosition);

public sealed record AcceptedBatchView(
    Guid BatchId, Guid DatasetGovernanceId, Guid ConfigurationId,
    int ConfigurationVersion, string State, string? RelationName,
    long RecordCount, long PayloadBytes, long CommittedOrdinal);

/// <summary>
/// Runtime ownership is outside the accepted-record store. The authority must check
/// the actual capture/replay ownership in the same database unit of work and hold
/// its takeover lock until that transaction completes. A token supplied by a client
/// or a batch-local generation is not proof. No authority is permissive by default.
/// </summary>
public interface ISessionFencingAuthority
{
    Task<AcquisitionRefusal?> ValidateAsync(
        Guid tenantId, Guid datasetGovernanceId, string streamKey,
        Guid sessionId, long generation, CancellationToken cancellationToken);
}

/// <summary>
/// Metadata alone is not durable original-byte preservation. An implementation must
/// verify immutable bytes, hash, size and the required retention obligation.
/// </summary>
public interface IOriginalBytesPreservationAuthority
{
    Task<AcquisitionRefusal?> ValidateAsync(
        AcceptedOriginalBytesRequirement requirement, CancellationToken cancellationToken);
}

public sealed class UnavailableSessionFencingAuthority : ISessionFencingAuthority
{
    public Task<AcquisitionRefusal?> ValidateAsync(Guid tenantId, Guid datasetGovernanceId,
        string streamKey, Guid sessionId, long generation, CancellationToken cancellationToken) =>
        Task.FromResult<AcquisitionRefusal?>(new("AR13", "The capture/replay ownership authority is unavailable."));
}

public sealed class UnavailableOriginalBytesPreservationAuthority : IOriginalBytesPreservationAuthority
{
    public Task<AcquisitionRefusal?> ValidateAsync(AcceptedOriginalBytesRequirement requirement,
        CancellationToken cancellationToken) => Task.FromResult<AcquisitionRefusal?>(
            new("AR14", "The immutable original-byte preservation authority is unavailable."));
}

public interface IAcceptedRecordStore
{
    Task<AcquisitionOutcome<AcceptedBatchView>> OpenAsync(AcceptedBatchRequest request, CancellationToken cancellationToken);
    Task<AcquisitionOutcome<AcceptedRecordReceipt>> AcceptAsync(AcceptedRecordRequest request, CancellationToken cancellationToken);
    Task<AcquisitionOutcome<AcceptedBatchView>> SealAsync(AcceptedBatchSeal request, CancellationToken cancellationToken);
    Task<AcquisitionOutcome<AcceptedBatchView>> GetAsync(Guid tenantId, Guid batchId, CancellationToken cancellationToken);
}

/// <summary>Immutable evidence of missing source occurrences. A gap never supplies invented values.</summary>
public sealed record AcceptedGapRequest(Guid TenantId, Guid BatchId, Guid SessionId, long Generation,
    Guid GapId, string Reason, string? FromPosition, string? ToPosition, long? MissingCount);

public interface IAcceptedGapStore
{
    Task<AcquisitionOutcome<Guid>> RecordGapAsync(AcceptedGapRequest request, CancellationToken cancellationToken);
}

/// <summary>Exact source bytes are a separate immutable artifact, never the JSONB payload's byte image.</summary>
public sealed record AcceptedSourceArtifact(Guid ArtifactId, string Sha256, int Length, string MediaType);

public interface IAcceptedSourceArtifactStore
{
    Task<AcquisitionOutcome<AcceptedSourceArtifact>> PutAsync(Guid tenantId, Guid artifactId,
        byte[] bytes, string mediaType, string expectedSha256, CancellationToken cancellationToken);
}

/// <summary>Exact consumer context for the separate retention-policy producer. The producer must
/// install a tenant/batch/record/artifact-bound durable admission in the same transaction;
/// returning null alone cannot bypass database admission. Runtime role has no table write grant.</summary>
public sealed record AcceptedOriginalBytesRequirement(Guid TenantId, Guid DatasetGovernanceId,
    Guid BatchId, string RecordId, Guid ConfigurationId, int ConfigurationVersion,
    string PolicyReference, JsonElement Envelope);
