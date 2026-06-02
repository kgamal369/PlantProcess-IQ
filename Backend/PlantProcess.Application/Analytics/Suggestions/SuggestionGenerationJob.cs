namespace PlantProcess.Application.Analytics.Suggestions;

/// <summary>
/// T-047: runs deterministic generation off the overall-learning output and reconciles via the store
/// (create / update-in-place / dismiss-superseded), returning monitor counts + a correlation id.
/// </summary>
public sealed class SuggestionGenerationJob
{
    private readonly ISuggestionEngine _engine;
    private readonly ISuggestionStore _store;

    public SuggestionGenerationJob(ISuggestionEngine engine, ISuggestionStore store)
    {
        _engine = engine;
        _store = store;
    }

    public async Task<SuggestionSyncResult> RunAsync(Guid tenantId, IReadOnlyList<ApprovedFinding> approvedFindings, string? correlationId, CancellationToken cancellationToken)
    {
        var corr = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId!;
        var cards = _engine.Generate(approvedFindings);
        return await _store.SyncAsync(tenantId, cards, corr, cancellationToken);
    }
}