// Operational Evidence Reconciliation - deterministic acceptance.
//
// Backlog origin: T-219.
//
// The committed validation fixture carries four subjects that map onto four of the seven
// states without any adjustment:
//
//   SUBJ-030  both at minute 200                   -> Aligned
//   SUBJ-031  minute 200 and 205, intervals overlap -> Aligned
//   SUBJ-032  machine only                          -> MissingEvidence
//   SUBJ-033  minute 200 and 900, disjoint          -> LikelyMisclassified
//
// Read the fixture's construction helpers, not its call-site comments: the second
// argument is the minute, and NumericValue is 200 for every observation. The four
// subjects therefore differ in TIME, not in value, and every value divergence here is
// zero. That makes them the temporal half of the taxonomy.
//
// The value half - PartiallyAligned and ConflictingEvidence - is proven separately below
// against bounded inputs with declared bands of agreement 1 and material 100.
using System;
using System.Linq;
using PlantProcess.Analytics.Core.Kernel;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

[Trait("BacklogTask", "T-219")]
public sealed class OperationalEvidenceReconciliationKnownAnswerTests
{
    private const string Fact = "FACT-OBSERVED-STATE";
    private const string MachineSource = "SOURCE-M";
    private const string ManualSource = "SOURCE-H";
    private const string AlignmentPolicy = "ALIGN-A";
    private const string AgreementPolicy = "AGREE-A";

    private static readonly TimeSpan Tight = TimeSpan.FromSeconds(1);
    private static readonly DateTimeOffset Always = FrozenTestEpoch.AtMinute(0);
    private static readonly DateTimeOffset Forever = FrozenTestEpoch.AtMinute(100000);
    private static readonly DateTimeOffset AsOf = FrozenTestEpoch.AtMinute(100);

    private static FactEvidenceAuthorityRegistry Authority(bool declareManual = true)
    {
        var registry = new FactEvidenceAuthorityRegistry();
        Assert.True(registry.TryDeclareFact(new SemanticFactDeclaration(Fact, 0.5d), out _));

        Assert.True(registry.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(Fact, MachineSource, EvidenceRole.Primary, Always, Forever), out _));

        if (declareManual)
        {
            Assert.True(registry.TryDeclareAuthority(
                new FactSourceAuthorityDeclaration(Fact, ManualSource, EvidenceRole.Supporting, Always, Forever), out _));
        }

