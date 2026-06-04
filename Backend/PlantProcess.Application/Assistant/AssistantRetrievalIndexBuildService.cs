namespace PlantProcess.Application.Assistant;

/// <summary>
/// PPIQ-T024: idempotent retrieval index refresh job.
/// 
/// The caller supplies canonical chunks from findings, KPI definitions, reports or docs.
/// The underlying index must be tenant-scoped and changed-content aware.
/// </summary>
public sealed class AssistantRetrievalIndexBuildService
{
    private readonly IRetrievalIndex _index;

    public AssistantRetrievalIndexBuildService(IRetrievalIndex index)
    {
        _index = index;
    }

    public async Task<AssistantIndexBuildResult> RefreshAsync(
        Guid tenantId,
        IReadOnlyList<RetrievedChunk> canonicalChunks,
        string sourceKind,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(sourceKind))
            throw new ArgumentException("Source kind is required.", nameof(sourceKind));

        var filtered = canonicalChunks
            .Where(x => !x.IsSynthetic)
            .GroupBy(x => $"{x.SourceKind}:{x.SourceRef}", StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(c => c.Score).First())
            .ToArray();

        var result = await _index.ReindexAsync(
            new ReindexRequest(tenantId, filtered, correlationId),
            cancellationToken);

        return new AssistantIndexBuildResult(
            tenantId,
            sourceKind,
            correlationId,
            filtered.Length,
            result.ReplacedCount,
            ChangedOnly: true,
            "completed");
    }
}

public sealed record AssistantIndexBuildResult(
    Guid TenantId,
    string SourceKind,
    string CorrelationId,
    int ChunkCount,
    int ReplacedCount,
    bool ChangedOnly,
    string Status);
