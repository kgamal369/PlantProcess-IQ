// Causal Confidence Ladder evaluator.
//
// Backlog origin: T-220.
//
// Walks the ladder from the bottom and stops at the first rung the evidence does not
// support. That stopping point is the answer: the highest contiguous supported level,
// with the requirement that would unlock the next one.
//
// The walk is deliberately not a search for the best available level. Evidence for a
// high rung with a gap beneath it earns the level below the gap, because a mechanism
// nobody has shown to precede its effect is not a supported cause; it is a hypothesis
// wearing one.
//
// L5 has no computational path. It is reached only by a governed external confirmation
// carrying its own handle, on top of a complete L0 to L4 chain.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Kernel;

public static class CausalConfidenceKernel
{
    /// <summary>
    /// Assess how strongly the evidence supports a causal claim. Every evidence argument
    /// is optional: absence is a normal answer and stops the walk rather than failing it.
    /// </summary>
    public static CausalConfidenceAssessment Assess(
        CausalEvidencePolicyRegistry policies,
        string? policyKey,
        GovernedReconciliationFinding? finding,
        StatisticalAssociationEvidence? statistical = null,
        TemporalPrecedenceEvidence? temporal = null,
        MechanisticEvidence? mechanistic = null,
        GovernedExternalConfirmation? confirmation = null)
    {
        ArgumentNullException.ThrowIfNull(policies);

        if (!policies.TryGetPolicy(policyKey, out var policy) || policy is null)
        {
            return Refuse(CausalConfidenceCodes.PolicyNotDeclared);
        }

        var handles = finding?.EvidenceHandles ?? Array.Empty<EvidenceHandle>();

        // L0. A governed observed fact with a handle behind it. No inference.
        var hasObservedFact = finding is not null
            && finding.CausalInput.AuthorityEstablished
            && finding.CausalInput.ObservedFactPresent
            && handles.Count > 0;

        if (!hasObservedFact)
        {
            return Unsupported(handles, Require(CausalConfidenceLevel.ObservedFact,
                CausalConfidenceCodes.RequiresObservedFact));
        }

        // L1. An actual discrepancy. Missing evidence and temporal uncertainty are
        // distinct states upstream and neither of them is a discrepancy.
        var hasDiscrepancy = finding!.CausalInput.DiscrepancyPresent && finding.Discrepancy is not null;

        if (!hasDiscrepancy)
        {
            return Supported(CausalConfidenceLevel.ObservedFact, handles, confirmation,
                Require(CausalConfidenceLevel.Discrepancy, CausalConfidenceCodes.RequiresDiscrepancy));
        }

        // L2. Governed statistical evidence, consumed whole. Nothing is recomputed, and a
        // q-value that has not been corrected for multiplicity does not count.
        var hasAssociation = statistical is not null
            && statistical.MultiplicityCorrectionApplied
            && !double.IsNaN(statistical.QValue)
            && statistical.QValue <= policy.MaximumQValue
            && Math.Abs(statistical.EffectSize) >= policy.MinimumEffectSize
            && statistical.SupportCount >= policy.MinimumSupportCount
            && !string.IsNullOrWhiteSpace(statistical.EvidenceHandleKey);

        if (!hasAssociation)
        {
            return Supported(CausalConfidenceLevel.Discrepancy, handles, confirmation,
                Require(CausalConfidenceLevel.StatisticalAssociation, CausalConfidenceCodes.RequiresStatisticalAssociation));
        }

        // L3. Consistent precedence across occurrences, and the records must have been
        // placeable in time in the first place.
        var temporallyQualified = finding.CausalInput.TemporallyQualified;

        var hasPrecedence = temporallyQualified
            && temporal is not null
            && temporal.OccurrenceCount >= policy.MinimumPrecedenceOccurrences
            && temporal.ConsistentPrecedenceCount == temporal.OccurrenceCount
            && !string.IsNullOrWhiteSpace(temporal.EvidenceHandleKey);

        if (!hasPrecedence)
        {
            return Supported(CausalConfidenceLevel.StatisticalAssociation, handles, confirmation,
                Require(CausalConfidenceLevel.TemporallySupportedHypothesis, CausalConfidenceCodes.RequiresTemporalPrecedence));
        }

        // L4. Independent fact kinds aligning on one named mechanism. Correlation plus
        // time is not mechanism.
        var independentKinds = mechanistic?.IndependentFactKinds?
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.Ordinal)
            .Count() ?? 0;

