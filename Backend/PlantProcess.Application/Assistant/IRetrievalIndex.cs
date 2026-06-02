namespace PlantProcess.Application.Assistant;

public sealed record ReindexRequest(Guid TenantId, IReadOnlyList<RetrievedChunk> Chunks, string CorrelationId);
public sealed record ReindexResult(int ChunkCount, int ReplacedCount, string CorrelationId);

public interface IRetrievalIndex
{
    /// <summary>Tenant- AND permission-scoped search; excludes synthetic and stale chunks.</summary>
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(RetrievalQuery query, CancellationToken cancellationToken);

    /// <summary>Replaces stale chunks for a tenant and records per-tenant counts. Never crosses tenants.</summary>
    Task<ReindexResult> ReindexAsync(ReindexRequest request, CancellationToken cancellationToken);
}