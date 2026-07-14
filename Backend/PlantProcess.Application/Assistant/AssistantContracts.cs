using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Assistant;

public sealed record RetrievalQuery(Guid TenantId, string Role, string Text, int TopK = 6);

public sealed record RetrievedChunk(
    Guid Id, string SourceKind, string SourceRef, string Content, ProvenanceHandle Handle, double Score, bool IsSynthetic = false, string? ScopeRole = null);

/// <summary>A single grounded statement the assistant may surface, with the exact numbers it authorizes.</summary>
public sealed record AssistantClaim(
    string Text, ProvenanceHandle Handle, IReadOnlyList<string> NumericTokens, bool IsSynthetic = false);

public sealed record AssistantDraft(string Text, IReadOnlyList<AssistantClaim> Claims);

public sealed record GroundedAnswer(
    string Text,
    IReadOnlyList<ProvenanceHandle> Citations,
    bool IsRefusal,
    string? RefusalReason,
    IReadOnlyList<string> BlockedSentences)
{
    public static GroundedAnswer Refusal(string reason)
        => new(string.Empty, Array.Empty<ProvenanceHandle>(), true, reason, Array.Empty<string>());
}

public sealed record ToolContext(Guid TenantId, string Role, string License);

public sealed record ToolResult(
    bool Ok, string ToolName, string? PayloadJson, IReadOnlyList<ProvenanceHandle> Handles, string? Error)
{
    public static ToolResult Refused(string name, string error) => new(false, name, null, Array.Empty<ProvenanceHandle>(), error);
}

public sealed record AssistantRequest(
    Guid TenantId, string Role, string License, string Question, IReadOnlyList<string> ContextChips);