        return registry;
    }

    private static TemporalAlignmentPolicyRegistry Alignment(TimeSpan? tolerance = null)
    {
        var registry = new TemporalAlignmentPolicyRegistry();
        Assert.True(registry.TryDeclarePolicy(
            new TemporalAlignmentPolicy(AlignmentPolicy, tolerance ?? TimeSpan.FromMinutes(30)), out _));
        return registry;
    }

    private static ValueAgreementPolicyRegistry Agreement(double agree = 1d, double material = 100d)
    {
        var registry = new ValueAgreementPolicyRegistry();
        Assert.True(registry.TryDeclarePolicy(new ValueAgreementPolicy(AgreementPolicy, agree, material), out _));
        return registry;
    }

    private static EvidenceAssertion Assert_(string source, double value, double atMinute = 100, TimeSpan? uncertainty = null) =>
        new(Fact, source,
            new TemporalInstant(FrozenTestEpoch.AtMinute(atMinute), TimeRole.Effective, source, "SIGNAL-1", uncertainty ?? Tight),
            value, 0.9d);

    private static ReconciliationOutcome Run(
        EvidenceAssertion[] assertions,
        FactEvidenceAuthorityRegistry? authority = null,
        TemporalAlignmentPolicyRegistry? alignment = null,
        ValueAgreementPolicyRegistry? agreement = null) =>
        OperationalEvidenceReconciliationKernel.Reconcile(
            authority ?? Authority(),
            alignment ?? Alignment(),
            AlignmentPolicy,
            agreement ?? Agreement(),
            AgreementPolicy,
            Fact, AsOf, assertions);

    // ------------------------------------------------- the fixture's four cases

    [Fact]
    public void Agreeing_sources_are_aligned()
    {
        var outcome = Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 200d) });

        Assert.True(outcome.IsReconciled);
        Assert.Equal(ReconciliationState.Aligned, outcome.State);
        Assert.Equal(0d, outcome.WidestDivergence);
    }

    [Fact]
    public void Ordinary_scatter_is_partial_alignment_and_not_conflict()
    {
        // Five apart, against a declared material threshold of a hundred. Reporting this
        // as conflict would put the plant permanently at war with its own instruments.
        var outcome = Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 205d) });

        Assert.Equal(ReconciliationState.PartiallyAligned, outcome.State);
        Assert.Equal(5d, outcome.WidestDivergence);
    }

    [Fact]
    public void A_declared_source_that_said_nothing_is_missing_evidence()
    {
        var outcome = Run(new[] { Assert_(MachineSource, 200d) });

        Assert.Equal(ReconciliationState.MissingEvidence, outcome.State);
        Assert.NotEqual(ReconciliationState.ConflictingEvidence, outcome.State);
    }

    [Fact]
    public void Material_divergence_about_the_same_moment_is_conflict()
    {
        var outcome = Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 900d) });

        Assert.Equal(ReconciliationState.ConflictingEvidence, outcome.State);
        Assert.Equal(700d, outcome.WidestDivergence);
    }

    [Fact]
    public void Every_fixture_observation_carries_the_same_value_so_the_subjects_differ_in_time_alone()
    {
        // Guards the mapping below against being read back as a value case. If the
        // fixture ever encodes differing values, this fails and the expectations must be
        // revisited rather than quietly still passing.
        Assert.All(GenericProcessFixture.EvidencePairs, o => Assert.Equal(200d, o.NumericValue!.Value));
    }

    [Fact]
    public void The_four_fixture_subjects_reproduce_the_temporal_half_of_the_taxonomy()
    {
        var pairs = GenericProcessFixture.EvidencePairs.GroupBy(o => o.SubjectId).OrderBy(g => g.Key).ToArray();

        Assert.Equal(4, pairs.Length);

        ReconciliationOutcome ForSubject(IGrouping<string, ProcessObservation> group) =>
            Run(group.Select(o => new EvidenceAssertion(
                    Fact,
                    o.Source == ObservationSourceKind.Machine ? MachineSource : ManualSource,
                    new TemporalInstant(o.At, TimeRole.Effective,
                        o.Source == ObservationSourceKind.Machine ? MachineSource : ManualSource,
                        "SIGNAL-1", o.ClockUncertainty),
                    o.NumericValue!.Value, 0.9d))
                .ToArray(),
                alignment: Alignment(TimeSpan.FromMinutes(30)));

        var states = pairs.Select(g => ForSubject(g).State).ToArray();

        Assert.Equal(ReconciliationState.Aligned, states[0]);             // SUBJ-030, same instant
        Assert.Equal(ReconciliationState.Aligned, states[1]);             // SUBJ-031, intervals overlap
        Assert.Equal(ReconciliationState.MissingEvidence, states[2]);     // SUBJ-032, machine only
        Assert.Equal(ReconciliationState.LikelyMisclassified, states[3]); // SUBJ-033, provably apart

        // Identical values throughout, so nothing here reached the value comparison.
        Assert.All(pairs.Select(ForSubject), o => Assert.Equal(0d, o.WidestDivergence));
    }

    [Fact]
    public void The_widely_separated_fixture_subject_is_misclassification_and_not_conflict()
    {
        // Eleven hours apart with identical values. Two records filed against the same
        // moment that provably describe different ones is a statement about filing.
        var subject = GenericProcessFixture.EvidencePairs.Where(o => o.SubjectId == "SUBJ-033").ToArray();

        var outcome = Run(
            subject.Select(o => new EvidenceAssertion(
                Fact,
                o.Source == ObservationSourceKind.Machine ? MachineSource : ManualSource,
                new TemporalInstant(o.At, TimeRole.Effective,
                    o.Source == ObservationSourceKind.Machine ? MachineSource : ManualSource,
                    "SIGNAL-1", o.ClockUncertainty),
                o.NumericValue!.Value, 0.9d)).ToArray(),
            alignment: Alignment(TimeSpan.FromMinutes(30)));

        Assert.Equal(ReconciliationState.LikelyMisclassified, outcome.State);
        Assert.NotEqual(ReconciliationState.ConflictingEvidence, outcome.State);
        Assert.Equal(0d, outcome.WidestDivergence);
    }

    // --------------------------------- temporal uncertainty never becomes conflict

    [Fact]
    public void Records_that_cannot_be_placed_at_one_moment_are_temporally_uncertain()
    {
        // Values 700 apart, which would otherwise be conflict. It has not been shown the
        // records describe the same moment, so they have not been shown to disagree.
        var outcome = Run(
            new[]
            {
                Assert_(MachineSource, 200d, 100, TimeSpan.FromSeconds(1)),
                Assert_(ManualSource, 900d, 108, TimeSpan.FromMinutes(15))
            },
            alignment: Alignment(TimeSpan.Zero));

        Assert.Equal(ReconciliationState.TemporalUncertain, outcome.State);
        Assert.NotEqual(ReconciliationState.ConflictingEvidence, outcome.State);
        Assert.Equal(700d, outcome.WidestDivergence);
    }

    [Fact]
    public void The_same_records_become_conflict_once_the_moment_is_established()
    {
        // Only the declared alignment tolerance changed. Nothing about the values did.
        var uncertain = Run(
            new[]
            {
                Assert_(MachineSource, 200d, 100, TimeSpan.FromSeconds(1)),
                Assert_(ManualSource, 900d, 108, TimeSpan.FromMinutes(15))
            },
            alignment: Alignment(TimeSpan.FromMinutes(30)));

        Assert.Equal(ReconciliationState.ConflictingEvidence, uncertain.State);
    }

    [Fact]
    public void Agreeing_values_are_still_temporally_uncertain_when_the_moment_is_not_established()
    {
        // Uncertainty is not overridden by convenient agreement either.
        var outcome = Run(
            new[]
            {
                Assert_(MachineSource, 200d, 100, TimeSpan.FromSeconds(1)),
                Assert_(ManualSource, 200d, 108, TimeSpan.FromMinutes(15))
            },
            alignment: Alignment(TimeSpan.Zero));

        Assert.Equal(ReconciliationState.TemporalUncertain, outcome.State);
        Assert.NotEqual(ReconciliationState.Aligned, outcome.State);
    }

    [Fact]
    public void Records_provably_describing_different_moments_are_likely_misclassified()
    {
        // Forty minutes apart with tight uncertainty, both claiming the same fact at the
        // same moment. The likeliest reading is that one is filed against the wrong
        // moment - a statement about filing, not about anybody's honesty.
        var outcome = Run(
            new[]
            {
                Assert_(MachineSource, 200d, 100, TimeSpan.FromSeconds(1)),
                Assert_(ManualSource, 200d, 140, TimeSpan.FromSeconds(1))
            },
            alignment: Alignment(TimeSpan.FromMinutes(5)));

        Assert.Equal(ReconciliationState.LikelyMisclassified, outcome.State);
    }

    // ------------------------------- missing authority never becomes conflict

    [Fact]
    public void An_undeclared_fact_is_unresolved_and_not_conflict()
    {
        var outcome = OperationalEvidenceReconciliationKernel.Reconcile(
            new FactEvidenceAuthorityRegistry(), Alignment(), AlignmentPolicy,
            Agreement(), AgreementPolicy, Fact, AsOf,
            new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 900d) });

        Assert.Equal(ReconciliationState.Unresolved, outcome.State);
        Assert.NotEqual(ReconciliationState.ConflictingEvidence, outcome.State);
    }

    [Fact]
    public void A_silent_primary_authority_is_missing_evidence_and_not_conflict()
    {
        // Only the supporting source spoke, and it disagrees with nothing because the
        // authority has not spoken at all.
        var outcome = Run(new[] { Assert_(ManualSource, 900d) });

        Assert.Equal(ReconciliationState.MissingEvidence, outcome.State);
        Assert.NotEqual(ReconciliationState.ConflictingEvidence, outcome.State);
    }

    [Fact]
    public void Two_primaries_at_one_moment_are_unresolved()
    {
        var registry = new FactEvidenceAuthorityRegistry();
        Assert.True(registry.TryDeclareFact(new SemanticFactDeclaration(Fact, 0.5d), out _));
        Assert.True(registry.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(Fact, MachineSource, EvidenceRole.Primary, Always, Forever), out _));
        Assert.True(registry.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(Fact, ManualSource, EvidenceRole.Primary, Always, Forever), out _));

        var outcome = Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 900d) }, authority: registry);

        Assert.Equal(ReconciliationState.Unresolved, outcome.State);
    }

    [Fact]
    public void A_lone_authority_with_no_second_declared_source_is_missing_evidence()
    {
        // One voice cannot agree or disagree with anything.
        var outcome = Run(new[] { Assert_(MachineSource, 200d) }, authority: Authority(declareManual: false));

        Assert.Equal(ReconciliationState.MissingEvidence, outcome.State);
    }

    // ---------------------------------------------------------- the vocabulary

    [Fact]
    public void There_are_exactly_seven_states_and_no_eighth()
    {
        var states = Enum.GetNames(typeof(ReconciliationState));

        Assert.Equal(7, states.Length);

        foreach (var required in new[]
        {
            "Aligned", "PartiallyAligned", "MissingEvidence", "TemporalUncertain",
            "ConflictingEvidence", "LikelyMisclassified", "Unresolved"
        })
        {
            Assert.Contains(required, states);
        }
    }

    [Fact]
    public void No_state_attributes_dishonesty_carelessness_or_intent_to_anyone()
    {
        // This is not lie detection. A disagreement between a manual record and an
        // instrument is a fact about the records.
        var states = Enum.GetNames(typeof(ReconciliationState));

        foreach (var forbidden in new[] { "liar", "lie", "fraud", "intent", "blame", "fault", "negligen", "operator", "error" })
        {
            Assert.DoesNotContain(states, s => s.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }

    [Fact]
    public void Every_state_has_its_own_code()
    {
        var codes = new[]
        {
            ReconciliationCodes.Aligned, ReconciliationCodes.PartiallyAligned,
            ReconciliationCodes.MissingEvidence, ReconciliationCodes.TemporalUncertain,
            ReconciliationCodes.ConflictingEvidence, ReconciliationCodes.LikelyMisclassified,
            ReconciliationCodes.Unresolved
        };

        Assert.Equal(7, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_reconciled_state_is_a_finding_rather_than_a_refusal()
    {
        // All seven say something about the evidence. None is a refusal to look.
        var outcome = Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 900d) });

        Assert.True(outcome.IsReconciled);
        Assert.Equal(TerminalState.Finding, outcome.Outcome);
        Assert.Equal(ExclusionAttribution.None, outcome.Attribution);
    }

    // ------------------------------------------------- declared bands

    [Fact]
    public void The_bands_are_declared_and_move_the_verdict()
    {
        var assertions = new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 205d) };

        Assert.Equal(ReconciliationState.Aligned, Run(assertions, agreement: Agreement(agree: 10d, material: 100d)).State);
        Assert.Equal(ReconciliationState.PartiallyAligned, Run(assertions, agreement: Agreement(agree: 1d, material: 100d)).State);
        Assert.Equal(ReconciliationState.ConflictingEvidence, Run(assertions, agreement: Agreement(agree: 0.5d, material: 1d)).State);
    }

    [Fact]
    public void A_boundary_value_falls_on_the_inclusive_side_of_agreement()
    {
        var assertions = new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 205d) };

        Assert.Equal(ReconciliationState.Aligned, Run(assertions, agreement: Agreement(agree: 5d, material: 100d)).State);
        Assert.Equal(ReconciliationState.PartiallyAligned, Run(assertions, agreement: Agreement(agree: 4.9d, material: 100d)).State);
    }

    [Fact]
    public void An_undeclared_agreement_policy_refuses_rather_than_choosing_a_band()
    {
        var outcome = OperationalEvidenceReconciliationKernel.Reconcile(
            Authority(), Alignment(), AlignmentPolicy,
            new ValueAgreementPolicyRegistry(), AgreementPolicy,
            Fact, AsOf, new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 205d) });

        Assert.False(outcome.IsReconciled);
        Assert.Equal(ReconciliationCodes.AgreementPolicyNotDeclared, outcome.Code);
        Assert.Equal(TerminalState.RefusedByGuard, outcome.Outcome);
        Assert.Equal(ExclusionAttribution.Declaration, outcome.Attribution);
    }

    [Fact]
    public void A_material_threshold_at_or_below_the_agreement_tolerance_is_rejected()
    {
        // It would leave no room for partial alignment, which is the state that stops
        // ordinary scatter being reported as conflict.
        var registry = new ValueAgreementPolicyRegistry();

        foreach (var (agree, material) in new[] { (5d, 5d), (5d, 1d), (-1d, 10d) })
        {
            Assert.False(registry.TryDeclarePolicy(new ValueAgreementPolicy(AgreementPolicy, agree, material), out var code));
            Assert.Equal(ReconciliationCodes.InvalidDeclaration, code);
        }

        Assert.Equal(0, registry.PolicyCount);
    }

    [Fact]
    public void An_identical_redeclaration_is_idempotent_and_a_conflicting_one_fails_closed()
    {
        var registry = Agreement();

        Assert.True(registry.TryDeclarePolicy(new ValueAgreementPolicy(AgreementPolicy, 1d, 100d), out _));
        Assert.Equal(1, registry.PolicyCount);

        Assert.False(registry.TryDeclarePolicy(new ValueAgreementPolicy(AgreementPolicy, 2d, 100d), out var code));
        Assert.Equal(ReconciliationCodes.ConflictingDeclaration, code);

        Assert.True(registry.TryGetPolicy(AgreementPolicy, out var stored));
        Assert.Equal(1d, stored!.AgreementTolerance);
    }

    [Fact]
    public void Policy_identity_uses_the_same_trim_only_normalisation()
    {
        var registry = Agreement();

        Assert.True(registry.TryGetPolicy("  " + AgreementPolicy + "  ", out var policy));
        Assert.Equal(AgreementPolicy, policy!.PolicyKey);

        Assert.False(registry.TryGetPolicy(AgreementPolicy.ToLowerInvariant(), out _));
    }

    [Fact]
    public void The_outcome_names_the_sources_that_participated()
    {
        var outcome = Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 200d) });

        Assert.Contains(MachineSource, outcome.ParticipatingSourceKeys);
        Assert.Contains(ManualSource, outcome.ParticipatingSourceKeys);
        Assert.Equal(Fact, outcome.FactKey);
    }

    [Fact]
    public void A_refusal_carries_no_participating_sources_and_no_divergence()
    {
        var outcome = OperationalEvidenceReconciliationKernel.Reconcile(
            Authority(), Alignment(), AlignmentPolicy,
            new ValueAgreementPolicyRegistry(), AgreementPolicy,
            Fact, AsOf, new[] { Assert_(MachineSource, 200d) });

        Assert.False(outcome.IsReconciled);
        Assert.Empty(outcome.ParticipatingSourceKeys);
        Assert.Equal(0d, outcome.WidestDivergence);
    }

    // ------------------------------------------------- governed projection

    [Fact]
    public void The_governed_projection_carries_identity_interval_and_evidence_handles()
    {
        var outcome = Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 205d) });
        var finding = ReconciliationFindingProjector.Project(outcome);

        Assert.Equal(ReconciliationCodes.PartiallyAligned, finding.FindingCode);
        Assert.Equal(ReconciliationState.PartiallyAligned, finding.State);
        Assert.Equal(Fact, finding.FactKey);
        Assert.Equal(MachineSource, finding.PrimarySourceKey);
        Assert.Equal(2, finding.EvidenceHandles.Count);
        Assert.True(finding.EffectiveFrom <= finding.EffectiveTo);
    }

    [Fact]
    public void Each_evidence_handle_names_its_source_role_instant_and_uncertainty()
    {
        var outcome = Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 205d) });
        var finding = ReconciliationFindingProjector.Project(outcome);

        var primary = finding.EvidenceHandles.Single(h => h.SourceKey == MachineSource);
        var supporting = finding.EvidenceHandles.Single(h => h.SourceKey == ManualSource);

        Assert.Equal(EvidenceRole.Primary, primary.Role);
        Assert.Equal(EvidenceRole.Supporting, supporting.Role);
        Assert.Equal(Tight, primary.Uncertainty);
        Assert.Equal(200d, primary.Value);
        Assert.Equal(205d, supporting.Value);
    }

    [Fact]
    public void The_projection_reports_the_declared_bands_alongside_the_divergence()
    {
        var outcome = Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 205d) });
        var finding = ReconciliationFindingProjector.Project(outcome);

        Assert.NotNull(finding.Discrepancy);
        Assert.Equal(5d, finding.Discrepancy!.WidestDivergence);
        Assert.Equal(1d, finding.Discrepancy.AgreementTolerance);
        Assert.Equal(100d, finding.Discrepancy.MaterialDivergence);
    }

    [Fact]
    public void A_temporally_uncertain_finding_carries_no_discrepancy_evidence()
    {
        // The values were never compared, so reporting a divergence band would imply a
        // comparison that did not happen.
        var outcome = Run(
            new[]
            {
                Assert_(MachineSource, 200d, 100, TimeSpan.FromSeconds(1)),
                Assert_(ManualSource, 900d, 108, TimeSpan.FromMinutes(15))
            },
            alignment: Alignment(TimeSpan.Zero));

        var finding = ReconciliationFindingProjector.Project(outcome);

        Assert.Equal(ReconciliationState.TemporalUncertain, finding.State);
        Assert.Null(finding.Discrepancy);
        Assert.NotNull(finding.Temporal);
        Assert.Equal(TemporalAlignment.Indeterminate, finding.Temporal!.Alignment);
    }

    [Fact]
    public void The_causal_inputs_are_facts_and_never_a_level()
    {
        var aligned = ReconciliationFindingProjector.Project(
            Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 200d) }));

        var conflicting = ReconciliationFindingProjector.Project(
            Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 900d) }));

        Assert.True(aligned.CausalInput.AuthorityEstablished);
        Assert.True(aligned.CausalInput.ObservedFactPresent);
        Assert.False(aligned.CausalInput.DiscrepancyPresent);
        Assert.True(aligned.CausalInput.TemporallyQualified);

        Assert.True(conflicting.CausalInput.DiscrepancyPresent);

        // No level, no rung, no confidence score. Promotion belongs to the ladder.
        var members = typeof(CausalConfidenceInput).GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain(members, m => m.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.DoesNotContain(members, m => m.IndexOf("Confidence", StringComparison.OrdinalIgnoreCase) >= 0 && m != "AuthorityEstablished");
        Assert.DoesNotContain(members, m => m.IndexOf("Cause", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void An_unresolved_finding_projects_without_authority_established()
    {
        var outcome = OperationalEvidenceReconciliationKernel.Reconcile(
            new FactEvidenceAuthorityRegistry(), Alignment(), AlignmentPolicy,
            Agreement(), AgreementPolicy, Fact, AsOf,
            new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 900d) });

        var finding = ReconciliationFindingProjector.Project(outcome);

        Assert.Equal(ReconciliationState.Unresolved, finding.State);
        Assert.False(finding.CausalInput.AuthorityEstablished);
        Assert.False(finding.CausalInput.DiscrepancyPresent);
        Assert.Empty(finding.EvidenceHandles);
    }

    [Fact]
    public void The_projection_is_deterministic()
    {
        var outcome = Run(new[] { Assert_(MachineSource, 200d), Assert_(ManualSource, 205d) });

        Assert.Equal(ReconciliationFindingProjector.Project(outcome), ReconciliationFindingProjector.Project(outcome));
    }
}