using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Assistant;

public sealed record RetrievalQuery(
    Guid TenantId, string Role, string Text, int TopK = 6, IReadOnlyList<string>? ContextTerms = null);

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

/// <summary>
/// T-072. What the user is looking at when the question is asked.
///
/// The law this record exists to keep: CONTEXT NARROWS RETRIEVAL, AND CONTEXT IS
/// NOT EVIDENCE. Only the identifiers below contribute retrieval terms. The
/// last-result summary and the evidence handles are carried so the evidence
/// surfaces can link back, and are deliberately NOT embedded, because a number
/// supplied by the client must never influence what the retrieval layer treats
/// as evidence. Tenant, role and licence are NOT here: they come from the
/// caller's claims, so this envelope can narrow a search and can never widen
/// permission.
/// </summary>
public sealed record AssistantContextEnvelope(
    string? Route = null,
    string? PageCode = null,
    string? WidgetCode = null,
    IReadOnlyList<string>? Selections = null,
    IReadOnlyList<string>? Filters = null,
    string? LastResultSummary = null,
    IReadOnlyList<string>? EvidenceHandles = null)
{
    private const int MaxTermLength = 120;
    private const int MaxTerms = 24;

    /// <summary>The terms this envelope contributes to retrieval, and nothing else.</summary>
    public IReadOnlyList<string> RetrievalTerms()
    {
        var terms = new List<string>();

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (terms.Count >= MaxTerms) return;
            var trimmed = value.Trim();
            if (trimmed.Length > MaxTermLength) trimmed = trimmed.Substring(0, MaxTermLength);
            terms.Add(trimmed);
        }

        Add(Route);
        Add(PageCode);
        Add(WidgetCode);
        foreach (var selection in Selections ?? Array.Empty<string>()) Add(selection);
        foreach (var filter in Filters ?? Array.Empty<string>()) Add(filter);

        return terms;
    }
}

public sealed record AssistantRequest(
    Guid TenantId, string Role, string License, string Question, IReadOnlyList<string> ContextChips,
    AssistantContextEnvelope? Context = null);