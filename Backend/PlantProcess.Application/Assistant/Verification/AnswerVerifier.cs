using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PlantProcess.Application.Assistant.Planning;
using PlantProcess.Application.Assistant.Retrieval;

namespace PlantProcess.Application.Assistant.Verification;

/// <summary>
/// T-181. THE DETERMINISTIC ANSWER VERIFIER.
///
/// It runs after phrasing and before display, and it calls no model.
///
/// THE ORDER OF THE CHECKS.
///
///     transport  ->  refusal  ->  ledger integrity  ->  per claim  ->  rendered text
///
/// Transport comes first because a request that never completed cannot have a
/// conclusion to verify, and the one thing that must never happen is a failure to
/// execute being rendered as a finding about the plant.
///
/// Refusal comes second because a governed refusal replaced by a plausible answer is
/// the failure that does the most damage: it is fluent, it is confident, and nothing
/// about it looks wrong.
///
/// THE HARD MINIMUM AND WHAT IS BEYOND IT. Every numeric claim must resolve to a cited
/// handle that actually supports that value, that unit and that quantity. A citation
/// that exists is not a citation that supports: a real handle attached to an unrelated
/// number is refused by name. Beyond numbers, a material claim with no resolving
/// evidence is refused too, because a sentence does not become true by omitting a
/// figure.
///
/// THE TEXT IS INSPECTED, NOT JUST THE LEDGER. A well-formed ledger beside prose that
/// says something else is the most dangerous shape of this failure, since everything
/// structured about it is correct.
/// </summary>
public static class AnswerVerifier
{
    /// <summary>Numbers rendered in prose. Deliberately simple and deliberately declared.</summary>
    private static readonly Regex NumberToken = new(
        @"(?<![\w.])[-+]?\d+(?:[.,]\d+)?(?![\w])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// A value matches when it agrees to this relative tolerance.
    ///
    /// Declared rather than measured. Rendering a stored double loses precision, and
    /// demanding exact equality would reject correct answers for a formatting reason.
    /// </summary>
    public const double RelativeValueTolerance = 1e-9;

    public static VerificationReport Verify(
        AnswerDraft draft,
        EvidencePack pack,
        EvidenceLedger ledger)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(ledger);

        var findings = new List<VerificationFinding>();
        var packHandles = PackHandles(pack);

        // ---------------------------------------------------------- transport
        if (draft.Transport == TransportState.Failed)
        {
            foreach (var phrase in ClaimPhrasePolicy.DomainConclusionPhrases(draft.Language))
            {
                if (draft.Text.ToLowerInvariant().Contains(phrase, StringComparison.Ordinal))
                {
                    findings.Add(new VerificationFinding(
                        VerificationCode.TransportFailurePresentedAsConclusion,
                        "-",
                        $"The request did not complete, and the text says '{phrase}'. A "
                            + "failure to execute is not a finding about the plant."));
                }
            }

            if (!draft.Claims.IsEmpty)
            {
                findings.Add(new VerificationFinding(
                    VerificationCode.TransportFailurePresentedAsConclusion,
                    "-",
                    $"The request did not complete and the draft asserts "
                        + $"{draft.Claims.Length} claim(s). Nothing was retrieved to assert."));
            }

            return new VerificationReport(
                VerificationVerdict.SystemFailure,
                findings.ToImmutableArray(),
                draft.EngineOutcome,
                draft.Transport,
                draft.Language,
                draft.Claims.Length,
                draft.Claims.Count(c => c.Kind == ClaimKind.Numeric),
                "The request did not complete. This is a system failure and is never "
                    + "reported as an absence of evidence, a lack of relationship or an "
                    + "absence of risk.");
        }

        // ------------------------------------------------------------ refusal
        if (draft.EngineOutcome != EngineOutcome.Answered)
        {
            if (!draft.Claims.IsEmpty)
            {
                findings.Add(new VerificationFinding(
                    VerificationCode.GovernedRefusalReplacedByAnswer,
                    "-",
                    $"The engine outcome is {draft.EngineOutcome} and the draft asserts "
                        + $"{draft.Claims.Length} claim(s). A model may phrase a refusal "
                        + "and may never replace one."));
            }

            var markers = ClaimPhrasePolicy.RefusalMarkers(draft.Language);
            var lowered = draft.Text.ToLowerInvariant();
            if (!markers.Any(m => lowered.Contains(m, StringComparison.Ordinal)))
            {
                findings.Add(new VerificationFinding(
                    VerificationCode.GovernedRefusalReplacedByAnswer,
                    "-",
                    $"The engine outcome is {draft.EngineOutcome} and the rendered text "
                        + "carries no refusal marker, so the refusal has been erased."));
            }
        }

        // --------------------------------------------------- ledger integrity
        foreach (var fact in ledger.Facts)
        {
            if (!packHandles.Contains(fact.EvidenceHandle))
            {
                findings.Add(new VerificationFinding(
                    VerificationCode.LedgerHandleNotInEvidencePack,
                    "-",
                    $"The ledger declares facts for handle '{fact.EvidenceHandle}', which "
                        + "the permission-filtered evidence pack does not contain. A "
                        + "ledger may describe the pack and may never extend it."));
            }
        }

        // --------------------------------------------------------- per claim
        foreach (var claim in draft.Claims.OrderBy(c => c.ClaimId, StringComparer.Ordinal))
        {
            VerifyClaim(claim, draft, packHandles, ledger, findings);
        }

        // ------------------------------------------------------ rendered text
        VerifyRenderedText(draft, packHandles, findings);

        var ordered = findings
            .OrderBy(f => (int)f.Code)
            .ThenBy(f => f.ClaimId, StringComparer.Ordinal)
            .ToImmutableArray();

        var verdict = ordered.IsEmpty
            ? VerificationVerdict.Displayable
            : VerificationVerdict.Rejected;

        return new VerificationReport(
            verdict,
            ordered,
            draft.EngineOutcome,
            draft.Transport,
            draft.Language,
            draft.Claims.Length,
            draft.Claims.Count(c => c.Kind == ClaimKind.Numeric),
            verdict == VerificationVerdict.Displayable
                ? $"All {draft.Claims.Length} claim(s) resolve to supplied evidence and no "
                    + "phrasing exceeds its evidence class."
                : $"{ordered.Length} check(s) failed: "
                    + string.Join(", ", ordered.Select(f => f.Code.ToString()).Distinct())
                    + ". The draft is rejected before display.");
    }

