using PlantProcess.Application.Assistant;

namespace PlantProcess.Infrastructure.Assistant;

/// <summary>
/// T-073. The registered IAssistantChunkProducer.
///
/// CanonicalChunkProducer keeps its five families and is not modified, so
/// nothing that already works can regress here. The widget-result family is a
/// separate producer, which keeps dashboard execution out of the type that
/// reads the canonical substrate.
///
/// The widget family FAILS SOFT: if it throws, the canonical families still
/// reindex. A reindex that returns nothing at all because one widget misbehaved
/// would take the whole assistant down with it.
/// </summary>
public sealed class CompositeChunkProducer : IAssistantChunkProducer
{
    private readonly CanonicalChunkProducer _canonical;
    private readonly WidgetResultChunkProducer _widgets;

    public CompositeChunkProducer(CanonicalChunkProducer canonical, WidgetResultChunkProducer widgets)
    {
        _canonical = canonical;
        _widgets = widgets;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> BuildAsync(Guid tenantId, CancellationToken ct)
    {
        var chunks = new List<RetrievedChunk>();
        chunks.AddRange(await _canonical.BuildAsync(tenantId, ct));

        try
        {
            chunks.AddRange(await _widgets.BuildAsync(tenantId, ct));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Reported by absence: the widget family simply contributes nothing,
            // and the assistant then refuses chart-value questions honestly
            // rather than answering from a stale corpus.
        }

        return chunks;
    }
}