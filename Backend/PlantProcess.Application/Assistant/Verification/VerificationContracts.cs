using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using PlantProcess.Application.Assistant.Planning;
using PlantProcess.Application.Assistant.Retrieval;

namespace PlantProcess.Application.Assistant.Verification;

/// <summary>
/// T-181. WHAT IS VERIFIED, AND WHY IT IS NOT VERIFIED FROM PROSE ALONE.
///
/// This layer runs after a model has phrased something and before anything is shown.
/// It calls no model. A verifier that asked a model whether a model's answer was
/// grounded would be asking the defendant to be the judge, and would inherit exactly
/// the failure it exists to catch.
///
/// So the input is typed. A draft carries a ledger of claims: what was asserted, of
/// which claim class, citing which evidence handles, with which numeric value, unit
/// and quantity. Everything checkable is checked against that ledger and against the
/// evidence pack it cites.
///
/// THE LEDGER IS NOT PERMISSION FOR THE TEXT TO SAY SOMETHING ELSE. The rendered text
/// is inspected too: a number in the prose that no claim declares, a handle that the
/// pack does not contain, phrasing that claims more authority than the evidence class
/// permits, or a refusal that has quietly become an answer. A well-formed ledger
/// beside prose that says something different is the most dangerous shape this
/// failure takes, because everything structured about it looks correct.
/// </summary>
public enum ClaimKind
{
    /// <summary>Asserts a number. The hard minimum for grounding.</summary>
    Numeric = 0,

    /// <summary>Asserts a material fact without a number. Also requires evidence.</summary>
    Material = 1
}

/// <summary>
/// What the governed engine concluded, before any phrasing.
///
/// These are the outcomes a model may phrase and may never replace.
/// </summary>
public enum EngineOutcome
{
    Answered = 0,
    Refused = 1,
    Blocked = 2,
    NoData = 3,
    InsufficientEvidence = 4,
    Unsupported = 5
}

/// <summary>
/// Whether the request completed at all.
///
/// Deliberately separate from <see cref="EngineOutcome"/>. "The engine looked and
/// found nothing" and "the call never completed" are different facts, and collapsing
/// them turns a failure to execute into a conclusion about the plant.
/// </summary>
public enum TransportState
{
    Completed = 0,
    Failed = 1
}

/// <summary>One asserted claim, with everything needed to check it.</summary>
public sealed record AnswerClaim(
    string ClaimId,
    ClaimKind Kind,
    ClaimClass Class,
    ImmutableArray<string> EvidenceHandles,
    double? NumericValue,
    string? Unit,
    string? QuantityKind,
    string Subject,
    string AssertedText)
{
    public static AnswerClaim Numeric(
        string claimId,
        ClaimClass claimClass,
        double value,
        string unit,
        string quantityKind,
        string subject,
        string assertedText,
        params string[] evidenceHandles) =>
        new(
            claimId,
            ClaimKind.Numeric,
            claimClass,
            evidenceHandles.ToImmutableArray(),
            value,
            unit,
            quantityKind,
            subject,
            assertedText);

    public static AnswerClaim Material(
        string claimId,
        ClaimClass claimClass,
        string subject,
        string assertedText,
        params string[] evidenceHandles) =>
        new(
            claimId,
            ClaimKind.Material,
            claimClass,
            evidenceHandles.ToImmutableArray(),
            null,
            null,
            null,
            subject,
            assertedText);

    /// <summary>The value as it would be rendered, for comparison with the prose.</summary>
    public string RenderedValue() =>
        NumericValue.HasValue
            ? NumericValue.Value.ToString("0.############", CultureInfo.InvariantCulture)
            : string.Empty;
}

/// <summary>A citation in the rendered answer.</summary>
public sealed record AnswerCitation(string CitationId, string EvidenceHandle, string ClaimId);