    private static void VerifyClaim(
        AnswerClaim claim,
        AnswerDraft draft,
        ImmutableHashSet<string> packHandles,
        EvidenceLedger ledger,
        List<VerificationFinding> findings)
    {
        // Claim-class integrity applies to every claim, cited or not. Language cannot
        // raise a class whatever the evidence behind it.
        foreach (var violation in ClaimPhrasePolicy.Violations(claim.Class, draft.Language, claim.AssertedText))
        {
            findings.Add(new VerificationFinding(
                VerificationCode.ClaimClassUpgradedByPhrasing,
                claim.ClaimId,
                $"The claim is {claim.Class} and its text asserts {violation.AssertedAuthority} "
                    + $"through '{violation.Phrase}'. Evidence determines the class; language "
                    + "cannot raise it."));
        }

        if (claim.EvidenceHandles.IsEmpty)
        {
            findings.Add(new VerificationFinding(
                claim.Kind == ClaimKind.Numeric
                    ? VerificationCode.UncitedNumericClaim
                    : VerificationCode.UnsupportedMaterialClaim,
                claim.ClaimId,
                claim.Kind == ClaimKind.Numeric
                    ? "The claim asserts a number and cites no evidence."
                    : "The claim asserts a material fact and cites no evidence. A sentence "
                        + "does not become supported by omitting a figure."));
            return;
        }

        var fabricated = claim.EvidenceHandles
            .Where(h => !packHandles.Contains(h))
            .ToArray();

        foreach (var handle in fabricated)
        {
            findings.Add(new VerificationFinding(
                VerificationCode.FabricatedEvidenceHandle,
                claim.ClaimId,
                $"The claim cites '{handle}', which the evidence pack does not contain."));
        }

        if (claim.Kind != ClaimKind.Numeric)
        {
            return;
        }

        var resolvable = claim.EvidenceHandles.Where(packHandles.Contains).ToArray();
        if (resolvable.Length == 0)
        {
            return;
        }

        var facts = resolvable.SelectMany(ledger.For).ToArray();
        if (facts.Length == 0)
        {
            findings.Add(new VerificationFinding(
                VerificationCode.CitationDoesNotSupportClaim,
                claim.ClaimId,
                $"The cited handle(s) exist but declare no fact. A citation that exists is "
                    + "not a citation that supports."));
            return;
        }

        var quantityMatches = facts
            .Where(f => string.Equals(f.QuantityKind, claim.QuantityKind, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (quantityMatches.Length == 0)
        {
            findings.Add(new VerificationFinding(
                VerificationCode.QuantityKindMismatch,
                claim.ClaimId,
                $"The claim asserts a quantity of kind '{claim.QuantityKind}' and the cited "
                    + $"evidence declares only "
                    + string.Join(", ", facts.Select(f => f.QuantityKind).Distinct().OrderBy(q => q, StringComparer.Ordinal))
                    + ". A quantity answered in the wrong kind is not an inaccuracy; it is "
                    + "evidence the answer was never grounded."));
            return;
        }

        var unitMatches = quantityMatches
            .Where(f => string.Equals(f.Unit, claim.Unit, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (unitMatches.Length == 0)
        {
            findings.Add(new VerificationFinding(
                VerificationCode.UnitMismatch,
                claim.ClaimId,
                $"The claim asserts '{claim.Unit}' and the cited evidence declares "
                    + string.Join(", ", quantityMatches.Select(f => f.Unit).Distinct().OrderBy(u => u, StringComparer.Ordinal))
                    + "."));
            return;
        }

        var subjectMatches = unitMatches
            .Where(f => string.IsNullOrEmpty(claim.Subject)
                || string.Equals(f.Subject, claim.Subject, StringComparison.Ordinal))
            .ToArray();

        if (subjectMatches.Length == 0)
        {
            findings.Add(new VerificationFinding(
                VerificationCode.SubjectMismatch,
                claim.ClaimId,
                $"The claim is about '{claim.Subject}' and the cited evidence describes "
                    + string.Join(", ", unitMatches.Select(f => f.Subject).Distinct().OrderBy(s => s, StringComparer.Ordinal))
                    + "."));
            return;
        }

        if (!subjectMatches.Any(f => ValuesAgree(f.Value, claim.NumericValue!.Value)))
        {
            findings.Add(new VerificationFinding(
                VerificationCode.CitedValueDoesNotMatch,
                claim.ClaimId,
                $"The claim asserts {claim.RenderedValue()} and the cited evidence supports "
                    + string.Join(", ", subjectMatches
                        .Select(f => f.Value.ToString("0.############", CultureInfo.InvariantCulture))
                        .Distinct()
                        .OrderBy(v => v, StringComparer.Ordinal))
                    + "."));
        }
    }

    /// <summary>
    /// Inspect the rendered text for material the ledger never declared.
    ///
    /// Two checks, both deliberately narrow. A number in the prose that no claim
    /// declares, and a citation handle in the prose that the pack does not contain.
    /// Narrow because a deterministic reader of free text cannot do more without
    /// becoming the regex theatre this task was told to avoid, and stated as narrow
    /// rather than presented as complete coverage of the prose.
    /// </summary>
    private static void VerifyRenderedText(
        AnswerDraft draft,
        ImmutableHashSet<string> packHandles,
        List<VerificationFinding> findings)
    {
        var declaredValues = draft.Claims
            .Where(c => c.NumericValue.HasValue)
            .Select(c => c.RenderedValue())
            .ToImmutableHashSet(StringComparer.Ordinal);

        foreach (Match match in NumberToken.Matches(draft.Text))
        {
            var token = match.Value.Replace(",", ".", StringComparison.Ordinal).TrimStart('+');

            if (declaredValues.Contains(token))
            {
                continue;
            }

            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                && draft.Claims.Any(c => c.NumericValue.HasValue && ValuesAgree(c.NumericValue.Value, parsed)))
            {
                continue;
            }

            // A number inside a cited handle is part of an identifier, not an assertion.
            if (packHandles.Any(h => h.Contains(match.Value, StringComparison.Ordinal)))
            {
                continue;
            }

            findings.Add(new VerificationFinding(
                VerificationCode.UndeclaredNumberInText,
                "-",
                $"The rendered text contains the number '{match.Value}', which no claim "
                    + "declares. A structured ledger is not permission for the text to say "
                    + "something different."));
        }

        foreach (var citation in draft.Citations)
        {
            if (!packHandles.Contains(citation.EvidenceHandle))
            {
                findings.Add(new VerificationFinding(
                    VerificationCode.UndeclaredHandleInText,
                    citation.ClaimId,
                    $"The answer cites '{citation.EvidenceHandle}', which the evidence pack "
                        + "does not contain."));
            }
        }
    }

    /// <summary>Every handle the pack can resolve, including handles merged during packing.</summary>
    public static ImmutableHashSet<string> PackHandles(EvidencePack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        return pack.Items
            .SelectMany(item => item.MergedHandles.Append(item.EvidenceHandle))
            .ToImmutableHashSet(StringComparer.Ordinal);
    }

    private static bool ValuesAgree(double left, double right)
    {
        if (left.Equals(right))
        {
            return true;
        }

        var scale = Math.Max(Math.Abs(left), Math.Abs(right));
        return scale > 0.0 && Math.Abs(left - right) / scale <= RelativeValueTolerance;
    }
}
