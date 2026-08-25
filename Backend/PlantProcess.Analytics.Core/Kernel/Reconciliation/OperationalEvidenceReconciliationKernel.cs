// Operational Evidence Reconciliation kernel.
//
// Backlog origin: T-219.
//
// Establishes what several sources' assertions about one fact actually show, in a fixed
// order of precedence. The order is the contract: each question must be settled before
// the next can be asked, and asking them out of order is how uncertainty becomes
// conflict.
//
//   1. Could authority be established at all?      no -> Unresolved
//   2. Did every declared source speak?            no -> MissingEvidence
//   3. Can they be shown to describe one moment?   no -> TemporalUncertain
//   4. Do they describe different moments?         yes -> LikelyMisclassified
//   5. How far apart are the values?               -> Aligned, PartiallyAligned or
//                                                     ConflictingEvidence
//
// Steps one to four all sit above the value comparison, so a fact nobody could place in
// time, and a fact a declared source never spoke about, can never be reported as
// disagreement.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Kernel;

public static class OperationalEvidenceReconciliationKernel
{
    /// <summary>
    /// Reconcile the assertions offered about one fact at one moment.
    /// </summary>
    public static ReconciliationOutcome Reconcile(
        FactEvidenceAuthorityRegistry authority,
        TemporalAlignmentPolicyRegistry alignmentPolicies,
        string? alignmentPolicyKey,
        ValueAgreementPolicyRegistry agreementPolicies,
        string? agreementPolicyKey,
        string? factKey,
        DateTimeOffset asOf,
        IReadOnlyList<EvidenceAssertion> assertions)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(alignmentPolicies);
        ArgumentNullException.ThrowIfNull(agreementPolicies);
        ArgumentNullException.ThrowIfNull(assertions);

        if (!agreementPolicies.TryGetPolicy(agreementPolicyKey, out var agreement) || agreement is null)
        {
            return Refuse(ReconciliationCodes.AgreementPolicyNotDeclared, factKey);
        }

        var offered = assertions
            .Where(a => a is not null)
            .Select(a => new OfferedEvidence(a.FactKey, a.SourceKey, a.Quality, a.At.Instant))
            .ToArray();

        // Step 1. Authority first: without it there is nothing to reconcile against.
        var resolution = FactEvidenceAuthorityKernel.Resolve(authority, factKey, asOf, offered);

        if (!resolution.IsResolved)
        {
            // A declared authority that said nothing is missing evidence. An authority
            // that could not be established at all is unresolved. Neither is dissent.
            var state = resolution.Code == FactAuthorityCodes.PrimaryAuthorityUnavailable
                ? ReconciliationState.MissingEvidence
                : ReconciliationState.Unresolved;

            return Decide(state, factKey, Array.Empty<string>(), 0d);
        }

        var participating = new List<string> { resolution.Authority!.PrimarySourceKey };
        participating.AddRange(resolution.Authority.SupportingSourceKeys);
        participating.AddRange(resolution.Authority.CorroboratingSourceKeys);

        // Step 2. Every source with declared standing is expected to speak. One that did
        // not leaves the picture incomplete, which is a different thing from disagreeing.
        var expected = authority
            .BindingsAt(resolution.Authority.FactKey, asOf)
            .Where(b => b.Role != EvidenceRole.Irrelevant)
            .Select(b => b.SourceKey)
            .ToArray();

        if (expected.Any(s => !participating.Contains(s, StringComparer.Ordinal)))
        {
            return Decide(ReconciliationState.MissingEvidence, factKey, participating, 0d);
        }

        var relevant = assertions
            .Where(a => a is not null && participating.Contains(Normalise(a.SourceKey), StringComparer.Ordinal))
            .ToArray();

        if (relevant.Length < 2)
        {
            // One voice cannot agree or disagree with anything. The authority resolved and
            // spoke, and no second declared source exists to reconcile against.
            return Decide(ReconciliationState.MissingEvidence, factKey, participating, 0d);
        }

        var handles = Handles(authority, relevant, resolution.Authority.FactKey, asOf);

        // Step 3 and 4. Time before values, always.
        var alignment = TemporalAlignmentKernel.Align(
            alignmentPolicies, alignmentPolicyKey, relevant.Select(a => a.At).ToArray());

        if (!alignment.IsDecided)
        {
            return Decide(ReconciliationState.Unresolved, factKey, participating, 0d, handles);
        }

        var qualification = new TemporalQualification(
            alignment.Alignment,
            alignment.Separation?.Minimum ?? TimeSpan.Zero,
            alignment.Separation?.Maximum ?? TimeSpan.Zero);

        if (alignment.Alignment == TemporalAlignment.Indeterminate)
        {
            // It has not been shown these records describe one moment, so they have not
            // been shown to disagree. This is the law that must never be optimised away.
            return Decide(ReconciliationState.TemporalUncertain, factKey, participating,
                Divergence(relevant), handles, qualification);
        }

        if (alignment.Alignment == TemporalAlignment.Separated)
        {
            // They provably describe different moments while claiming the same one. The
            // likeliest reading is that a record is filed against the wrong moment, which
            // is a statement about filing and not about anybody's honesty.
            return Decide(ReconciliationState.LikelyMisclassified, factKey, participating,
                Divergence(relevant), handles, qualification);
        }

        // Step 5. Only now may values be compared.
        var widest = Divergence(relevant);
        var discrepancy = new DiscrepancyEvidence(widest, agreement.AgreementTolerance, agreement.MaterialDivergence);

