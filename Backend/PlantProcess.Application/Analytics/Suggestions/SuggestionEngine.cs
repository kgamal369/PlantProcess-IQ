using System.Security.Cryptography;
using System.Text;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Analytics.Suggestions;

/// <summary>
/// PPIQ_REALIZATION_T047_DETERMINISTIC_SUGGESTION_WORKFLOW.
/// T-047: DETERMINISTIC suggestion generation. Same approved findings -> identical cards (stable ids and
/// order). Every card carries at least one resolvable evidence handle and a ranged impact; a finding with
/// no evidence handle is refused. Confidence is derived from data quality + sample size + stability.
/// The assistant may later explain a card, but cards are produced only here.
/// </summary>
public sealed class SuggestionEngine : ISuggestionEngine
{
    private const string Caveat =
        "Suggested from approved findings; impact is an estimated range, not a promise. Review before acting.";

    public IReadOnlyList<SuggestionCard> Generate(IReadOnlyList<ApprovedFinding> approvedFindings)
    {
        var cards = new List<SuggestionCard>();

        foreach (var finding in approvedFindings)
        {
            // Doctrine: no suggestion without resolvable evidence; seed rows never become live cards.
            if (finding.Handle is null || string.IsNullOrWhiteSpace(finding.Handle.Id) || finding.IsSynthetic)
                continue;

            var key = BuildKey(finding);
            var confidence = SuggestionConfidence.Compute(finding.DataQuality, finding.SampleSize, finding.Stability);

            cards.Add(new SuggestionCard(
                Id: DeterministicGuid(key),
                SuggestionKey: key,
                Title: BuildTitle(finding),
                ActionType: BuildActionType(finding.Kind),
                EvidenceHandles: new[] { finding.Handle },
                ImpactLow: finding.ImpactLow,
                ImpactHigh: finding.ImpactHigh,
                Confidence: confidence,
                HonestyText: Caveat,
                SourceFindingRefs: new[] { finding.FindingRef },
                Status: SuggestionStatus.Open,
                Population: finding.SampleSize,
                Method: finding.Method));
        }

        // Stable ordering: impact (high) desc, then confidence desc, then key asc.
        return cards
            .OrderByDescending(c => c.ImpactHigh ?? 0m)
            .ThenByDescending(c => c.Confidence)
            .ThenBy(c => c.SuggestionKey, StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildKey(ApprovedFinding f)
        => $"{f.Kind}|{f.Subject}|{f.DefectCode}|{f.FindingRef}";

    private static string BuildActionType(FindingKind kind) => kind switch
    {
        FindingKind.Risk        => "InvestigateRisk",
        FindingKind.Correlation => "ReviewAssociation",
        FindingKind.ValueImpact => "PrioritiseByValue",
        FindingKind.DataQuality => "FixDataQuality",
        FindingKind.Job         => "ReviewJobOutput",
        _                       => "Review"
    };

    private static string BuildTitle(ApprovedFinding f) => f.Kind switch
    {
        FindingKind.Risk        => $"Investigate elevated risk{Subject(f)}",
        FindingKind.Correlation => $"Review suspected association{Subject(f)}",
        FindingKind.ValueImpact => $"Prioritise by estimated value{Subject(f)}",
        FindingKind.DataQuality => $"Resolve data-quality issue{Subject(f)}",
        FindingKind.Job         => $"Review job output{Subject(f)}",
        _                       => $"Review finding{Subject(f)}"
    };

    private static string Subject(ApprovedFinding f)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.Subject)) parts.Add(f.Subject!);
        if (!string.IsNullOrWhiteSpace(f.DefectCode)) parts.Add(f.DefectCode!);
        return parts.Count == 0 ? "" : " on " + string.Join(" / ", parts);
    }

    /// <summary>PPIQ_REALIZATION_T047_DETERMINISTIC_SUGGESTION_WORKFLOW: MD5-stable card id from suggestion key.</summary>
    private static Guid DeterministicGuid(string key)
    {
        using var md5 = MD5.Create();
        return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes("ppiq-suggestion:" + key)));
    }
}