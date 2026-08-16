using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using PlantProcess.Application.Assistant.Planning;

namespace PlantProcess.Application.Assistant.Retrieval;

/// <summary>
/// T-180. WHAT EVIDENCE IS, AND WHO IS ALLOWED TO SEE IT.
///
/// This layer begins at a T-179 plan and ends at a bounded evidence package. It does
/// not answer the question, does not phrase anything, and does not verify a claim.
///
/// THE INVARIANT THE TYPES ENFORCE. Forbidden evidence must never enter the candidate
/// pool. Not "must be removed before display" and not "must be filtered after
/// ranking": never enter. A pool filtered afterwards leaks through scores, counts,
/// ordering, truncation behaviour and any fingerprint computed over it, all of which
/// are visible without a single forbidden row ever being shown.
///
/// So the ranker cannot be handed an unfiltered pool. It takes a
/// <see cref="PermittedCandidateSet"/>, and the only way to obtain one is through the
/// permission filter. This is a compile-time guarantee rather than a discipline.
/// </summary>
public enum RetrievalOutcome
{
    /// <summary>Permitted evidence was found and packed.</summary>
    EvidencePacked = 0,

    /// <summary>Retrieval ran and this caller may see nothing that matches.</summary>
    NoPermittedEvidence = 1,

    /// <summary>No retrieval signal was available at all. Not the same as finding nothing.</summary>
    RetrievalUnavailable = 2,

    /// <summary>The supplied plan is not one this layer may execute.</summary>
    PlanNotExecutable = 3
}

/// <summary>
/// What kind of evidence an item is, and therefore its priority under a budget.
///
/// A structured tool result is the value itself. A retrieved passage is text that
/// mentions it. When a budget forces a choice, the value outranks the mention, and it
/// does so by class rather than by score: a well-worded paragraph can out-score an
/// exact figure on any lexical measure, and it is still the weaker evidence.
/// </summary>
public enum EvidenceClass
{
    StructuredToolResult = 0,
    CanonicalRecord = 1,
    RetrievedPassage = 2
}

/// <summary>How a retrieval signal was obtained. A signal, never an authority.</summary>
public enum RetrievalSignal
{
    Exact = 0,
    Lexical = 1,
    Semantic = 2
}

/// <summary>
/// One candidate before permission has been considered.
///
/// TokenCost is declared by the producer rather than computed here. Counting tokens
/// requires a tokeniser, a tokeniser belongs to whichever model will read the pack,
/// and a budget that guessed would be a budget that lies.
/// </summary>
public sealed record EvidenceCandidate(
    string EvidenceHandle,
    string TenantId,
    string ToolId,
    EvidenceClass Class,
    string ContentIdentity,
    string Payload,
    int TokenCost,
    double ExactScore,
    double LexicalScore,
    double? SemanticScore,
    ImmutableArray<string> EntityScope,
    string Provenance)
{
    public static EvidenceCandidate Create(
        string evidenceHandle,
        string tenantId,
        string toolId,
        EvidenceClass evidenceClass,
        string contentIdentity,
        string payload,
        int tokenCost,
        double exactScore = 0.0,
        double lexicalScore = 0.0,
        double? semanticScore = null,
        IEnumerable<string>? entityScope = null,
        string provenance = "")
    {
        if (string.IsNullOrWhiteSpace(evidenceHandle))
        {
            throw new ArgumentException(
                "Every candidate must carry a stable evidence handle. Evidence a later "
                    + "verifier cannot address is evidence it cannot check.",
                nameof(evidenceHandle));
        }

        if (tokenCost < 0)
        {
            throw new ArgumentException("A token cost may not be negative.", nameof(tokenCost));
        }

        return new EvidenceCandidate(
            evidenceHandle,
            tenantId,
            toolId,
            evidenceClass,
            string.IsNullOrWhiteSpace(contentIdentity) ? evidenceHandle : contentIdentity,
            payload,
            tokenCost,
            exactScore,
            lexicalScore,
            semanticScore,
            (entityScope ?? Array.Empty<string>())
                .OrderBy(e => e, StringComparer.Ordinal)
                .ToImmutableArray(),
            provenance);
    }
}

/// <summary>
/// A candidate pool that has already passed permission.
///
/// Constructible only from within this assembly's permission filter. Nothing else can
/// produce one, so no ranking, scoring, packing or counting path can be reached with
/// an unfiltered pool even by mistake.
/// </summary>
public sealed record PermittedCandidateSet
{
    internal PermittedCandidateSet(
        ImmutableArray<EvidenceCandidate> candidates,
        int rejectedByPermission)
    {
        Candidates = candidates;
        RejectedByPermission = rejectedByPermission;
    }

    public ImmutableArray<EvidenceCandidate> Candidates { get; }

    /// <summary>
    /// How many candidates permission removed.
    ///
    /// Deliberately NOT carried onto the evidence pack. It is available to a server
    /// operator inspecting this object and is never published downstream, because a
    /// count of what a caller may not see is itself a disclosure.
    /// </summary>
    public int RejectedByPermission { get; }

    public bool IsEmpty => Candidates.IsEmpty;
}

