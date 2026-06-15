namespace PlantProcess.Application.Security.DataBoundary;

/// <summary>PPIQ-904: an assistant dispatch decision honouring the per-tenant no-egress boundary.</summary>
public sealed record AssistantDispatch(
    bool NoEgressEnabled,
    bool ModelIsLocal,
    string Question,
    IReadOnlyList<string> ScopedEvidenceHandles,
    bool PayloadContainsPlantData);

public enum AssistantBoundaryOutcome
{
    LocalAllowed,
    RemoteAllowedScopedOnly,
    BlockedNoEgress,
    BlockedPlantDataLeak
}

public sealed record DataBoundaryDecision(AssistantBoundaryOutcome Outcome, bool Allowed, string? Reason);

/// <summary>
/// No plant data may leave the tenant for computation. With no-egress enabled the assistant path is
/// local-only or disabled. A remote (zero-retention) endpoint may receive ONLY the question + scoped handles.
/// </summary>
public static class DataBoundaryPolicy
{
    public static DataBoundaryDecision Evaluate(AssistantDispatch dispatch)
    {
        if (dispatch.PayloadContainsPlantData)
            return new(AssistantBoundaryOutcome.BlockedPlantDataLeak, false,
                "Payload contains plant data; only the question + scoped evidence handles may leave the tenant.");

        if (dispatch.NoEgressEnabled && !dispatch.ModelIsLocal)
            return new(AssistantBoundaryOutcome.BlockedNoEgress, false,
                "No-egress is enabled and the model is not local; the assistant path is disabled.");

        if (dispatch.ModelIsLocal)
            return new(AssistantBoundaryOutcome.LocalAllowed, true, null);

        return new(AssistantBoundaryOutcome.RemoteAllowedScopedOnly, true, null);
    }
}