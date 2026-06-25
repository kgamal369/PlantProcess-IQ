using System;
using System.Collections.Generic;

namespace PlantProcess.Application.Security.DataBoundary;

/// <summary>
/// Turns the DataBoundaryPolicy decision into a concrete egress plan for the assistant
/// gateway: under no-egress (self-hosted default) an external model is never called and
/// the gateway answers locally; a permitted remote endpoint receives ONLY the question
/// and scoped evidence handles - raw evidence chunks never leave the tenant.
/// </summary>
public static class AssistantEgressGuard
{
    public static AssistantEgressPlan Plan(
        bool noEgressEnabled,
        bool modelIsExternal,
        string question,
        IReadOnlyList<string> evidenceHandles,
        IReadOnlyList<string> evidenceChunks)
    {
        var handles = evidenceHandles ?? Array.Empty<string>();
        var chunks = evidenceChunks ?? Array.Empty<string>();

        var decision = DataBoundaryPolicy.Evaluate(new AssistantDispatch(
            NoEgressEnabled: noEgressEnabled,
            ModelIsLocal: !modelIsExternal,
            Question: question,
            ScopedEvidenceHandles: handles,
            PayloadContainsPlantData: false));

        switch (decision.Outcome)
        {
            case AssistantBoundaryOutcome.LocalAllowed:
                return new AssistantEgressPlan(decision.Outcome, false, true, chunks, decision.Reason);

            case AssistantBoundaryOutcome.RemoteAllowedScopedOnly:
                return new AssistantEgressPlan(decision.Outcome, true, false, Array.Empty<string>(), decision.Reason);

            case AssistantBoundaryOutcome.BlockedPlantDataLeak:
                return new AssistantEgressPlan(decision.Outcome, false, true, Array.Empty<string>(), decision.Reason);

            case AssistantBoundaryOutcome.BlockedNoEgress:
            default:
                return new AssistantEgressPlan(decision.Outcome, false, true, chunks, decision.Reason);
        }
    }
}