/// <summary>The declared budget. Reserved allowance is subtracted before anything is packed.</summary>
public sealed record TokenBudget(int TotalTokens, int ReservedAnswerTokens)
{
    public static TokenBudget Of(int totalTokens, int reservedAnswerTokens)
    {
        if (totalTokens < 0 || reservedAnswerTokens < 0)
        {
            throw new ArgumentException("A budget may not be negative.");
        }

        if (reservedAnswerTokens > totalTokens)
        {
            throw new ArgumentException(
                "The reserved answer allowance exceeds the whole budget, which would "
                    + "leave no room for the evidence the answer must be grounded in.");
        }

        return new TokenBudget(totalTokens, reservedAnswerTokens);
    }

    /// <summary>What evidence may actually occupy.</summary>
    public int AvailableForEvidence => TotalTokens - ReservedAnswerTokens;
}

/// <summary>Why a permitted candidate did not reach the pack.</summary>
public enum OmissionReason
{
    None = 0,
    CollapsedAsDuplicate = 1,
    ExceededRemainingBudget = 2,
    ExceedsWholeBudget = 3
}

/// <summary>One item in the pack, with the provenance a later verifier needs.</summary>
public sealed record EvidenceItem(
    string EvidenceHandle,
    ImmutableArray<string> MergedHandles,
    string ToolId,
    EvidenceClass Class,
    string ContentIdentity,
    string Payload,
    int TokenCost,
    double FusedScore,
    ImmutableArray<RetrievalSignal> ContributingSignals,
    ImmutableArray<string> EntityScope,
    string Provenance,
    int Rank)
{
    /// <summary>
    /// One source, however many handles pointed at it.
    ///
    /// Two retrievals of the same content are one piece of evidence. Counting them
    /// twice would let a single fact corroborate itself.
    /// </summary>
    public int DistinctSourceCount => 1;
}

/// <summary>A permitted candidate that was left out, and why.</summary>
public sealed record OmittedEvidence(
    string EvidenceHandle,
    EvidenceClass Class,
    int TokenCost,
    OmissionReason Reason);

/// <summary>
/// The bounded package.
///
/// Truncated says whether permitted evidence existed and did not fit. A downstream
/// reader can therefore tell "there is nothing" from "there is more", which are
/// different answers and must never look the same.
/// </summary>
public sealed record EvidencePack(
    RetrievalOutcome Outcome,
    ImmutableArray<EvidenceItem> Items,
    ImmutableArray<OmittedEvidence> Omitted,
    bool Truncated,
    int PermittedCandidateCount,
    int DistinctPermittedSourceCount,
    int TokensUsed,
    TokenBudget Budget,
    bool SemanticSignalAvailable,
    ImmutableArray<string> DegradedReasons,
    string TenantId,
    string IntentCode,
    ImmutableArray<string> PlannedToolIds,
    string RerankerIdentity,
    string Reason)
{
    public int OmittedForBudgetCount =>
        Omitted.Count(o => o.Reason is OmissionReason.ExceededRemainingBudget
            or OmissionReason.ExceedsWholeBudget);

    /// <summary>
    /// A canonical fingerprint of the package.
    ///
    /// Computed from permitted content only. Nothing a caller may not see contributes
    /// to it, so a fingerprint cannot become a channel for what was filtered out.
    /// </summary>
    public string PackFingerprint()
    {
        var builder = new StringBuilder();
        builder.Append("ppiq.assistant.evidence/1|");
        builder.Append(Outcome).Append('|');
        builder.Append(TenantId).Append('|');
        builder.Append(IntentCode).Append('|');
        builder.Append(string.Join(",", PlannedToolIds)).Append('|');
        builder.Append(Truncated).Append('|');
        builder.Append(TokensUsed).Append('/').Append(Budget.AvailableForEvidence).Append('|');
        builder.Append(SemanticSignalAvailable).Append('|');

        foreach (var item in Items)
        {
            builder.Append(item.Rank).Append(':').Append(item.EvidenceHandle).Append(':');
            builder.Append(item.ContentIdentity).Append(';');
        }

        builder.Append('|');
        foreach (var omitted in Omitted.OrderBy(o => o.EvidenceHandle, StringComparer.Ordinal))
        {
            builder.Append(omitted.EvidenceHandle).Append('=').Append(omitted.Reason).Append(';');
        }

        return builder.ToString();
    }
}

/// <summary>
/// The optional re-ranking seam, benchmarked under B-08.
///
/// A reranker reorders permitted candidates. It is never asked whether an item is
/// permitted, whether it fits the budget, or whether the pack is truncated. Those are
/// governance decisions and stay in deterministic code, because a component that could
/// change what a caller is allowed to see is not a ranking signal.
/// </summary>
public interface IEvidenceReranker
{
    string RerankerIdentity { get; }

    ImmutableArray<EvidenceCandidate> Rerank(
        ResolvedIntent intent,
        ImmutableArray<EvidenceCandidate> ordered);
}

/// <summary>The default seam. Present so that "with and without" is a real comparison.</summary>
public sealed class NoReranker : IEvidenceReranker
{
    public string RerankerIdentity => "none";

    public ImmutableArray<EvidenceCandidate> Rerank(
        ResolvedIntent intent,
        ImmutableArray<EvidenceCandidate> ordered) => ordered;
}
