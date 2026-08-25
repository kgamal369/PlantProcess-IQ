// Transition-aware pooling guard - deterministic acceptance.
//
// Backlog origin: T-236.
//
// The committed validation fixture supplies the confounding case directly: four
// steady-state samples averaging exactly 100, three transition samples, and a pooled
// mean of 615/7. The pooled figure is not noise. It is a confident, plausible answer to
// a question nobody should have been allowed to ask, and this guard is what stops it
// being asked.
//
// Sample instants sit inside the fixture's own declared regime layout for SUBJ-010:
// steady state to minute 30, transition to minute 45, settling to minute 60.
using System;
using System.Linq;
using PlantProcess.Analytics.Core.Kernel;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

[Trait("BacklogTask", "T-236")]
public sealed class TransitionAwarePoolingKnownAnswerTests
{
    private const string Scope = "SCOPE-A";
    private const string OtherScope = "SCOPE-B";
    private const string KindCode = "context_change";
    private const string FromContext = "CONTEXT-1";
    private const string ToContext = "CONTEXT-2";

    private static readonly TimeSpan Tight = TimeSpan.FromSeconds(1);

    private static DateTimeOffset At(double minute) => FrozenTestEpoch.AtMinute(minute);

    private static OperationalTransitionRegistry RegistryWithFixtureLayout()
    {
        var registry = new OperationalTransitionRegistry();
        Assert.True(registry.TryDeclareScope(Scope, out _));

        // The fixture's own boundaries: transition 30 to 45, settling for 15 minutes.
        Assert.True(registry.TryDeclareTransition(new TransitionDeclaration(
            Scope, KindCode, FromContext, ToContext, At(30), At(45),
            StabilisationBasis.Time, TimeSpan.FromMinutes(15), 0, ""), out _));

        return registry;
    }

    private static RegimeScopedSample Sample(double minute, double value, TimeSpan? uncertainty = null, string scope = Scope) =>
        new(scope,
            new TemporalInstant(At(minute), TimeRole.Effective, "SOURCE-M", "SIGNAL-1", uncertainty ?? Tight),
            value);

    private static RegimeScopedSample[] SteadySamples() =>
        GenericProcessFixture.StableRegimeValues
            .Select((value, index) => Sample(5 + (index * 5), value))
            .ToArray();

    private static RegimeScopedSample[] TransitionSamples() =>
        GenericProcessFixture.TransitionRegimeValues
            .Select((value, index) => Sample(32 + (index * 4), value))
            .ToArray();

    // --------------------------------------------------------- the core case

    [Fact]
    public void Pooling_steady_state_with_transition_samples_is_refused()
    {
        var registry = RegistryWithFixtureLayout();
        var everything = SteadySamples().Concat(TransitionSamples()).ToArray();

        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, everything, StabilisationObservation.None);

