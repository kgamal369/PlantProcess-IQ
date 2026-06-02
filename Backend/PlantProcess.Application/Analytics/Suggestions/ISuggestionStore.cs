namespace PlantProcess.Application.Analytics.Suggestions;

public sealed record SuggestionSyncResult(int Created, int Updated, int Dismissed, string CorrelationId);

public interface ISuggestionStore
{
    /// <summary>Upserts the generated set for a tenant: create new, update changed in place, dismiss superseded.</summary>
    Task<SuggestionSyncResult> SyncAsync(Guid tenantId, IReadOnlyList<SuggestionCard> generated, string correlationId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SuggestionCard>> ListActiveAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Applies a workflow transition with RBAC + before/after audit. Returns the decision.</summary>
    Task<TransitionDecision> TransitionAsync(Guid tenantId, Guid suggestionId, SuggestionStatus to, string role, string actor, string? note, CancellationToken cancellationToken);
}