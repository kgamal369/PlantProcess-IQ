using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PlantProcess.Application.Assistant.Planning;

namespace PlantProcess.Application.Assistant.Retrieval;

/// <summary>
/// T-180. THE PACKER. Deduplicate, budget, disclose.
///
/// THE ORDER OF THE WORK.
///
///     executable plan  ->  permission  ->  fuse and order  ->  optional rerank
///                      ->  deduplicate  ->  budget  ->  disclose
///
/// DEDUPLICATION KEEPS EVERY HANDLE. The same content retrieved twice is one piece of
/// evidence, and it is collapsed. Both handles are kept on the surviving item, because
/// a later verifier may cite either one and must be able to resolve it. What is not
/// kept is the impression of two sources: the merged item reports one distinct source,
/// so a single fact can never appear to corroborate itself.
///
/// THE BUDGET IS A TOKEN BUDGET WITH AN ANSWER ALLOWANCE. The reserved allowance is
/// subtracted before anything is packed. Filling the whole window with evidence and
/// leaving no room to answer produces a truncated answer about complete evidence,
/// which is worse than complete evidence about a smaller question.
///
/// WHAT HAPPENS WHEN SOMETHING DOES NOT FIT. It is skipped, recorded with a reason,
/// and packing continues with the next item. The alternative, stopping at the first
/// item that does not fit, would let one oversized structured result empty the entire
/// pack. Both are deterministic; this one is declared, and it is disclosed per item.
///
/// TRUNCATION IS NEVER SILENT. If permitted evidence existed and did not fit,
/// Truncated is true. A downstream reader can tell "there is nothing" from "there is
/// more", and those are different answers.
///
/// NOTHING HERE CONSULTS A MODEL. Permission, deduplication, budgeting, tie-breaking
/// and truncation are deterministic code. The reranker seam may reorder permitted
/// candidates and is never asked any of those questions.
/// </summary>
public static class EvidencePacker
{
    /// <summary>Run the whole layer for one plan and one candidate producer.</summary>
    public static EvidencePack Pack(
        ToolPlan plan,
        IEnumerable<EvidenceCandidate> rawCandidates,
        TokenBudget budget,
        IEvidenceReranker? reranker = null,
        bool retrievalCapabilityAvailable = true)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(rawCandidates);
        ArgumentNullException.ThrowIfNull(budget);

        var seam = reranker ?? new NoReranker();

        if (!PermissionSafeCandidateFilter.IsExecutable(plan))
        {
            return Empty(
                RetrievalOutcome.PlanNotExecutable,
                plan,
                budget,
                seam.RerankerIdentity,
                semanticAvailable: false,
                degradedReasons: ImmutableArray<string>.Empty,
                reason: $"The plan is '{plan.Outcome}' with {plan.SelectedToolIds.Length} "
                    + "selected tool(s). This layer executes a planned tool set and never "
                    + "repairs, broadens or reinterprets one.");
        }

        if (!retrievalCapabilityAvailable)
        {
            return Empty(
                RetrievalOutcome.RetrievalUnavailable,
                plan,
                budget,
                seam.RerankerIdentity,
                semanticAvailable: false,
                degradedReasons: ImmutableArray.Create(
                    "No retrieval capability was available for this request."),
                reason: "Retrieval was unavailable, which is not the same as finding "
                    + "nothing and is never reported as an empty result.");
        }

        // PERMISSION FIRST. Nothing below can be reached without this.
        var permitted = PermissionSafeCandidateFilter.Filter(plan, rawCandidates);

        var semanticAvailable = HybridRanker.SemanticSignalPresent(permitted);
        var degraded = semanticAvailable
            ? ImmutableArray<string>.Empty
            : ImmutableArray.Create(
                "No permitted candidate carried a semantic score. The remaining signal "
                    + "weights were renormalised and no semantic score was fabricated.");

        if (permitted.IsEmpty)
        {
            return Empty(
                RetrievalOutcome.NoPermittedEvidence,
                plan,
                budget,
                seam.RerankerIdentity,
                semanticAvailable,
                degraded,
                "No evidence this caller is permitted to see matches the planned tools "
                    + "and resolved entities.");
        }

        var ranked = HybridRanker.Rank(permitted, semanticAvailable);

        // The seam may reorder. It cannot add, remove or admit anything: the result is
        // intersected back onto the ranked set by handle.
        var rerankedOrder = seam.Rerank(plan.Intent, ranked.Select(r => r.Candidate).ToImmutableArray());
        var byHandle = ranked.ToDictionary(r => r.Candidate.EvidenceHandle, r => r, StringComparer.Ordinal);
        var ordered = rerankedOrder
            .Where(c => byHandle.ContainsKey(c.EvidenceHandle))
            .Select(c => byHandle[c.EvidenceHandle])
            .ToList();

        foreach (var fused in ranked)
        {
            if (!ordered.Any(o => o.Candidate.EvidenceHandle == fused.Candidate.EvidenceHandle))
            {
                ordered.Add(fused);
            }
        }

        var (deduplicated, collapsed) = Deduplicate(ordered);
        var (items, omittedForBudget, tokensUsed) = ApplyBudget(deduplicated, budget);

        var omitted = collapsed
            .Concat(omittedForBudget)
            .OrderBy(o => o.EvidenceHandle, StringComparer.Ordinal)
            .ToImmutableArray();

        var truncated = omittedForBudget.Count > 0;

