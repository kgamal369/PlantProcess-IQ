namespace PlantProcess.Application.Analytics.Suggestions;

public sealed record TransitionDecision(bool Allowed, string Reason);

/// <summary>
/// PPIQ_REALIZATION_T047_DETERMINISTIC_SUGGESTION_WORKFLOW.
/// T-047/T-049: pure transition + RBAC rules. open -> assigned -> accepted | rejected -> closed; dismissed is
/// terminal. An Operator may acknowledge (assign) but never accept. Illegal transitions are refused.
/// </summary>
public static class SuggestionWorkflow
{
    private static readonly Dictionary<SuggestionStatus, SuggestionStatus[]> Allowed = new()
    {
        [SuggestionStatus.Open]      = new[] { SuggestionStatus.Assigned, SuggestionStatus.Rejected, SuggestionStatus.Dismissed },
        [SuggestionStatus.Assigned]  = new[] { SuggestionStatus.Accepted, SuggestionStatus.Rejected, SuggestionStatus.Dismissed },
        [SuggestionStatus.Accepted]  = new[] { SuggestionStatus.Closed },
        [SuggestionStatus.Rejected]  = Array.Empty<SuggestionStatus>(),
        [SuggestionStatus.Closed]    = Array.Empty<SuggestionStatus>(),
        [SuggestionStatus.Dismissed] = Array.Empty<SuggestionStatus>(),
    };

    public static TransitionDecision CanTransition(SuggestionStatus from, SuggestionStatus to, string role)
    {
        if (!Allowed.TryGetValue(from, out var next) || Array.IndexOf(next, to) < 0)
            return new TransitionDecision(false, $"Illegal transition {from} -> {to}.");

        var r = (role ?? "").Trim().ToLowerInvariant();
        var isManager = r is "admin" or "engineer" or "plantprocessadmin" or "plantprocessdatamanager";

        // Operators/Viewers may move open->assigned (acknowledge) but never accept/close.
        if (!isManager && (to is SuggestionStatus.Accepted or SuggestionStatus.Closed))
            return new TransitionDecision(false, $"Role '{role}' may acknowledge but not {to}.");

        return new TransitionDecision(true, "ok");
    }
}