        var hasMechanism = mechanistic is not null
            && !string.IsNullOrWhiteSpace(mechanistic.MechanismCode)
            && independentKinds >= policy.MinimumIndependentFactKinds
            && !string.IsNullOrWhiteSpace(mechanistic.EvidenceHandleKey);

        if (!hasMechanism)
        {
            return Supported(CausalConfidenceLevel.TemporallySupportedHypothesis, handles, confirmation,
                Require(CausalConfidenceLevel.MechanisticallySupportedHypothesis, CausalConfidenceCodes.RequiresMechanism));
        }

        // L5. Never computed. Somebody outside this system checked, and the handle is
        // what proves it.
        var hasConfirmation = confirmation is not null
            && !string.IsNullOrWhiteSpace(confirmation.ConfirmationCode)
            && !string.IsNullOrWhiteSpace(confirmation.ConfirmationAuthorityKey)
            && !string.IsNullOrWhiteSpace(confirmation.EvidenceHandleKey);

        if (!hasConfirmation)
        {
            return Supported(CausalConfidenceLevel.MechanisticallySupportedHypothesis, handles, confirmation,
                Require(CausalConfidenceLevel.ConfirmedCause, CausalConfidenceCodes.RequiresExternalConfirmation));
        }

        return Supported(CausalConfidenceLevel.ConfirmedCause, handles, confirmation);
    }

    private static IReadOnlyList<CausalEvidenceRequirement> Require(CausalConfidenceLevel level, string code) =>
        new[] { new CausalEvidenceRequirement(level, code, DescriptionFor(code)) };

    private static CausalConfidenceAssessment Supported(
        CausalConfidenceLevel level,
        IReadOnlyList<EvidenceHandle> handles,
        GovernedExternalConfirmation? confirmation,
        IReadOnlyList<CausalEvidenceRequirement>? missing = null) =>
        new(IsSupported: true,
            level,
            handles,
            missing ?? Array.Empty<CausalEvidenceRequirement>(),
            level == CausalConfidenceLevel.ConfirmedCause
                ? CausalClaimClass.ConfirmedCause
                : CausalClaimClass.StrongestSupportedHypothesis,
            // The handle travels only when the confirmation actually established the
            // level. A confirmation attached to a weak chain proves nothing and is not
            // reported as though it did.
            level == CausalConfidenceLevel.ConfirmedCause ? confirmation?.EvidenceHandleKey : null,
            CodeFor(level),
            TerminalState.Finding,
            ExclusionAttribution.None);

    private static CausalConfidenceAssessment Unsupported(
        IReadOnlyList<EvidenceHandle> handles,
        IReadOnlyList<CausalEvidenceRequirement> missing) =>
        new(IsSupported: false,
            CausalConfidenceLevel.ObservedFact,
            handles,
            missing,
            CausalClaimClass.NoClaim,
            ExternalConfirmationHandle: null,
            CausalConfidenceCodes.NoSupportedLevel,
            TerminalState.InsufficientData,
            ExclusionAttribution.Data);

    private static CausalConfidenceAssessment Refuse(string code) =>
        new(IsSupported: false,
            CausalConfidenceLevel.ObservedFact,
            Array.Empty<EvidenceHandle>(),
            Array.Empty<CausalEvidenceRequirement>(),
            CausalClaimClass.NoClaim,
            ExternalConfirmationHandle: null,
            code,
            TerminalState.RefusedByGuard,
            ExclusionAttribution.Declaration);

    private static string CodeFor(CausalConfidenceLevel level) => level switch
    {
        CausalConfidenceLevel.ObservedFact => CausalConfidenceCodes.ObservedFact,
        CausalConfidenceLevel.Discrepancy => CausalConfidenceCodes.Discrepancy,
        CausalConfidenceLevel.StatisticalAssociation => CausalConfidenceCodes.StatisticalAssociation,
        CausalConfidenceLevel.TemporallySupportedHypothesis => CausalConfidenceCodes.TemporallySupportedHypothesis,
        CausalConfidenceLevel.MechanisticallySupportedHypothesis => CausalConfidenceCodes.MechanisticallySupportedHypothesis,
        _ => CausalConfidenceCodes.ConfirmedCause
    };

    private static string DescriptionFor(string requirementCode) => requirementCode switch
    {
        CausalConfidenceCodes.RequiresObservedFact => "a governed observed fact with at least one evidence handle",
        CausalConfidenceCodes.RequiresDiscrepancy => "discrepancy evidence; missing or temporally uncertain evidence is not a discrepancy",
        CausalConfidenceCodes.RequiresStatisticalAssociation => "governed association evidence: effect size, support count and a multiplicity-corrected q-value",
        CausalConfidenceCodes.RequiresTemporalPrecedence => "consistent precedence across the declared minimum number of occurrences",
        CausalConfidenceCodes.RequiresMechanism => "independent fact kinds aligning on one named mechanism",
        _ => "a governed external confirmation carrying its own evidence handle"
    };
}

