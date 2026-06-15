namespace PlantProcess.Application.Integration.Protection;

/// <summary>PPIQ-902: per-source load budget - row cap, statement timeout, rate limit, approved window.</summary>
public sealed record SourceLoadBudget(
    int MaxRows,
    int StatementTimeoutSeconds,
    int MaxQueriesPerMinute,
    TimeOnly? WindowStartUtc = null,
    TimeOnly? WindowEndUtc = null);

public sealed record SourceQueryRequest(
    bool HasRowLimit,
    int RequestedRowLimit,
    int QueriesInLastMinute,
    TimeOnly NowUtc);

public enum SourceLoadRejectionReason
{
    None,
    NoRowLimit,
    RowCapExceeded,
    RateLimitExceeded,
    OutsideApprovedWindow
}

public sealed record SourceLoadDecision(
    bool Allowed,
    SourceLoadRejectionReason Reason,
    int EffectiveRowLimit,
    int StatementTimeoutSeconds,
    string? Message);

/// <summary>
/// Enforces a load budget on a source query. No unbounded query may reach a production source;
/// over-budget reads are rejected with a typed reason; reads outside the approved window are blocked.
/// </summary>
public static class SourceLoadProtectionPolicy
{
    public static SourceLoadDecision Evaluate(SourceQueryRequest request, SourceLoadBudget budget)
    {
        if (!request.HasRowLimit)
            return new(false, SourceLoadRejectionReason.NoRowLimit, 0, budget.StatementTimeoutSeconds,
                "Unbounded query rejected: every source read must declare a row limit.");

        if (request.RequestedRowLimit > budget.MaxRows)
            return new(false, SourceLoadRejectionReason.RowCapExceeded, budget.MaxRows, budget.StatementTimeoutSeconds,
                $"Requested {request.RequestedRowLimit} rows exceeds the {budget.MaxRows}-row source cap.");

        if (request.QueriesInLastMinute >= budget.MaxQueriesPerMinute)
            return new(false, SourceLoadRejectionReason.RateLimitExceeded, request.RequestedRowLimit, budget.StatementTimeoutSeconds,
                $"Rate limit of {budget.MaxQueriesPerMinute}/min reached for this source.");

        if (budget.WindowStartUtc is TimeOnly start && budget.WindowEndUtc is TimeOnly end)
        {
            var now = request.NowUtc;
            var inWindow = start <= end ? (now >= start && now <= end) : (now >= start || now <= end);
            if (!inWindow)
                return new(false, SourceLoadRejectionReason.OutsideApprovedWindow, request.RequestedRowLimit, budget.StatementTimeoutSeconds,
                    $"Read at {now} is outside the approved window {start}-{end}.");
        }

        return new(true, SourceLoadRejectionReason.None, request.RequestedRowLimit, budget.StatementTimeoutSeconds, null);
    }
}