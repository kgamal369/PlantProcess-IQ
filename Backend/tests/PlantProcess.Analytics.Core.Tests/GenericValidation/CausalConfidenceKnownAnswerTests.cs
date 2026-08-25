// Causal Confidence Ladder - deterministic acceptance.
//
// Backlog origin: T-220.
//
// Every case is built from a committed T-219 governed finding, so the ladder is fed the
// real seam rather than a hand-made stand-in. Nothing here recomputes a statistic, a
// reconciliation state or a temporal qualification.
using System;
using System.Linq;
using PlantProcess.Analytics.Core.Kernel;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

[Trait("BacklogTask", "T-220")]
public sealed class CausalConfidenceKnownAnswerTests
{
    private const string Fact = "FACT-OBSERVED-STATE";
    private const string MachineSource = "SOURCE-M";
    private const string ManualSource = "SOURCE-H";
    private const string AlignmentPolicy = "ALIGN-A";
    private const string AgreementPolicy = "AGREE-A";
    private const string CausalPolicy = "CAUSAL-A";

    private static readonly TimeSpan Tight = TimeSpan.FromSeconds(1);
    private static readonly DateTimeOffset Always = FrozenTestEpoch.AtMinute(0);
    private static readonly DateTimeOffset Forever = FrozenTestEpoch.AtMinute(100000);
    private static readonly DateTimeOffset AsOf = FrozenTestEpoch.AtMinute(100);

    private static CausalEvidencePolicyRegistry Policies()
    {
        var registry = new CausalEvidencePolicyRegistry();
        Assert.True(registry.TryDeclarePolicy(
            new CausalEvidencePolicy(CausalPolicy, 0.05d, 0.3d, 30, 3, 2), out _));
        return registry;
    }

    private static StatisticalAssociationEvidence Association(
        double q = 0.01d, double effect = 0.6d, int support = 100, bool corrected = true) =>
        new(effect, support, q, corrected, "HANDLE-STAT");

    private static TemporalPrecedenceEvidence Precedence(int occurrences = 5, int consistent = 5) =>
        new(occurrences, consistent, "HANDLE-TIME");

    private static MechanisticEvidence Mechanism(params string[] kinds) =>
        new("MECHANISM-1", kinds.Length == 0 ? new[] { "KIND-A", "KIND-B" } : kinds, "HANDLE-MECH");

    private static GovernedExternalConfirmation Confirmation() =>
        new("CONFIRMATION-1", "AUTHORITY-1", "HANDLE-CONFIRM", AsOf);

