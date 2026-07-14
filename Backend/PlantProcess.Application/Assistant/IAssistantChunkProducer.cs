using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlantProcess.Application.Assistant;

/// <summary>
/// M1-01: produces the assistant's retrieval corpus (dataset/config/doc/finding
/// chunks) from the canonical substrate. Implemented in Infrastructure over
/// Npgsql; the API and AddAssistant() depend only on this seam - same pattern as
/// IRetrievalIndex.
/// </summary>
public interface IAssistantChunkProducer
{
    Task<IReadOnlyList<RetrievedChunk>> BuildAsync(Guid tenantId, CancellationToken ct);
}