/// <summary>What a model produced, before anything is displayed.</summary>
public sealed record AnswerDraft(
    string Text,
    string Language,
    EngineOutcome EngineOutcome,
    TransportState Transport,
    string GovernedReason,
    ImmutableArray<AnswerClaim> Claims,
    ImmutableArray<AnswerCitation> Citations)
{
    public static AnswerDraft Create(
        string text,
        string language,
        EngineOutcome engineOutcome = EngineOutcome.Answered,
        TransportState transport = TransportState.Completed,
        string governedReason = "",
        IEnumerable<AnswerClaim>? claims = null,
        IEnumerable<AnswerCitation>? citations = null) =>
        new(
            text,
            language,
            engineOutcome,
            transport,
            governedReason,
            (claims ?? Array.Empty<AnswerClaim>()).ToImmutableArray(),
            (citations ?? Array.Empty<AnswerCitation>()).ToImmutableArray());
}

/// <summary>
/// One fact a piece of evidence actually supports.
///
/// The evidence pack carries payload text, which is not machine-checkable. A verifier
/// that read prose to decide whether a number was supported would be guessing. The
/// producer of the evidence therefore declares what each handle supports, and every
/// handle in this ledger must exist in the pack, so the ledger can add nothing the
/// permission-filtered pack did not already contain.
/// </summary>
public sealed record EvidenceFact(
    string EvidenceHandle,
    string QuantityKind,
    string Unit,
    double Value,
    string Subject);

/// <summary>The declared facts, keyed by handle. A subset of what the pack contains.</summary>
public sealed record EvidenceLedger(ImmutableArray<EvidenceFact> Facts)
{
    public static EvidenceLedger Of(params EvidenceFact[] facts) =>
        new(facts.ToImmutableArray());

    public IEnumerable<EvidenceFact> For(string handle) =>
        Facts.Where(f => string.Equals(f.EvidenceHandle, handle, StringComparison.Ordinal));
}

/// <summary>Every way a draft can fail. One code per failure mode, never merged.</summary>
public enum VerificationCode
{
    None = 0,
    UncitedNumericClaim = 1,
    FabricatedEvidenceHandle = 2,
    CitedValueDoesNotMatch = 3,
    UnitMismatch = 4,
    QuantityKindMismatch = 5,
    CitationDoesNotSupportClaim = 6,
    ClaimClassUpgradedByPhrasing = 7,
    GovernedRefusalReplacedByAnswer = 8,
    TransportFailurePresentedAsConclusion = 9,
    UnsupportedMaterialClaim = 10,
    UndeclaredNumberInText = 11,
    UndeclaredHandleInText = 12,
    LedgerHandleNotInEvidencePack = 13,
    SubjectMismatch = 14
}

/// <summary>What the verifier concluded about the whole draft.</summary>
public enum VerificationVerdict
{
    /// <summary>Every check passed. The draft may be displayed.</summary>
    Displayable = 0,

    /// <summary>At least one check failed. The draft is rejected before display.</summary>
    Rejected = 1,

    /// <summary>The request never completed. Not a conclusion about anything.</summary>
    SystemFailure = 2
}

/// <summary>One failed check, with the claim and the numbers behind it.</summary>
public sealed record VerificationFinding(
    VerificationCode Code,
    string ClaimId,
    string Detail)
{
    public override string ToString() => $"{Code} [{ClaimId}] {Detail}";
}

/// <summary>The verdict, every finding, and a fingerprint free of timestamps.</summary>
public sealed record VerificationReport(
    VerificationVerdict Verdict,
    ImmutableArray<VerificationFinding> Findings,
    EngineOutcome EngineOutcome,
    TransportState Transport,
    string Language,
    int CheckedClaimCount,
    int CheckedNumericClaimCount,
    string Reason)
{
    public bool HasCode(VerificationCode code) => Findings.Any(f => f.Code == code);

    public ImmutableArray<VerificationCode> Codes =>
        Findings.Select(f => f.Code).Distinct().OrderBy(c => (int)c).ToImmutableArray();

    /// <summary>Deterministic identity of the verdict. Carries no clock reading.</summary>
    public string ReportFingerprint()
    {
        var builder = new StringBuilder();
        builder.Append("ppiq.assistant.verification/1|");
        builder.Append(Verdict).Append('|');
        builder.Append(EngineOutcome).Append('|');
        builder.Append(Transport).Append('|');
        builder.Append(Language).Append('|');
        builder.Append(CheckedClaimCount).Append('/').Append(CheckedNumericClaimCount).Append('|');

        foreach (var finding in Findings)
        {
            builder.Append(finding.Code).Append(':').Append(finding.ClaimId).Append(';');
        }

        return builder.ToString();
    }
}