        return new EvidencePack(
            Outcome: RetrievalOutcome.EvidencePacked,
            Items: items,
            Omitted: omitted,
            Truncated: truncated,
            PermittedCandidateCount: permitted.Candidates.Length,
            DistinctPermittedSourceCount: deduplicated.Count,
            TokensUsed: tokensUsed,
            Budget: budget,
            SemanticSignalAvailable: semanticAvailable,
            DegradedReasons: degraded,
            TenantId: plan.TenantId,
            IntentCode: plan.Intent.IntentCode,
            PlannedToolIds: plan.SelectedToolIds,
            RerankerIdentity: seam.RerankerIdentity,
            Reason: truncated
                ? $"{items.Length} item(s) packed within {budget.AvailableForEvidence} token(s); "
                    + $"{omittedForBudget.Count} permitted item(s) did not fit and are disclosed."
                : $"{items.Length} item(s) packed within {budget.AvailableForEvidence} token(s). "
                    + "All permitted evidence fits.");
    }

    /// <summary>
    /// Collapse identical content, keeping every handle on the survivor.
    ///
    /// The survivor is the first occurrence in ranked order, so which one survives is
    /// a consequence of the ranking rather than of dictionary iteration.
    /// </summary>
    private static (List<PackableItem> Kept, List<OmittedEvidence> Collapsed) Deduplicate(
        IReadOnlyList<HybridRanker.FusedCandidate> ordered)
    {
        var kept = new List<PackableItem>();
        var collapsed = new List<OmittedEvidence>();
        var byContent = new Dictionary<string, PackableItem>(StringComparer.Ordinal);

        foreach (var fused in ordered)
        {
            var identity = fused.Candidate.ContentIdentity;
            if (byContent.TryGetValue(identity, out var survivor))
            {
                survivor.MergedHandles.Add(fused.Candidate.EvidenceHandle);
                collapsed.Add(new OmittedEvidence(
                    fused.Candidate.EvidenceHandle,
                    fused.Candidate.Class,
                    fused.Candidate.TokenCost,
                    OmissionReason.CollapsedAsDuplicate));
                continue;
            }

            var item = new PackableItem(fused);
            byContent[identity] = item;
            kept.Add(item);
        }

        foreach (var item in kept)
        {
            item.MergedHandles.Sort(StringComparer.Ordinal);
        }

        return (kept, collapsed);
    }

    private static (ImmutableArray<EvidenceItem> Items, List<OmittedEvidence> Omitted, int TokensUsed)
        ApplyBudget(IReadOnlyList<PackableItem> candidates, TokenBudget budget)
    {
        var available = budget.AvailableForEvidence;
        var used = 0;
        var rank = 0;
        var items = new List<EvidenceItem>();
        var omitted = new List<OmittedEvidence>();

        foreach (var candidate in candidates)
        {
            var cost = candidate.Fused.Candidate.TokenCost;

            if (cost > available)
            {
                omitted.Add(new OmittedEvidence(
                    candidate.Fused.Candidate.EvidenceHandle,
                    candidate.Fused.Candidate.Class,
                    cost,
                    OmissionReason.ExceedsWholeBudget));
                continue;
            }

            if (used + cost > available)
            {
                omitted.Add(new OmittedEvidence(
                    candidate.Fused.Candidate.EvidenceHandle,
                    candidate.Fused.Candidate.Class,
                    cost,
                    OmissionReason.ExceededRemainingBudget));
                continue;
            }

            used += cost;
            items.Add(new EvidenceItem(
                EvidenceHandle: candidate.Fused.Candidate.EvidenceHandle,
                MergedHandles: candidate.MergedHandles.ToImmutableArray(),
                ToolId: candidate.Fused.Candidate.ToolId,
                Class: candidate.Fused.Candidate.Class,
                ContentIdentity: candidate.Fused.Candidate.ContentIdentity,
                Payload: candidate.Fused.Candidate.Payload,
                TokenCost: cost,
                FusedScore: candidate.Fused.FusedScore,
                ContributingSignals: candidate.Fused.ContributingSignals,
                EntityScope: candidate.Fused.Candidate.EntityScope,
                Provenance: candidate.Fused.Candidate.Provenance,
                Rank: rank));
            rank++;
        }

        return (items.ToImmutableArray(), omitted, used);
    }

    private static EvidencePack Empty(
        RetrievalOutcome outcome,
        ToolPlan plan,
        TokenBudget budget,
        string rerankerIdentity,
        bool semanticAvailable,
        ImmutableArray<string> degradedReasons,
        string reason) =>
        new(
            outcome,
            ImmutableArray<EvidenceItem>.Empty,
            ImmutableArray<OmittedEvidence>.Empty,
            Truncated: false,
            PermittedCandidateCount: 0,
            DistinctPermittedSourceCount: 0,
            TokensUsed: 0,
            Budget: budget,
            SemanticSignalAvailable: semanticAvailable,
            DegradedReasons: degradedReasons,
            TenantId: plan.TenantId,
            IntentCode: plan.Intent.IntentCode,
            PlannedToolIds: plan.SelectedToolIds,
            RerankerIdentity: rerankerIdentity,
            Reason: reason);

    /// <summary>A ranked candidate plus the handles that merged into it.</summary>
    private sealed class PackableItem
    {
        public PackableItem(HybridRanker.FusedCandidate fused)
        {
            Fused = fused;
            MergedHandles = new List<string> { fused.Candidate.EvidenceHandle };
        }

        public HybridRanker.FusedCandidate Fused { get; }

        public List<string> MergedHandles { get; }
    }
}