        if (widest > agreement.MaterialDivergence)
        {
            return Decide(ReconciliationState.ConflictingEvidence, factKey, participating,
                widest, handles, qualification, discrepancy);
        }

        if (widest > agreement.AgreementTolerance)
        {
            return Decide(ReconciliationState.PartiallyAligned, factKey, participating,
                widest, handles, qualification, discrepancy);
        }

        return Decide(ReconciliationState.Aligned, factKey, participating, widest, handles, qualification, discrepancy);
    }

    private static IReadOnlyList<EvidenceHandle> Handles(
        FactEvidenceAuthorityRegistry authority,
        IReadOnlyList<EvidenceAssertion> assertions,
        string factKey,
        DateTimeOffset asOf) =>
        assertions
            .Select(a => new EvidenceHandle(
                factKey,
                Normalise(a.SourceKey),
                FactEvidenceAuthorityKernel.RoleOf(authority, factKey, a.SourceKey, asOf),
                a.At.Instant,
                a.At.Uncertainty,
                a.Value))
            .OrderBy(h => h.SourceKey, StringComparer.Ordinal)
            .ToArray();

    private static double Divergence(IReadOnlyList<EvidenceAssertion> assertions)
    {
        var widest = 0d;

        for (var i = 0; i < assertions.Count - 1; i++)
        {
            for (var j = i + 1; j < assertions.Count; j++)
            {
                var gap = Math.Abs(assertions[i].Value - assertions[j].Value);
                if (gap > widest) widest = gap;
            }
        }

        return widest;
    }

    private static string Normalise(string? key) =>
        DeclaredKey.TryNormalise(key, out var normalised) ? normalised : string.Empty;

    private static ReconciliationOutcome Decide(
        ReconciliationState state,
        string? factKey,
        IReadOnlyList<string> participating,
        double widest,
        IReadOnlyList<EvidenceHandle>? handles = null,
        TemporalQualification? temporal = null,
        DiscrepancyEvidence? discrepancy = null) =>
        new(IsReconciled: true,
            state,
            Normalise(factKey),
            participating,
            widest,
            handles ?? Array.Empty<EvidenceHandle>(),
            temporal,
            discrepancy,
            CodeFor(state),
            // Every one of the seven is a statement about the evidence, so all are
            // findings. None of them is a refusal to look.
            TerminalState.Finding,
            ExclusionAttribution.None);

    private static ReconciliationOutcome Refuse(string code, string? factKey) =>
        new(IsReconciled: false,
            ReconciliationState.Unresolved,
            Normalise(factKey),
            Array.Empty<string>(),
            0d,
            Array.Empty<EvidenceHandle>(),
            Temporal: null,
            Discrepancy: null,
            code,
            TerminalState.RefusedByGuard,
            ExclusionAttribution.Declaration);

    private static string CodeFor(ReconciliationState state) => state switch
    {
        ReconciliationState.Aligned => ReconciliationCodes.Aligned,
        ReconciliationState.PartiallyAligned => ReconciliationCodes.PartiallyAligned,
        ReconciliationState.ConflictingEvidence => ReconciliationCodes.ConflictingEvidence,
        ReconciliationState.LikelyMisclassified => ReconciliationCodes.LikelyMisclassified,
        ReconciliationState.TemporalUncertain => ReconciliationCodes.TemporalUncertain,
        ReconciliationState.MissingEvidence => ReconciliationCodes.MissingEvidence,
        _ => ReconciliationCodes.Unresolved
    };
}


/// <summary>
/// Projects a reconciliation outcome onto the governed result a downstream consumer
/// binds to. Deterministic and persistence-free: it adds no judgement, invents no
/// schema, and reads nothing the outcome does not already carry.
///
/// <para>
/// The causal inputs are facts, not a level. Whether the evidence supports a weak or a
/// strong causal claim is the ladder's judgement, made under its own governance; this
/// projection hands over what is deterministically true and stops there.
/// </para>
/// </summary>
public static class ReconciliationFindingProjector
{
    public static GovernedReconciliationFinding Project(ReconciliationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var from = outcome.EvidenceHandles.Count == 0
            ? default
            : outcome.EvidenceHandles.Min(h => h.At - h.Uncertainty);

        var to = outcome.EvidenceHandles.Count == 0
            ? default
            : outcome.EvidenceHandles.Max(h => h.At + h.Uncertainty);

        var primary = outcome.EvidenceHandles
            .FirstOrDefault(h => h.Role == EvidenceRole.Primary)?.SourceKey ?? string.Empty;

        var causal = new CausalConfidenceInput(
            AuthorityEstablished: outcome.IsReconciled && outcome.State != ReconciliationState.Unresolved,
            ObservedFactPresent: outcome.EvidenceHandles.Count > 0,
            DiscrepancyPresent: outcome.State is ReconciliationState.PartiallyAligned
                                             or ReconciliationState.ConflictingEvidence,
            TemporallyQualified: outcome.Temporal?.Alignment == TemporalAlignment.Coincident);

        return new GovernedReconciliationFinding(
            outcome.Code,
            outcome.State,
            outcome.FactKey,
            primary,
            from,
            to,
            outcome.EvidenceHandles,
            outcome.Temporal,
            outcome.Discrepancy,
            causal,
            outcome.Outcome,
            outcome.Attribution);
    }
}