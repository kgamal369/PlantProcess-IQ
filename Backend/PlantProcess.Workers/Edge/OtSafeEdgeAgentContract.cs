namespace PlantProcess.Workers.Edge;

/// <summary>
/// PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND
/// Contract-only worker-side model for the OT-safe edge collector.
///
/// Safety invariant:
/// - read-only collection from source systems
/// - no inbound listener required in the OT network
/// - outbound-only push to PlantProcess IQ
/// - bounded local queue/spool before push
/// </summary>
public static class OtSafeEdgeAgentContract
{
    public const string Marker = "PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND";
    public const string Mode = "read-only-outbound-one-way-push";
    public const bool OpensInboundListener = false;
    public const bool ReadOnlyCollection = true;
    public const bool OutboundOnly = true;

    public static EdgeAgentHeartbeat CreateHeartbeat(
        string collectorId,
        string agentVersion,
        int localQueueDepth,
        int failedPushCount,
        string status = "healthy",
        string? lastError = null)
    {
        return new EdgeAgentHeartbeat(
            CollectorId: collectorId,
            AgentVersion: agentVersion,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            LocalQueueDepth: Math.Max(0, localQueueDepth),
            FailedPushCount: Math.Max(0, failedPushCount),
            LastError: lastError);
    }

    public static EdgeAgentPushBatch CreateBatch(
        string collectorId,
        string batchId,
        int sequenceNumber,
        IReadOnlyCollection<EdgeAgentSample> samples)
    {
        if (string.IsNullOrWhiteSpace(collectorId))
            throw new ArgumentException("Collector id is required.", nameof(collectorId));

        if (string.IsNullOrWhiteSpace(batchId))
            throw new ArgumentException("Batch id is required.", nameof(batchId));

        if (samples.Count > 5000)
            throw new InvalidOperationException("Maximum edge batch size is 5000 samples.");

        return new EdgeAgentPushBatch(
            CollectorId: collectorId,
            BatchId: batchId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            ReadOnlyCollection: true,
            OutboundOnly: true,
            OpensInboundListener: false,
            SequenceNumber: sequenceNumber,
            Samples: samples.ToArray());
    }
}

public sealed record EdgeAgentHeartbeat(
    string CollectorId,
    string AgentVersion,
    DateTimeOffset ObservedAtUtc,
    string Status,
    int LocalQueueDepth,
    int FailedPushCount,
    string? LastError);

public sealed record EdgeAgentSample(
    string SourceProfile,
    string TagPath,
    DateTimeOffset TimestampUtc,
    double? NumericValue,
    string? TextValue,
    string? Unit,
    string Quality);

public sealed record EdgeAgentPushBatch(
    string CollectorId,
    string BatchId,
    DateTimeOffset CreatedAtUtc,
    bool ReadOnlyCollection,
    bool OutboundOnly,
    bool OpensInboundListener,
    int SequenceNumber,
    EdgeAgentSample[] Samples);
