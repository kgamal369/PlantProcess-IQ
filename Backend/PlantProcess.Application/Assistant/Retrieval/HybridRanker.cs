using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PlantProcess.Application.Assistant.Retrieval;

/// <summary>
/// T-180. HYBRID FUSION, AND WHAT HYBRID IS NOT ALLOWED TO MEAN.
///
/// Hybrid here means a declared weighted combination of declared signals: exact,
/// lexical and, where it exists, semantic. It does not mean a model deciding what
/// looks useful, a rewritten query, or an agent loop. Every number below can be
/// recomputed by hand from the inputs, which is what makes a ranking auditable.
///
/// CLASS BEFORE SCORE. Ordering is by evidence class first and fused score second. A
/// structured tool result is the value; a retrieved passage is text that mentions the
/// value. A well-worded passage out-scores an exact figure on any lexical measure and
/// is still the weaker evidence, so score is never allowed to reorder across classes.
///
/// THE DEGRADED PATH IS DECLARED, NOT IMPROVISED. When no candidate carries a semantic
/// score, the semantic weight is not silently treated as zero-scoring, which would
/// quietly rescale everything. The remaining weights are renormalised, the pack records
/// that the signal was unavailable, and no semantic number is fabricated.
/// </summary>
public static class HybridRanker
{
    /// <summary>Declared weights. Stated constants, not measured ones.</summary>
    public const double ExactWeight = 0.5;
    public const double LexicalWeight = 0.2;
    public const double SemanticWeight = 0.3;

    /// <summary>One ranked candidate and the arithmetic behind its position.</summary>
    public sealed record FusedCandidate(
        EvidenceCandidate Candidate,
        double FusedScore,
        ImmutableArray<RetrievalSignal> ContributingSignals);

    /// <summary>
    /// Fuse and order a permitted set. Requires a
    /// <see cref="PermittedCandidateSet"/>, so an unfiltered pool cannot be ranked.
    /// </summary>
    public static ImmutableArray<FusedCandidate> Rank(
        PermittedCandidateSet permitted,
        bool semanticSignalAvailable)
    {
        ArgumentNullException.ThrowIfNull(permitted);

        var fused = permitted.Candidates
            .Select(candidate => Fuse(candidate, semanticSignalAvailable))
            .ToArray();

        return fused
            .OrderBy(f => (int)f.Candidate.Class)
            .ThenByDescending(f => f.FusedScore)
            // The tie break is the handle, compared ordinally. Two candidates with
            // equal class and equal score must land in the same order on every run
            // and on every machine, and nothing else here is guaranteed stable.
            .ThenBy(f => f.Candidate.EvidenceHandle, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>Whether any permitted candidate carries a semantic score at all.</summary>
    public static bool SemanticSignalPresent(PermittedCandidateSet permitted)
    {
        ArgumentNullException.ThrowIfNull(permitted);
        return permitted.Candidates.Any(c => c.SemanticScore.HasValue);
    }

    private static FusedCandidate Fuse(EvidenceCandidate candidate, bool semanticAvailable)
    {
        var signals = new List<RetrievalSignal>();
        var weighted = 0.0;
        var weight = 0.0;

        if (candidate.ExactScore > 0.0)
        {
            signals.Add(RetrievalSignal.Exact);
        }

        weighted += ExactWeight * candidate.ExactScore;
        weight += ExactWeight;

        if (candidate.LexicalScore > 0.0)
        {
            signals.Add(RetrievalSignal.Lexical);
        }

        weighted += LexicalWeight * candidate.LexicalScore;
        weight += LexicalWeight;

        if (semanticAvailable && candidate.SemanticScore.HasValue)
        {
            signals.Add(RetrievalSignal.Semantic);
            weighted += SemanticWeight * candidate.SemanticScore.Value;
            weight += SemanticWeight;
        }

        // Renormalised over the signals that actually contributed. Without this, a
        // population with no semantic signal would score uniformly lower than one
        // with it, and two runs of the same retrieval would not be comparable.
        var fused = weight > 0.0 ? weighted / weight : 0.0;

        return new FusedCandidate(
            candidate,
            fused,
            signals.OrderBy(s => (int)s).ToImmutableArray());
    }
}
