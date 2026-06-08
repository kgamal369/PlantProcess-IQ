using System.Text.RegularExpressions;
using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Assistant;

/// <summary>
/// PPIQ_REALIZATION_T048_ASSISTANT_GROUNDING_EVAL_GATE.
/// T-048/T-053 grounding contract (§2/§7.7). Operates on a model DRAFT plus the grounded claims that back it:
///  - a sentence containing a NUMBER not authorized by a claim is BLOCKED (the number never reaches the client);
///  - sentences asserting causation ('root cause', 'is caused by') or forbidden value phrasing are BLOCKED;
///  - synthetic/seed-backed claims are dropped; if nothing grounded remains, the answer is an honest refusal.
/// The model never performs arithmetic: every surfaced number traces to a claim handle.
/// </summary>
public static class GroundingService
{
    private static readonly string[] Forbidden = { "root cause", "is caused by", "will cause", "guaranteed", "will save" };
    private static readonly Regex NumberRx = new("\\d[\\d.,]*", RegexOptions.Compiled);
    private static readonly Regex SentenceRx = new("(?<=[\\.\\!\\?])\\s+", RegexOptions.Compiled);

    public static GroundedAnswer Enforce(string? draft, IReadOnlyList<AssistantClaim> claims)
    {
        var live = claims
            .Where(c => !c.IsSynthetic && c.Handle is not null && !string.IsNullOrWhiteSpace(c.Handle.Id))
            .ToList();

        if (string.IsNullOrWhiteSpace(draft) || live.Count == 0)
            return GroundedAnswer.Refusal("I don't have enough approved evidence to answer that.");

        var allowed = live.SelectMany(c => c.NumericTokens.Select(Normalize)).ToHashSet();
        var kept = new List<string>();
        var blocked = new List<string>();
        var citations = new List<ProvenanceHandle>();

        foreach (var sentence in SentenceRx.Split(draft))
        {
            if (string.IsNullOrWhiteSpace(sentence)) continue;

            if (ContainsForbidden(sentence)) { blocked.Add(sentence); continue; }

            var numbers = NumberRx.Matches(sentence).Select(m => Normalize(m.Value)).ToList();
            if (numbers.Any(n => !allowed.Contains(n))) { blocked.Add(sentence); continue; } // uncited number

            kept.Add(sentence.Trim());
            foreach (var c in live)
            {
                var cTokens = c.NumericTokens.Select(Normalize).ToHashSet();
                var supportsNumber = numbers.Count > 0 && numbers.Any(cTokens.Contains);
                if ((numbers.Count == 0 || supportsNumber) && !citations.Contains(c.Handle))
                    citations.Add(c.Handle);
            }
        }

        if (kept.Count == 0)
            return GroundedAnswer.Refusal("The available evidence does not support a grounded answer.");

        return new GroundedAnswer(string.Join(" ", kept), citations, false, null, blocked);
    }

    private static bool ContainsForbidden(string sentence)
    {
        var s = Regex.Replace(sentence, "\\s+", " ").ToLowerInvariant();
        return Forbidden.Any(s.Contains);
    }

    private static string Normalize(string token) => token.Replace(",", "").TrimEnd('.');
}