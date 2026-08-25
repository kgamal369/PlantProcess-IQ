// Operational Evidence Reconciliation contract.
//
// Backlog origin: T-219.
//
// Answers one question: when several declared sources speak about the same fact, what
// exactly does the evidence show?
//
// Seven states, and no eighth. Each says something different about the world, and
// collapsing any of them into ConflictingEvidence is the failure this contract exists to
// prevent:
//
//   Aligned              the sources agree within the declared tolerance
//   PartiallyAligned     they differ, but not materially
//   MissingEvidence      a declared source said nothing
//   TemporalUncertain    it cannot be established they describe the same moment
//   ConflictingEvidence  they materially disagree about the same moment
//   LikelyMisclassified  they describe different moments while claiming one
//   Unresolved           authority itself could not be established
//
// This is not lie detection. Nothing here reasons about who is right, who is careless or
// what anyone intended, and no member of the vocabulary permits it. A disagreement
// between a manual record and an instrument is a fact about the records, not about a
// person.
//
// Two laws hold precedence over everything else. Temporal uncertainty never becomes
// conflict: if it cannot be shown the records describe one moment, they have not been
// shown to disagree. Missing authority never becomes conflict: silence is not dissent.
//
// Deliberately out of scope: causal reasoning, confidence levels and persistence.
using System;
using System.Collections.Generic;

namespace PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// What the evidence shows. Exactly seven, and the set is closed.
/// </summary>
public enum ReconciliationState
{
    Unresolved,
    MissingEvidence,
    TemporalUncertain,
    LikelyMisclassified,
    ConflictingEvidence,
    PartiallyAligned,
    Aligned
}

/// <summary>
/// One source's assertion about a fact at an instant, with the quality declared for it.
/// </summary>
public sealed record EvidenceAssertion(
    string FactKey,
    string SourceKey,
    TemporalInstant At,
    double Value,
    double Quality);

/// <summary>
/// How far apart two assertions may be before they stop agreeing, and how far before
/// they materially disagree. Both bands are declared: what counts as the same reading
/// differs between a laboratory result and a shift count, and guessing it silently
/// decides whether the plant is in conflict with itself.
/// </summary>
public sealed record ValueAgreementPolicy(
    string PolicyKey,
    double AgreementTolerance,
    double MaterialDivergence);

/// <summary>
/// One record that took part, kept as a handle rather than a copy of the data. A
/// consumer can follow it back to the source; nothing downstream needs to re-derive
/// which evidence produced a state.
/// </summary>
public sealed record EvidenceHandle(
    string FactKey,
    string SourceKey,
    EvidenceRole Role,
    DateTimeOffset At,
    TimeSpan Uncertainty,
    double Value);

/// <summary>
/// How well the records could be placed in time. Carried on the result so that a
/// TemporalUncertain state is auditable rather than asserted.
/// </summary>
public sealed record TemporalQualification(
    TemporalAlignment Alignment,
    TimeSpan MinimumSeparation,
    TimeSpan MaximumSeparation);

/// <summary>
/// How far apart the records were, against the bands that were declared. Present only
/// once the values were actually compared.
/// </summary>
public sealed record DiscrepancyEvidence(
    double WidestDivergence,
    double AgreementTolerance,
    double MaterialDivergence);

/// <summary>
/// The deterministic facts a causal ladder consumes. These are inputs, not a level:
/// nothing here decides how strongly a cause is supported, and no member names one.
/// Promotion is the ladder's judgement, made elsewhere and under its own governance.
/// </summary>
public sealed record CausalConfidenceInput(
    bool AuthorityEstablished,
    bool ObservedFactPresent,
    bool DiscrepancyPresent,
    bool TemporallyQualified);

/// <summary>
/// The reconciliation outcome, with the widest divergence observed so a consumer can see
/// how close the call was. A refused reconciliation carries no state other than
/// Unresolved.
/// </summary>
public sealed record ReconciliationOutcome(
    bool IsReconciled,
    ReconciliationState State,
    string FactKey,
    IReadOnlyList<string> ParticipatingSourceKeys,
    double WidestDivergence,
    IReadOnlyList<EvidenceHandle> EvidenceHandles,
    TemporalQualification? Temporal,
    DiscrepancyEvidence? Discrepancy,
    string Code,
    TerminalState Outcome,
    ExclusionAttribution Attribution);

/// <summary>
/// The governed result a downstream consumer binds to. Deterministic and
/// persistence-free: it is a projection of the outcome, carrying identity, effective
/// interval, evidence handles, temporal qualification, discrepancy evidence and the
/// causal inputs. It exists so that consumers need not invent a schema of their own.
/// </summary>
public sealed record GovernedReconciliationFinding(
    string FindingCode,
    ReconciliationState State,
    string FactKey,
    string PrimarySourceKey,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveTo,
    IReadOnlyList<EvidenceHandle> EvidenceHandles,
    TemporalQualification? Temporal,
    DiscrepancyEvidence? Discrepancy,
    CausalConfidenceInput CausalInput,
    TerminalState Outcome,
    ExclusionAttribution Attribution);

/// <summary>
/// State and refusal codes. Stable strings, so a consumer can branch on them without
/// parsing prose.
/// </summary>
public static class ReconciliationCodes
{
    public const string AgreementPolicyNotDeclared = "RC01 agreement_policy_not_declared";
    public const string InsufficientAssertions = "RC02 insufficient_assertions";
    public const string ConflictingDeclaration = "RC03 conflicting_declaration";
    public const string InvalidDeclaration = "RC04 invalid_declaration";

    public const string Unresolved = "RC10 unresolved";
    public const string MissingEvidence = "RC11 missing_evidence";
    public const string TemporalUncertain = "RC12 temporal_uncertain";
    public const string LikelyMisclassified = "RC13 likely_misclassified";
    public const string ConflictingEvidence = "RC14 conflicting_evidence";
    public const string PartiallyAligned = "RC15 partially_aligned";
    public const string Aligned = "RC16 aligned";
}

/// <summary>
/// The agreement policies in force. Starts empty: there is no default tolerance, and no
/// default notion of what counts as material.
/// </summary>
public sealed class ValueAgreementPolicyRegistry
{
    private readonly Dictionary<string, ValueAgreementPolicy> _policies = new(StringComparer.Ordinal);

    public int PolicyCount => _policies.Count;

    public bool TryDeclarePolicy(ValueAgreementPolicy policy, out string code)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (!DeclaredKey.TryNormalise(policy.PolicyKey, out var policyKey))
        {
            code = ReconciliationCodes.AgreementPolicyNotDeclared;
            return false;
        }

        // The bands must be ordered and real. A material threshold at or below the
        // agreement tolerance would leave no room for partial alignment, which is the
        // state that stops ordinary measurement scatter being reported as conflict.
        if (double.IsNaN(policy.AgreementTolerance) || double.IsNaN(policy.MaterialDivergence) ||
            policy.AgreementTolerance < 0d || policy.MaterialDivergence <= policy.AgreementTolerance)
        {
            code = ReconciliationCodes.InvalidDeclaration;
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

            code = ReconciliationCodes.ConflictingDeclaration;
            return false;
        }

        _policies[policyKey] = normalised;
        code = string.Empty;
        return true;
    }

    public bool TryGetPolicy(string? policyKey, out ValueAgreementPolicy? policy)
    {
        policy = null;

        if (!DeclaredKey.TryNormalise(policyKey, out var normalised)) return false;

        return _policies.TryGetValue(normalised, out policy);
    }
}