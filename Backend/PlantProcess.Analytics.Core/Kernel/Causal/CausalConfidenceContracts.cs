// Causal Confidence Ladder contract.
//
// Backlog origin: T-220.
//
// Answers one question: how strongly does the evidence support a causal claim, and what
// is missing before it could support a stronger one?
//
// Six levels, and the ladder is cumulative. A higher rung cannot be reached by stepping
// over a missing lower one: mechanistic evidence without temporal support is not L4, it
// is whatever the last contiguous supported level was. The result is therefore the
// highest CONTIGUOUS supported level, and it always names what the next rung would
// require.
//
// L5 is never computed. Every other level is a reading of evidence; Confirmed Cause is a
// statement that somebody outside this system checked and said so, and it carries the
// handle proving it. Remove that confirmation and an otherwise L5 case deterministically
// becomes L4 - not because the evidence changed, but because the confirmation was the
// only thing that made it a confirmation.
//
// Language is a consumer of level, never a producer of one. Nothing in this contract
// lets a phrase choose a rung.
//
// Deliberately out of scope: recomputing statistics, reconciliation, persistence and any
// language model. Statistical, temporal and mechanistic evidence are consumed as
// governed inputs; this kernel decides only what they add up to.
using System;
using System.Collections.Generic;

namespace PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// The six rungs. There is no seventh, and no member for "nothing supported" - an
/// unsupported case is reported by the assessment rather than by inventing a level.
/// </summary>
public enum CausalConfidenceLevel
{
    ObservedFact = 0,
    Discrepancy = 1,
    StatisticalAssociation = 2,
    TemporallySupportedHypothesis = 3,
    MechanisticallySupportedHypothesis = 4,
    ConfirmedCause = 5
}

/// <summary>
/// What a consumer is permitted to say. The claim class follows the level; no phrasing
/// choice can move it.
/// </summary>
public enum CausalClaimClass
{
    NoClaim,
    StrongestSupportedHypothesis,
    ConfirmedCause
}

/// <summary>
/// Governed statistical evidence, consumed rather than computed here. A q-value that has
/// not been through multiplicity correction is not a q-value, so the correction is
/// declared explicitly rather than assumed.
/// </summary>
public sealed record StatisticalAssociationEvidence(
    double EffectSize,
    int SupportCount,
    double QValue,
    bool MultiplicityCorrectionApplied,
    string EvidenceHandleKey);

/// <summary>
/// Evidence that the candidate cause consistently precedes the effect. One coincident
/// interval is a coincidence; precedence has to hold across occurrences, and how many is
/// declared rather than guessed.
/// </summary>
public sealed record TemporalPrecedenceEvidence(
    int OccurrenceCount,
    int ConsistentPrecedenceCount,
    string EvidenceHandleKey);

/// <summary>
/// Evidence that independent kinds of fact point at the same mechanism. Correlation plus
/// time is not mechanism, so the independent fact kinds must be named.
/// </summary>
public sealed record MechanisticEvidence(
    string MechanismCode,
    IReadOnlyList<string> IndependentFactKinds,
    string EvidenceHandleKey);

/// <summary>
/// Somebody outside this system checked. The authority is an opaque declared key - a
/// role or process, never a person - and the handle is what makes the confirmation
/// auditable rather than asserted.
/// </summary>
public sealed record GovernedExternalConfirmation(
    string ConfirmationCode,
    string ConfirmationAuthorityKey,
    string EvidenceHandleKey,
    DateTimeOffset ConfirmedAt);

/// <summary>
/// One thing standing between the current level and the next.
/// </summary>
public sealed record CausalEvidenceRequirement(
    CausalConfidenceLevel ForLevel,
    string RequirementCode,
    string Description);

/// <summary>
/// The thresholds evidence must clear. Declared per policy, because what counts as a
/// meaningful effect or sufficient support differs by what is being claimed, and a
/// threshold this kernel chose would decide causation on the customer's behalf.
/// </summary>
public sealed record CausalEvidencePolicy(
    string PolicyKey,
    double MaximumQValue,
    double MinimumEffectSize,
    int MinimumSupportCount,
    int MinimumPrecedenceOccurrences,
    int MinimumIndependentFactKinds);