        Assert.False(admission.IsAdmitted);
        Assert.Equal(PoolingGuardCodes.MixedProcessRegime, admission.Code);
        Assert.Equal(OperationalRegime.Unknown, admission.Regime);
        Assert.Equal(7, admission.SampleCount);
    }

    [Fact]
    public void The_refusal_code_is_the_one_the_committed_fixture_already_names()
    {
        Assert.Equal(ContinuousProcessKnownAnswers.RequiredRegimeRefusalCode, PoolingGuardCodes.MixedProcessRegime);
        Assert.Equal(OperationalTransitionCodes.MixedProcessRegime, PoolingGuardCodes.MixedProcessRegime);
    }

    [Fact]
    public void The_confidently_wrong_pooled_mean_is_exactly_what_the_refusal_prevents()
    {
        // Computed here only to show what would have been returned. The guard refused, so
        // nothing downstream ever reaches this number.
        var registry = RegistryWithFixtureLayout();
        var everything = SteadySamples().Concat(TransitionSamples()).ToArray();

        Assert.False(TransitionAwarePoolingGuard.Admit(registry, Scope, everything, StabilisationObservation.None).IsAdmitted);

        var pooled = everything.Average(s => s.Value);

        Assert.Equal(ContinuousProcessKnownAnswers.PooledAcrossRegimes.AsDouble, pooled, 10);
        Assert.NotEqual(ContinuousProcessKnownAnswers.StableRegimeOnly.AsDouble, pooled, 10);
    }

    [Fact]
    public void A_steady_state_population_is_admitted_and_yields_the_known_answer()
    {
        var registry = RegistryWithFixtureLayout();
        var steady = SteadySamples();

        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, steady, StabilisationObservation.None);

        Assert.True(admission.IsAdmitted);
        Assert.Equal(OperationalRegime.Stable, admission.Regime);
        Assert.Equal(4, admission.SampleCount);
        Assert.Equal(PoolingGuardCodes.PoolingAdmitted, admission.Code);

        Assert.Equal(ContinuousProcessKnownAnswers.StableRegimeOnly.AsDouble, steady.Average(s => s.Value), 10);
    }

    [Fact]
    public void A_transition_population_is_admitted_on_its_own_terms()
    {
        // Transition samples are correct measurements of what the plant was doing. They
        // are not rejected as bad data; they are simply a different population.
        var registry = RegistryWithFixtureLayout();

        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, TransitionSamples(), StabilisationObservation.None);

        Assert.True(admission.IsAdmitted);
        Assert.Equal(OperationalRegime.Transition, admission.Regime);
        Assert.Equal(3, admission.SampleCount);
    }

    [Fact]
    public void Selecting_one_regime_turns_a_refused_population_into_an_admitted_one()
    {
        var registry = RegistryWithFixtureLayout();
        var everything = SteadySamples().Concat(TransitionSamples()).ToArray();

        var steadyOnly = TransitionAwarePoolingGuard.SelectRegime(
            registry, Scope, everything, OperationalRegime.Stable, StabilisationObservation.None);

        Assert.Equal(4, steadyOnly.Count);

        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, steadyOnly, StabilisationObservation.None);

        Assert.True(admission.IsAdmitted);
        Assert.Equal(ContinuousProcessKnownAnswers.StableRegimeOnly.AsDouble, steadyOnly.Average(s => s.Value), 10);
    }

    [Fact]
    public void Settling_samples_are_not_pooled_with_steady_state_ones()
    {
        // The third regime the fixture declares. Settling is neither the transition nor
        // steady state, and pooling it with either is the same defect.
        var registry = RegistryWithFixtureLayout();

        var mixed = SteadySamples().Concat(new[] { Sample(50, 95d) }).ToArray();

        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, mixed, StabilisationObservation.None);

        Assert.False(admission.IsAdmitted);
        Assert.Equal(PoolingGuardCodes.MixedProcessRegime, admission.Code);

        var settlingOnly = TransitionAwarePoolingGuard.SelectRegime(
            registry, Scope, mixed, OperationalRegime.Stabilising, StabilisationObservation.None);

        Assert.Single(settlingOnly);
    }

    // ------------------------------------------------- temporal uncertainty

    [Fact]
    public void A_sample_whose_uncertainty_straddles_a_boundary_cannot_be_placed()
    {
        // Two minutes of uncertainty around minute 30. This sample could have been taken
        // on either side of the changeover, and which side its point estimate landed on
        // is not evidence.
        var registry = RegistryWithFixtureLayout();

        var straddling = new[] { Sample(30, 100d, TimeSpan.FromMinutes(2)) };

        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, straddling, StabilisationObservation.None);

        Assert.False(admission.IsAdmitted);
        Assert.Equal(PoolingGuardCodes.SampleRegimeTemporallyUncertain, admission.Code);
    }

    [Fact]
    public void One_unplaceable_sample_refuses_a_population_that_is_otherwise_uniform()
    {
        var registry = RegistryWithFixtureLayout();

        var population = SteadySamples().Concat(new[] { Sample(29, 100d, TimeSpan.FromMinutes(5)) }).ToArray();

        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, population, StabilisationObservation.None);

        Assert.False(admission.IsAdmitted);
        Assert.Equal(PoolingGuardCodes.SampleRegimeTemporallyUncertain, admission.Code);
    }

    [Fact]
    public void The_same_sample_is_placeable_once_its_uncertainty_no_longer_reaches_the_boundary()
    {
        var registry = RegistryWithFixtureLayout();

        var uncertain = TransitionAwarePoolingGuard.Admit(
            registry, Scope, new[] { Sample(29, 100d, TimeSpan.FromMinutes(5)) }, StabilisationObservation.None);

        var precise = TransitionAwarePoolingGuard.Admit(
            registry, Scope, new[] { Sample(29, 100d, Tight) }, StabilisationObservation.None);

        Assert.False(uncertain.IsAdmitted);
        Assert.True(precise.IsAdmitted);
        Assert.Equal(OperationalRegime.Stable, precise.Regime);
    }

    [Fact]
    public void Uncertainty_that_stays_inside_one_regime_does_not_prevent_pooling()
    {
        var registry = RegistryWithFixtureLayout();

        var admission = TransitionAwarePoolingGuard.Admit(
            registry, Scope, new[] { Sample(15, 100d, TimeSpan.FromMinutes(10)) }, StabilisationObservation.None);

        Assert.True(admission.IsAdmitted);
        Assert.Equal(OperationalRegime.Stable, admission.Regime);
    }

    // ------------------------------------------------------------ fail closed

    [Fact]
    public void An_empty_population_refuses_rather_than_admitting_nothing()
    {
        var registry = RegistryWithFixtureLayout();

        var admission = TransitionAwarePoolingGuard.Admit(
            registry, Scope, Array.Empty<RegimeScopedSample>(), StabilisationObservation.None);

        Assert.False(admission.IsAdmitted);
        Assert.Equal(PoolingGuardCodes.EmptyPopulation, admission.Code);
        Assert.Equal(0, admission.SampleCount);
    }

    [Fact]
    public void An_undeclared_scope_refuses_through_the_regime_classifier()
    {
        var registry = new OperationalTransitionRegistry();

        var admission = TransitionAwarePoolingGuard.Admit(
            registry, Scope, SteadySamples(), StabilisationObservation.None);

        Assert.False(admission.IsAdmitted);
        Assert.Equal(OperationalTransitionCodes.ScopeNotDeclared, admission.Code);
    }

    [Fact]
    public void Samples_from_different_scopes_are_refused()
    {
        // Different scopes have different declared transitions, so one regime verdict
        // over both would not mean anything.
        var registry = RegistryWithFixtureLayout();
        Assert.True(registry.TryDeclareScope(OtherScope, out _));

        var population = SteadySamples().Concat(new[] { Sample(10, 100d, Tight, OtherScope) }).ToArray();

        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, population, StabilisationObservation.None);

        Assert.False(admission.IsAdmitted);
        Assert.Equal(PoolingGuardCodes.HeterogeneousScope, admission.Code);
    }

    [Fact]
    public void A_missing_stabilisation_observation_refuses_through_the_classifier()
    {
        // The settling basis is a declared condition whose outcome the caller has not
        // supplied. The guard does not decide it on the caller's behalf.
        var registry = new OperationalTransitionRegistry();
        Assert.True(registry.TryDeclareScope(Scope, out _));
        Assert.True(registry.TryDeclareTransition(new TransitionDeclaration(
            Scope, KindCode, FromContext, ToContext, At(30), At(45),
            StabilisationBasis.Condition, default, 0, "declared_condition"), out _));

        var admission = TransitionAwarePoolingGuard.Admit(
            registry, Scope, new[] { Sample(50, 100d) }, StabilisationObservation.None);

        Assert.False(admission.IsAdmitted);
        Assert.Equal(OperationalTransitionCodes.StabilisationObservationNotSupplied, admission.Code);
    }

    [Fact]
    public void A_refusal_never_reports_a_regime_or_a_value()
    {
        var registry = RegistryWithFixtureLayout();
        var everything = SteadySamples().Concat(TransitionSamples()).ToArray();

        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, everything, StabilisationObservation.None);

        Assert.False(admission.IsAdmitted);
        Assert.Equal(OperationalRegime.Unknown, admission.Regime);
        Assert.Equal(TerminalState.RefusedByGuard, admission.Outcome);
        Assert.Equal(ExclusionAttribution.Declaration, admission.Attribution);
    }

    [Fact]
    public void An_admission_is_a_finding_and_names_exactly_one_regime()
    {
        var registry = RegistryWithFixtureLayout();

        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, SteadySamples(), StabilisationObservation.None);

        Assert.True(admission.IsAdmitted);
        Assert.Equal(TerminalState.Finding, admission.Outcome);
        Assert.Equal(ExclusionAttribution.None, admission.Attribution);
        Assert.NotEqual(OperationalRegime.Mixed, admission.Regime);
        Assert.NotEqual(OperationalRegime.Unknown, admission.Regime);
    }

    // --------------------------------------------------------- subject count

    [Fact]
    public void A_subject_count_basis_keeps_settling_samples_out_of_steady_state()
    {
        var registry = new OperationalTransitionRegistry();
        Assert.True(registry.TryDeclareScope(Scope, out _));
        Assert.True(registry.TryDeclareTransition(new TransitionDeclaration(
            Scope, KindCode, FromContext, ToContext, At(30), At(45),
            StabilisationBasis.SubjectCount, default, 3, ""), out _));

        var afterTransition = new[] { Sample(50, 99d), Sample(55, 101d) };

        var stillSettling = TransitionAwarePoolingGuard.Admit(
            registry, Scope, afterTransition, StabilisationObservation.WithSubjectsCompleted(1));

        var settled = TransitionAwarePoolingGuard.Admit(
            registry, Scope, afterTransition, StabilisationObservation.WithSubjectsCompleted(3));

        Assert.Equal(OperationalRegime.Stabilising, stillSettling.Regime);
        Assert.Equal(OperationalRegime.Stable, settled.Regime);
        Assert.True(stillSettling.IsAdmitted);
        Assert.True(settled.IsAdmitted);
    }

    [Fact]
    public void The_guard_computes_no_statistic_of_its_own()
    {
        // It admits or refuses a population. What is computed from an admitted one
        // belongs to the aggregation semantics kernel, not here.
        var registry = RegistryWithFixtureLayout();

        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, SteadySamples(), StabilisationObservation.None);

        var properties = admission.GetType().GetProperties().Select(p => p.Name).ToArray();

        Assert.DoesNotContain(properties, p => p.IndexOf("Mean", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.DoesNotContain(properties, p => p.IndexOf("Average", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.DoesNotContain(properties, p => p.IndexOf("Value", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void Selecting_a_regime_that_no_sample_belongs_to_returns_nothing_rather_than_everything()
    {
        var registry = RegistryWithFixtureLayout();

        var selected = TransitionAwarePoolingGuard.SelectRegime(
            registry, Scope, SteadySamples(), OperationalRegime.Transition, StabilisationObservation.None);

        Assert.Empty(selected);

        // And an empty selection is then refused rather than pooled.
        var admission = TransitionAwarePoolingGuard.Admit(registry, Scope, selected, StabilisationObservation.None);

        Assert.False(admission.IsAdmitted);
        Assert.Equal(PoolingGuardCodes.EmptyPopulation, admission.Code);
    }

    [Fact]
    public void Every_sample_in_this_suite_rests_on_the_committed_fixture_values()
    {
        Assert.Equal(GenericProcessFixture.StableRegimeValues.Count, SteadySamples().Length);
        Assert.Equal(GenericProcessFixture.TransitionRegimeValues.Count, TransitionSamples().Length);

        Assert.Equal(
            GenericProcessFixture.StableRegimeValues.ToArray(),
            SteadySamples().Select(s => s.Value).ToArray());

        Assert.Equal(
            GenericProcessFixture.TransitionRegimeValues.ToArray(),
            TransitionSamples().Select(s => s.Value).ToArray());
    }
}