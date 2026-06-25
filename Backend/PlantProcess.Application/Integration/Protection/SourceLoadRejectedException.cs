using System;

namespace PlantProcess.Application.Integration.Protection;

/// <summary>
/// Thrown when a live source read is rejected by the load-protection policy.
/// Carries the typed decision so callers can surface the precise reason.
/// </summary>
public sealed class SourceLoadRejectedException : Exception
{
    public SourceLoadDecision Decision { get; }

    public SourceLoadRejectionReason Reason => Decision.Reason;

    public SourceLoadRejectedException(SourceLoadDecision decision)
        : base(decision.Message ?? "Source query rejected by the load-protection policy.")
    {
        Decision = decision;
    }
}