/// <summary>
/// The assessment. It reports the highest contiguous supported level, the handles behind
/// it, what the next rung needs, and what may therefore be said.
/// </summary>
public sealed record CausalConfidenceAssessment(
    bool IsSupported,
    CausalConfidenceLevel SupportedLevel,
    IReadOnlyList<EvidenceHandle> EvidenceHandles,
    IReadOnlyList<CausalEvidenceRequirement> MissingEvidenceForNextLevel,
    CausalClaimClass AllowedClaimClass,
    string? ExternalConfirmationHandle,
    string Code,
    TerminalState Outcome,
    ExclusionAttribution Attribution);

/// <summary>
/// The verdict on a phrase. Language is checked against a level that was decided
/// elsewhere; this carries no level of its own.
/// </summary>
public sealed record CausalClaimLanguageVerdict(
    bool IsPermitted,
    string Code,
    string OffendingPhrase);

/// <summary>
/// Level, requirement and refusal codes. Stable strings, so a consumer can branch on
/// them without parsing prose.
/// </summary>
public static class CausalConfidenceCodes
{
    public const string PolicyNotDeclared = "CL01 causal_evidence_policy_not_declared";
    public const string ConflictingDeclaration = "CL02 conflicting_declaration";
    public const string InvalidDeclaration = "CL03 invalid_declaration";
    public const string NoSupportedLevel = "CL04 no_supported_level";

    public const string ObservedFact = "CL10 observed_fact";
    public const string Discrepancy = "CL11 discrepancy";
    public const string StatisticalAssociation = "CL12 statistical_association";
    public const string TemporallySupportedHypothesis = "CL13 temporally_supported_hypothesis";
    public const string MechanisticallySupportedHypothesis = "CL14 mechanistically_supported_hypothesis";
    public const string ConfirmedCause = "CL15 confirmed_cause";

    public const string ClaimLanguagePermitted = "CL20 claim_language_permitted";
    public const string ClaimLanguageForbidden = "CL21 claim_language_forbidden";

    public const string RequiresObservedFact = "RQ00 governed_observed_fact_with_evidence_handle";
    public const string RequiresDiscrepancy = "RQ01 discrepancy_evidence";
    public const string RequiresStatisticalAssociation = "RQ02 governed_statistical_association_evidence";
    public const string RequiresTemporalPrecedence = "RQ03 consistent_temporal_precedence_across_occurrences";
    public const string RequiresMechanism = "RQ04 independent_fact_kinds_aligning_on_one_mechanism";
    public const string RequiresExternalConfirmation = "RQ05 governed_external_confirmation_with_handle";
}

/// <summary>
/// The causal evidence policies in force. Starts empty: no default threshold of any
/// kind, because a default would decide causation silently.
/// </summary>
public sealed class CausalEvidencePolicyRegistry
{
    private readonly Dictionary<string, CausalEvidencePolicy> _policies = new(StringComparer.Ordinal);

    public int PolicyCount => _policies.Count;

    public bool TryDeclarePolicy(CausalEvidencePolicy policy, out string code)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!DeclaredKey.TryNormalise(policy.PolicyKey, out var policyKey))
        {
            code = CausalConfidenceCodes.PolicyNotDeclared;
            return false;
        }

        if (double.IsNaN(policy.MaximumQValue) || policy.MaximumQValue <= 0d || policy.MaximumQValue > 1d ||
            double.IsNaN(policy.MinimumEffectSize) || policy.MinimumEffectSize < 0d ||
            policy.MinimumSupportCount <= 0 ||
            policy.MinimumPrecedenceOccurrences < 2 ||
            policy.MinimumIndependentFactKinds < 2)
        {
            // Precedence across fewer than two occurrences is a coincidence, and one fact
            // kind is not independent corroboration. Neither threshold may be declared
            // away.
            code = CausalConfidenceCodes.InvalidDeclaration;
            return false;
        }

        var normalised = policy with { PolicyKey = policyKey };

        if (_policies.TryGetValue(policyKey, out var existing))
        {
            if (existing == normalised)
            {
                code = string.Empty;
                return true;
            }

            code = CausalConfidenceCodes.ConflictingDeclaration;
            return false;
        }

        _policies[policyKey] = normalised;
        code = string.Empty;
        return true;
    }

    public bool TryGetPolicy(string? policyKey, out CausalEvidencePolicy? policy)
    {
        policy = null;

        if (!DeclaredKey.TryNormalise(policyKey, out var normalised)) return false;

        return _policies.TryGetValue(normalised, out policy);
    }
}