    // A committed T-219 finding, produced by the real kernel.
    private static GovernedReconciliationFinding Finding(double machineValue, double manualValue, double manualMinute = 100)
    {
        var authority = new FactEvidenceAuthorityRegistry();
        Assert.True(authority.TryDeclareFact(new SemanticFactDeclaration(Fact, 0.5d), out _));
        Assert.True(authority.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(Fact, MachineSource, EvidenceRole.Primary, Always, Forever), out _));
        Assert.True(authority.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(Fact, ManualSource, EvidenceRole.Supporting, Always, Forever), out _));

        var alignment = new TemporalAlignmentPolicyRegistry();
        Assert.True(alignment.TryDeclarePolicy(new TemporalAlignmentPolicy(AlignmentPolicy, TimeSpan.FromMinutes(30)), out _));

        var agreement = new ValueAgreementPolicyRegistry();
        Assert.True(agreement.TryDeclarePolicy(new ValueAgreementPolicy(AgreementPolicy, 1d, 100d), out _));

        EvidenceAssertion Assertion(string source, double value, double minute, TimeSpan uncertainty) =>
            new(Fact, source,
                new TemporalInstant(FrozenTestEpoch.AtMinute(minute), TimeRole.Effective, source, "SIGNAL-1", uncertainty),
                value, 0.9d);

        var outcome = OperationalEvidenceReconciliationKernel.Reconcile(
            authority, alignment, AlignmentPolicy, agreement, AgreementPolicy, Fact, AsOf,
            new[]
            {
                Assertion(MachineSource, machineValue, 100, Tight),
                Assertion(ManualSource, manualValue, manualMinute, Tight)
            });

        return ReconciliationFindingProjector.Project(outcome);
    }

    private static GovernedReconciliationFinding AlignedFinding() => Finding(200d, 200d);
    private static GovernedReconciliationFinding DiscrepantFinding() => Finding(200d, 205d);

    private static CausalConfidenceAssessment Assess(
        GovernedReconciliationFinding? finding,
        StatisticalAssociationEvidence? statistical = null,
        TemporalPrecedenceEvidence? temporal = null,
        MechanisticEvidence? mechanistic = null,
        GovernedExternalConfirmation? confirmation = null) =>
        CausalConfidenceKernel.Assess(Policies(), CausalPolicy, finding, statistical, temporal, mechanistic, confirmation);

    // ---------------------------------------------------------- the six rungs

    [Fact]
    public void An_observed_fact_alone_is_L0()
    {
        var assessment = Assess(AlignedFinding());

        Assert.True(assessment.IsSupported);
        Assert.Equal(CausalConfidenceLevel.ObservedFact, assessment.SupportedLevel);
        Assert.NotEmpty(assessment.EvidenceHandles);
        Assert.Equal(CausalConfidenceCodes.ObservedFact, assessment.Code);
    }

    [Fact]
    public void A_discrepancy_is_L1()
    {
        var assessment = Assess(DiscrepantFinding());

        Assert.Equal(CausalConfidenceLevel.Discrepancy, assessment.SupportedLevel);
    }

    [Fact]
    public void Governed_association_evidence_is_L2()
    {
        var assessment = Assess(DiscrepantFinding(), Association());

        Assert.Equal(CausalConfidenceLevel.StatisticalAssociation, assessment.SupportedLevel);
    }

    [Fact]
    public void Consistent_precedence_is_L3()
    {
        var assessment = Assess(DiscrepantFinding(), Association(), Precedence());

        Assert.Equal(CausalConfidenceLevel.TemporallySupportedHypothesis, assessment.SupportedLevel);
    }

    [Fact]
    public void Independent_fact_kinds_on_one_mechanism_are_L4()
    {
        var assessment = Assess(DiscrepantFinding(), Association(), Precedence(), Mechanism());

        Assert.Equal(CausalConfidenceLevel.MechanisticallySupportedHypothesis, assessment.SupportedLevel);
    }

    [Fact]
    public void A_governed_external_confirmation_on_a_complete_chain_is_L5()
    {
        var assessment = Assess(DiscrepantFinding(), Association(), Precedence(), Mechanism(), Confirmation());

        Assert.Equal(CausalConfidenceLevel.ConfirmedCause, assessment.SupportedLevel);
        Assert.Equal("HANDLE-CONFIRM", assessment.ExternalConfirmationHandle);
        Assert.Equal(CausalClaimClass.ConfirmedCause, assessment.AllowedClaimClass);
    }

    [Fact]
    public void There_are_exactly_six_levels_and_no_seventh()
    {
        var levels = Enum.GetNames(typeof(CausalConfidenceLevel));

        Assert.Equal(6, levels.Length);
        Assert.Equal(0, (int)CausalConfidenceLevel.ObservedFact);
        Assert.Equal(5, (int)CausalConfidenceLevel.ConfirmedCause);
    }

    // ------------------------------------------------------------ cumulative

    [Fact]
    public void Removing_the_confirmation_from_an_L5_case_deterministically_downgrades_to_L4()
    {
        var withConfirmation = Assess(DiscrepantFinding(), Association(), Precedence(), Mechanism(), Confirmation());
        var without = Assess(DiscrepantFinding(), Association(), Precedence(), Mechanism());

        Assert.Equal(CausalConfidenceLevel.ConfirmedCause, withConfirmation.SupportedLevel);
        Assert.Equal(CausalConfidenceLevel.MechanisticallySupportedHypothesis, without.SupportedLevel);

        // The evidence did not change. The confirmation was the only thing that made it a
        // confirmation.
        Assert.Null(without.ExternalConfirmationHandle);
        Assert.Equal(CausalClaimClass.StrongestSupportedHypothesis, without.AllowedClaimClass);
    }

    [Fact]
    public void Mechanistic_evidence_cannot_step_over_absent_temporal_support()
    {
        // A mechanism nobody has shown to precede its effect is a hypothesis wearing one.
        var assessment = Assess(DiscrepantFinding(), Association(), temporal: null, mechanistic: Mechanism());

        Assert.Equal(CausalConfidenceLevel.StatisticalAssociation, assessment.SupportedLevel);
        Assert.NotEqual(CausalConfidenceLevel.MechanisticallySupportedHypothesis, assessment.SupportedLevel);
    }

    [Fact]
    public void A_confirmation_cannot_lift_a_weak_chain()
    {
        // Confirmation attached to a case with no association evidence proves nothing
        // about the chain beneath it.
        var assessment = Assess(DiscrepantFinding(), confirmation: Confirmation());

        Assert.Equal(CausalConfidenceLevel.Discrepancy, assessment.SupportedLevel);
        Assert.Null(assessment.ExternalConfirmationHandle);
        Assert.Equal(CausalClaimClass.StrongestSupportedHypothesis, assessment.AllowedClaimClass);
    }

    [Fact]
    public void Every_gap_stops_the_walk_at_the_rung_below_it()
    {
        var noAssociation = Assess(DiscrepantFinding(), null, Precedence(), Mechanism(), Confirmation());
        var noPrecedence = Assess(DiscrepantFinding(), Association(), null, Mechanism(), Confirmation());
        var noMechanism = Assess(DiscrepantFinding(), Association(), Precedence(), null, Confirmation());

        Assert.Equal(CausalConfidenceLevel.Discrepancy, noAssociation.SupportedLevel);
        Assert.Equal(CausalConfidenceLevel.StatisticalAssociation, noPrecedence.SupportedLevel);
        Assert.Equal(CausalConfidenceLevel.TemporallySupportedHypothesis, noMechanism.SupportedLevel);
    }

    // ------------------------------------------------- what each rung requires

    [Fact]
    public void Missing_evidence_never_becomes_a_discrepancy()
    {
        // Only the primary spoke, so the reconciliation state is MissingEvidence. That is
        // not a disagreement, and no amount of association, precedence or mechanistic
        // evidence stacked on top of it turns the case into one.
        var authority = new FactEvidenceAuthorityRegistry();
        Assert.True(authority.TryDeclareFact(new SemanticFactDeclaration(Fact, 0.5d), out _));
        Assert.True(authority.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(Fact, MachineSource, EvidenceRole.Primary, Always, Forever), out _));
        Assert.True(authority.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(Fact, ManualSource, EvidenceRole.Supporting, Always, Forever), out _));

        var alignment = new TemporalAlignmentPolicyRegistry();
        Assert.True(alignment.TryDeclarePolicy(new TemporalAlignmentPolicy(AlignmentPolicy, TimeSpan.FromMinutes(30)), out _));

        var agreement = new ValueAgreementPolicyRegistry();
        Assert.True(agreement.TryDeclarePolicy(new ValueAgreementPolicy(AgreementPolicy, 1d, 100d), out _));

        var outcome = OperationalEvidenceReconciliationKernel.Reconcile(
            authority, alignment, AlignmentPolicy, agreement, AgreementPolicy, Fact, AsOf,
            new[]
            {
                new EvidenceAssertion(Fact, MachineSource,
                    new TemporalInstant(AsOf, TimeRole.Effective, MachineSource, "SIGNAL-1", Tight), 200d, 0.9d)
            });

        Assert.Equal(ReconciliationState.MissingEvidence, outcome.State);

        var finding = ReconciliationFindingProjector.Project(outcome);

        // Recorded observation, deferred: a MissingEvidence finding projects no evidence
        // handles, even though the primary authority did speak. The ladder therefore
        // cannot see an observed fact beneath the missing one and stops below L0 rather
        // than at L1. The law under test is unaffected either way.
        Assert.Empty(finding.EvidenceHandles);
        Assert.False(finding.CausalInput.DiscrepancyPresent);

        var assessment = Assess(finding, Association(), Precedence(), Mechanism());

        Assert.False(assessment.IsSupported);
        Assert.NotEqual(CausalConfidenceLevel.Discrepancy, assessment.SupportedLevel);
        Assert.Equal(CausalClaimClass.NoClaim, assessment.AllowedClaimClass);

        // And it says exactly what is missing rather than going quiet.
        Assert.Single(assessment.MissingEvidenceForNextLevel);
        Assert.Equal(CausalConfidenceCodes.RequiresObservedFact,
            assessment.MissingEvidenceForNextLevel[0].RequirementCode);
    }

    [Fact]
    public void Temporal_uncertainty_cannot_promote_to_L3()
    {
        // Records that could not be placed at one moment. Precedence evidence exists and
        // is irrelevant, because the qualification beneath it is absent.
        var authority = new FactEvidenceAuthorityRegistry();
        Assert.True(authority.TryDeclareFact(new SemanticFactDeclaration(Fact, 0.5d), out _));
        Assert.True(authority.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(Fact, MachineSource, EvidenceRole.Primary, Always, Forever), out _));
        Assert.True(authority.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(Fact, ManualSource, EvidenceRole.Supporting, Always, Forever), out _));

        var alignment = new TemporalAlignmentPolicyRegistry();
        Assert.True(alignment.TryDeclarePolicy(new TemporalAlignmentPolicy(AlignmentPolicy, TimeSpan.Zero), out _));

        var agreement = new ValueAgreementPolicyRegistry();
        Assert.True(agreement.TryDeclarePolicy(new ValueAgreementPolicy(AgreementPolicy, 1d, 100d), out _));

        var outcome = OperationalEvidenceReconciliationKernel.Reconcile(
            authority, alignment, AlignmentPolicy, agreement, AgreementPolicy, Fact, AsOf,
            new[]
            {
                new EvidenceAssertion(Fact, MachineSource,
                    new TemporalInstant(FrozenTestEpoch.AtMinute(100), TimeRole.Effective, MachineSource, "SIGNAL-1", TimeSpan.FromSeconds(1)), 200d, 0.9d),
                new EvidenceAssertion(Fact, ManualSource,
                    new TemporalInstant(FrozenTestEpoch.AtMinute(108), TimeRole.Effective, ManualSource, "SIGNAL-1", TimeSpan.FromMinutes(15)), 205d, 0.9d)
            });

        Assert.Equal(ReconciliationState.TemporalUncertain, outcome.State);

        var assessment = Assess(ReconciliationFindingProjector.Project(outcome), Association(), Precedence(), Mechanism());

        Assert.NotEqual(CausalConfidenceLevel.TemporallySupportedHypothesis, assessment.SupportedLevel);
        Assert.NotEqual(CausalConfidenceLevel.MechanisticallySupportedHypothesis, assessment.SupportedLevel);
    }

    [Fact]
    public void A_single_occurrence_of_precedence_is_a_coincidence()
    {
        var assessment = Assess(DiscrepantFinding(), Association(), Precedence(occurrences: 1, consistent: 1), Mechanism());

        Assert.Equal(CausalConfidenceLevel.StatisticalAssociation, assessment.SupportedLevel);
        Assert.Contains(assessment.MissingEvidenceForNextLevel,
            r => r.RequirementCode == CausalConfidenceCodes.RequiresTemporalPrecedence);
    }

    [Fact]
    public void Inconsistent_precedence_does_not_count_as_precedence()
    {
        var assessment = Assess(DiscrepantFinding(), Association(), Precedence(occurrences: 5, consistent: 4));

        Assert.Equal(CausalConfidenceLevel.StatisticalAssociation, assessment.SupportedLevel);
    }

    [Fact]
    public void One_fact_kind_is_not_independent_corroboration()
    {
        var assessment = Assess(DiscrepantFinding(), Association(), Precedence(), Mechanism("KIND-A"));

        Assert.Equal(CausalConfidenceLevel.TemporallySupportedHypothesis, assessment.SupportedLevel);
        Assert.Contains(assessment.MissingEvidenceForNextLevel,
            r => r.RequirementCode == CausalConfidenceCodes.RequiresMechanism);
    }

    [Fact]
    public void Correlation_plus_time_is_not_mechanism()
    {
        // L3 is reached, and stays there, until independent fact kinds say why.
        var assessment = Assess(DiscrepantFinding(), Association(), Precedence());

        Assert.Equal(CausalConfidenceLevel.TemporallySupportedHypothesis, assessment.SupportedLevel);
        Assert.Single(assessment.MissingEvidenceForNextLevel);
        Assert.Equal(CausalConfidenceLevel.MechanisticallySupportedHypothesis,
            assessment.MissingEvidenceForNextLevel[0].ForLevel);
    }

    [Fact]
    public void An_uncorrected_q_value_does_not_establish_association()
    {
        var assessment = Assess(DiscrepantFinding(), Association(corrected: false), Precedence(), Mechanism());

        Assert.Equal(CausalConfidenceLevel.Discrepancy, assessment.SupportedLevel);
    }

    [Fact]
    public void Association_evidence_must_clear_every_declared_threshold()
    {
        foreach (var weak in new[]
        {
            Association(q: 0.2d),
            Association(effect: 0.1d),
            Association(support: 5)
        })
        {
            var assessment = Assess(DiscrepantFinding(), weak, Precedence(), Mechanism());
            Assert.Equal(CausalConfidenceLevel.Discrepancy, assessment.SupportedLevel);
        }
    }

    [Fact]
    public void Each_level_reports_exactly_what_the_next_one_requires()
    {
        var expectations = new (CausalConfidenceAssessment Assessment, CausalConfidenceLevel NextLevel, string Requirement)[]
        {
            (Assess(AlignedFinding()), CausalConfidenceLevel.Discrepancy, CausalConfidenceCodes.RequiresDiscrepancy),
            (Assess(DiscrepantFinding()), CausalConfidenceLevel.StatisticalAssociation, CausalConfidenceCodes.RequiresStatisticalAssociation),
            (Assess(DiscrepantFinding(), Association()), CausalConfidenceLevel.TemporallySupportedHypothesis, CausalConfidenceCodes.RequiresTemporalPrecedence),
            (Assess(DiscrepantFinding(), Association(), Precedence()), CausalConfidenceLevel.MechanisticallySupportedHypothesis, CausalConfidenceCodes.RequiresMechanism),
            (Assess(DiscrepantFinding(), Association(), Precedence(), Mechanism()), CausalConfidenceLevel.ConfirmedCause, CausalConfidenceCodes.RequiresExternalConfirmation)
        };

        foreach (var expectation in expectations)
        {
            Assert.Single(expectation.Assessment.MissingEvidenceForNextLevel);

            var requirement = expectation.Assessment.MissingEvidenceForNextLevel[0];

            Assert.Equal(expectation.NextLevel, requirement.ForLevel);
            Assert.Equal(expectation.Requirement, requirement.RequirementCode);
            Assert.False(string.IsNullOrWhiteSpace(requirement.Description));
        }
    }

    [Fact]
    public void A_fully_confirmed_case_needs_nothing_further()
    {
        var assessment = Assess(DiscrepantFinding(), Association(), Precedence(), Mechanism(), Confirmation());

        Assert.Empty(assessment.MissingEvidenceForNextLevel);
    }

    // -------------------------------------------------------------- language

    [Fact]
    public void Confirmation_wording_is_forbidden_below_L5()
    {
        foreach (var level in new[]
        {
            CausalConfidenceLevel.ObservedFact,
            CausalConfidenceLevel.Discrepancy,
            CausalConfidenceLevel.StatisticalAssociation,
            CausalConfidenceLevel.TemporallySupportedHypothesis,
            CausalConfidenceLevel.MechanisticallySupportedHypothesis
        })
        {
            foreach (var phrase in new[]
            {
                "the confirmed root cause is the upstream setting",
                "this is the confirmed cause",
                "a proven cause was identified",
                "the outage was definitely caused by the change"
            })
            {
                var verdict = CausalClaimLanguagePolicy.Validate(level, phrase);

                Assert.False(verdict.IsPermitted);
                Assert.Equal(CausalConfidenceCodes.ClaimLanguageForbidden, verdict.Code);
                Assert.False(string.IsNullOrWhiteSpace(verdict.OffendingPhrase));
            }
        }
    }

    [Fact]
    public void Hypothesis_wording_is_permitted_below_L5()
    {
        var assessment = Assess(DiscrepantFinding(), Association(), Precedence(), Mechanism());

        var verdict = CausalClaimLanguagePolicy.Validate(
            assessment, "the strongest supported root-cause hypothesis is the upstream setting");

        Assert.True(verdict.IsPermitted);
        Assert.Equal(CausalClaimClass.StrongestSupportedHypothesis, assessment.AllowedClaimClass);
    }

    [Fact]
    public void Confirmation_wording_is_permitted_only_at_L5()
    {
        var assessment = Assess(DiscrepantFinding(), Association(), Precedence(), Mechanism(), Confirmation());

        var verdict = CausalClaimLanguagePolicy.Validate(assessment, "this is the confirmed cause");

        Assert.True(verdict.IsPermitted);
        Assert.Equal(CausalClaimClass.ConfirmedCause, assessment.AllowedClaimClass);
    }

    [Fact]
    public void Language_consumes_a_level_and_never_produces_one()
    {
        // The wording guard returns a verdict about a phrase. It carries no level, so no
        // phrasing can raise or lower one.
        var members = typeof(CausalClaimLanguageVerdict).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain(members, m => m.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0);

        // The same phrase yields opposite verdicts purely because the level differs.
        const string phrase = "this is the confirmed cause";

        Assert.False(CausalClaimLanguagePolicy.Validate(CausalConfidenceLevel.MechanisticallySupportedHypothesis, phrase).IsPermitted);
        Assert.True(CausalClaimLanguagePolicy.Validate(CausalConfidenceLevel.ConfirmedCause, phrase).IsPermitted);
    }

    [Fact]
    public void An_unsupported_assessment_permits_no_causal_phrasing_at_all()
    {
        var assessment = Assess(finding: null);

        Assert.False(assessment.IsSupported);
        Assert.Equal(CausalClaimClass.NoClaim, assessment.AllowedClaimClass);
        Assert.False(CausalClaimLanguagePolicy.Validate(assessment, "the strongest supported hypothesis is X").IsPermitted);
    }

    // ---------------------------------------------------------- fail closed

    [Fact]
    public void An_undeclared_policy_refuses_rather_than_choosing_thresholds()
    {
        var assessment = CausalConfidenceKernel.Assess(
            new CausalEvidencePolicyRegistry(), CausalPolicy, DiscrepantFinding(), Association(), Precedence(), Mechanism());

        Assert.False(assessment.IsSupported);
        Assert.Equal(CausalConfidenceCodes.PolicyNotDeclared, assessment.Code);
        Assert.Equal(TerminalState.RefusedByGuard, assessment.Outcome);
        Assert.Equal(ExclusionAttribution.Declaration, assessment.Attribution);
    }

    [Fact]
    public void Thresholds_that_would_make_a_coincidence_sufficient_are_rejected()
    {
        var registry = new CausalEvidencePolicyRegistry();

        foreach (var policy in new[]
        {
            new CausalEvidencePolicy(CausalPolicy, 0.05d, 0.3d, 30, 1, 2),  // one occurrence
            new CausalEvidencePolicy(CausalPolicy, 0.05d, 0.3d, 30, 3, 1),  // one fact kind
            new CausalEvidencePolicy(CausalPolicy, 0d, 0.3d, 30, 3, 2),     // impossible q
            new CausalEvidencePolicy(CausalPolicy, 0.05d, -1d, 30, 3, 2),   // negative effect
            new CausalEvidencePolicy(CausalPolicy, 0.05d, 0.3d, 0, 3, 2)    // no support
        })
        {
            Assert.False(registry.TryDeclarePolicy(policy, out var code));
            Assert.Equal(CausalConfidenceCodes.InvalidDeclaration, code);
        }

        Assert.Equal(0, registry.PolicyCount);
    }

    [Fact]
    public void An_absent_finding_supports_nothing_and_says_what_is_needed()
    {
        var assessment = Assess(finding: null);

        Assert.False(assessment.IsSupported);
        Assert.Equal(TerminalState.InsufficientData, assessment.Outcome);
        Assert.Single(assessment.MissingEvidenceForNextLevel);
        Assert.Equal(CausalConfidenceCodes.RequiresObservedFact,
            assessment.MissingEvidenceForNextLevel[0].RequirementCode);
    }

    [Fact]
    public void An_identical_redeclaration_is_idempotent_and_a_conflicting_one_fails_closed()
    {
        var registry = Policies();

        Assert.True(registry.TryDeclarePolicy(new CausalEvidencePolicy(CausalPolicy, 0.05d, 0.3d, 30, 3, 2), out _));
        Assert.Equal(1, registry.PolicyCount);

        Assert.False(registry.TryDeclarePolicy(new CausalEvidencePolicy(CausalPolicy, 0.10d, 0.3d, 30, 3, 2), out var code));
        Assert.Equal(CausalConfidenceCodes.ConflictingDeclaration, code);

        Assert.True(registry.TryGetPolicy(CausalPolicy, out var stored));
        Assert.Equal(0.05d, stored!.MaximumQValue);
    }

    [Fact]
    public void The_assessment_is_deterministic()
    {
        var first = Assess(DiscrepantFinding(), Association(), Precedence(), Mechanism());
        var second = Assess(DiscrepantFinding(), Association(), Precedence(), Mechanism());

        Assert.Equal(first.SupportedLevel, second.SupportedLevel);
        Assert.Equal(first.Code, second.Code);
        Assert.Equal(first.AllowedClaimClass, second.AllowedClaimClass);
    }

    [Fact]
    public void The_ladder_consumes_the_committed_reconciliation_seam_without_recreating_it()
    {
        // Everything the ladder reads about state, time and discrepancy comes from the
        // committed projection. It recomputes none of them.
        var finding = DiscrepantFinding();

        Assert.Equal(ReconciliationState.PartiallyAligned, finding.State);
        Assert.NotNull(finding.Discrepancy);
        Assert.NotNull(finding.Temporal);
        Assert.True(finding.CausalInput.DiscrepancyPresent);
        Assert.True(finding.CausalInput.TemporallyQualified);

        var assessment = Assess(finding, Association());

        Assert.Equal(finding.EvidenceHandles, assessment.EvidenceHandles);
    }
}