using System.Collections.Generic;

namespace PlantProcess.Application.Security.DataBoundary;

/// <summary>
/// Operational plan derived from DataBoundaryPolicy: whether a remote model may be
/// called, whether to fall back to the local provider, and exactly which evidence
/// chunks (if any) may leave the tenant.
/// </summary>
public sealed record AssistantEgressPlan(
    AssistantBoundaryOutcome Outcome,
    bool RemoteEgressAllowed,
    bool UseLocalProvider,
    IReadOnlyList<string> EgressChunks,
    string? Reason);