/// <summary>
/// What may be said at a given level.
///
/// <para>
/// This is a consumer of level and never a producer of one. It takes a level that was
/// decided from evidence and answers whether a phrase is permitted; it returns no level,
/// and no wording can raise or lower one. Below Confirmed Cause the honest framing is the
/// strongest supported hypothesis, because that is exactly what the evidence supports.
/// </para>
/// </summary>
public static class CausalClaimLanguagePolicy
{
    private static readonly string[] ConfirmationPhrases =
    {
        "confirmed root cause",
        "confirmed cause",
        "proven cause",
        "definitely caused by",
        "proves that",
        "root cause confirmed"
    };

    public static CausalClaimClass ClaimClassFor(CausalConfidenceLevel level) =>
        level == CausalConfidenceLevel.ConfirmedCause
            ? CausalClaimClass.ConfirmedCause
            : CausalClaimClass.StrongestSupportedHypothesis;

    public static CausalClaimLanguageVerdict Validate(CausalConfidenceLevel level, string? phrasing)
    {
        var text = phrasing ?? string.Empty;

        if (level == CausalConfidenceLevel.ConfirmedCause)
        {
            return new CausalClaimLanguageVerdict(true, CausalConfidenceCodes.ClaimLanguagePermitted, string.Empty);
        }

        foreach (var forbidden in ConfirmationPhrases)
        {
            if (text.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new CausalClaimLanguageVerdict(false, CausalConfidenceCodes.ClaimLanguageForbidden, forbidden);
            }
        }

        return new CausalClaimLanguageVerdict(true, CausalConfidenceCodes.ClaimLanguagePermitted, string.Empty);
    }

    public static CausalClaimLanguageVerdict Validate(CausalConfidenceAssessment assessment, string? phrasing)
    {
        ArgumentNullException.ThrowIfNull(assessment);

        if (!assessment.IsSupported)
        {
            // Nothing is supported, so no causal phrasing is available at all.
            return string.IsNullOrWhiteSpace(phrasing)
                ? new CausalClaimLanguageVerdict(true, CausalConfidenceCodes.ClaimLanguagePermitted, string.Empty)
                : new CausalClaimLanguageVerdict(false, CausalConfidenceCodes.ClaimLanguageForbidden, phrasing!.Trim());
        }

        return Validate(assessment.SupportedLevel, phrasing);
    }
}