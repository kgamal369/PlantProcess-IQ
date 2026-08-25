// Operational Transition and Stabilisation - deterministic acceptance.
//
// Backlog origin: T-234.
//
// The committed validation fixture already declares the regime layout this contract
// must reproduce for subject SUBJ-010: stable to minute 30, transition to minute 45,
// stabilising to minute 60. Declaring the transition and a fifteen-minute settling basis
// reproduces exactly that, without inventing a duration.
//
// Scope, kind and context keys are opaque throughout. Nothing here names a plant, an
// industry or a class of equipment.
using System;
using System.Linq;
using PlantProcess.Analytics.Core.Kernel;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

[Trait("BacklogTask", "T-234")]
public sealed class OperationalTransitionKnownAnswerTests
{
    private const string Scope = "SCOPE-A";
    private const string OtherScope = "SCOPE-B";
    private const string KindCode = "context_change";
    private const string FromContext = "CONTEXT-1";
    private const string ToContext = "CONTEXT-2";
    private const string ConditionCode = "declared_condition";

    private static DateTimeOffset At(double minute) => FrozenTestEpoch.AtMinute(minute);

    private static OperationalTransitionRegistry ScopeOnly(string scope = Scope)
    {
        var registry = new OperationalTransitionRegistry();
        Assert.True(registry.TryDeclareScope(scope, out _));
        return registry;
    }

    private static TransitionDeclaration Transition(
        StabilisationBasis basis,
        TimeSpan duration = default,
        int subjectCount = 0,
        string conditionCode = "",
        string scope = Scope,
        double start = 30,
        double end = 45) =>
        new(scope, KindCode, FromContext, ToContext, At(start), At(end), basis, duration, subjectCount, conditionCode);

    // ------------------------------------------------------------- fail closed

    [Fact]
    public void An_undeclared_scope_refuses_rather_than_answering_stable()
    {
        var registry = new OperationalTransitionRegistry();

        Assert.Equal(0, registry.ScopeCount);

        var classification = OperationalTransitionKernel.ClassifyInstant(
            registry, Scope, At(10), StabilisationObservation.None);

        Assert.False(classification.IsDecided);
        Assert.Equal(OperationalRegime.Unknown, classification.Regime);
        Assert.Equal(OperationalTransitionCodes.ScopeNotDeclared, classification.Code);
        Assert.Equal(TerminalState.RefusedByGuard, classification.Outcome);
        Assert.Equal(ExclusionAttribution.Declaration, classification.Attribution);
    }

    [Fact]
    public void A_time_basis_without_a_duration_is_not_a_declaration()
    {
        var registry = ScopeOnly();

        Assert.False(registry.TryDeclareTransition(
            Transition(StabilisationBasis.Time, duration: TimeSpan.Zero), out var code));

        Assert.Equal(OperationalTransitionCodes.StabilisationBasisNotDeclared, code);
        Assert.Equal(0, registry.TransitionCount);
    }

    [Fact]
    public void A_subject_count_basis_without_a_count_is_not_a_declaration()
    {
        var registry = ScopeOnly();

        Assert.False(registry.TryDeclareTransition(
            Transition(StabilisationBasis.SubjectCount, subjectCount: 0), out var code));

        Assert.Equal(OperationalTransitionCodes.StabilisationBasisNotDeclared, code);
    }

    [Fact]
    public void A_condition_basis_without_a_condition_is_not_a_declaration()
    {
        var registry = ScopeOnly();

        Assert.False(registry.TryDeclareTransition(
            Transition(StabilisationBasis.Condition, conditionCode: "   "), out var code));

        Assert.Equal(OperationalTransitionCodes.StabilisationBasisNotDeclared, code);
    }

    [Fact]
    public void Nothing_settles_a_transition_by_default()
    {
        // No duration, no count and no condition is not the same as "assume something
        // reasonable". Every one of those declarations is rejected until it carries its
        // own parameter.
        var registry = ScopeOnly();

        foreach (var basis in new[] { StabilisationBasis.Time, StabilisationBasis.SubjectCount, StabilisationBasis.Condition })
        {
            Assert.False(registry.TryDeclareTransition(Transition(basis), out _));
        }

        Assert.Equal(0, registry.TransitionCount);
    }

    [Fact]
    public void A_refusal_never_carries_a_regime_other_than_unknown()
    {
        var registry = new OperationalTransitionRegistry();

        var classification = OperationalTransitionKernel.ClassifyInstant(
            registry, "SCOPE-NEVER-DECLARED", At(10), StabilisationObservation.None);

        Assert.False(classification.IsDecided);
        Assert.Equal(OperationalRegime.Unknown, classification.Regime);
        Assert.Null(classification.Transition);
    }

    // ------------------------------------------------------- declared bases

    [Fact]
    public void A_declared_time_basis_reproduces_the_fixture_regime_layout()
    {
        // Fixture: stable to 30, transition to 45, stabilising to 60.
        var registry = ScopeOnly();
        Assert.True(registry.TryDeclareTransition(
            Transition(StabilisationBasis.Time, duration: TimeSpan.FromMinutes(15)), out _));

        OperationalRegime RegimeAt(double minute) =>
            OperationalTransitionKernel.ClassifyInstant(registry, Scope, At(minute), StabilisationObservation.None).Regime;

        Assert.Equal(OperationalRegime.Stable, RegimeAt(10));
        Assert.Equal(OperationalRegime.Transition, RegimeAt(35));
        Assert.Equal(OperationalRegime.Stabilising, RegimeAt(50));
        Assert.Equal(OperationalRegime.Stable, RegimeAt(70));
    }

    [Fact]
    public void The_fixture_declares_the_same_three_regimes_this_kernel_reproduces()
    {
        var fixtureRegimes = GenericProcessFixture.Regimes
            .Where(r => r.SubjectId == "SUBJ-010")
            .OrderBy(r => r.Start)
            .ToArray();

        Assert.Equal(3, fixtureRegimes.Length);
        Assert.Equal(ProcessRegime.Stable, fixtureRegimes[0].Regime);
        Assert.Equal(ProcessRegime.Transition, fixtureRegimes[1].Regime);
        Assert.Equal(ProcessRegime.Stabilising, fixtureRegimes[2].Regime);

        // The declaration this test builds uses the fixture's own boundaries, not
        // invented ones.
        Assert.Equal(At(30), fixtureRegimes[1].Start);
        Assert.Equal(At(45), fixtureRegimes[1].End);
        Assert.Equal(At(60), fixtureRegimes[2].End);
    }

    [Fact]
    public void A_declared_subject_count_basis_settles_on_subjects_not_on_the_clock()
    {
        var registry = ScopeOnly();
        Assert.True(registry.TryDeclareTransition(
            Transition(StabilisationBasis.SubjectCount, subjectCount: 3), out _));

        OperationalRegime RegimeAfter(int completed) =>
            OperationalTransitionKernel.ClassifyInstant(
                registry, Scope, At(1000), StabilisationObservation.WithSubjectsCompleted(completed)).Regime;

        // Hours later by the clock, and still stabilising until the subjects have run.
        Assert.Equal(OperationalRegime.Stabilising, RegimeAfter(0));
        Assert.Equal(OperationalRegime.Stabilising, RegimeAfter(2));
        Assert.Equal(OperationalRegime.Stable, RegimeAfter(3));
    }

    [Fact]
    public void A_declared_condition_basis_refuses_until_the_outcome_is_supplied()
    {
        var registry = ScopeOnly();
        Assert.True(registry.TryDeclareTransition(
            Transition(StabilisationBasis.Condition, conditionCode: ConditionCode), out _));

        var unsupplied = OperationalTransitionKernel.ClassifyInstant(
            registry, Scope, At(50), StabilisationObservation.None);

        Assert.False(unsupplied.IsDecided);
        Assert.Equal(OperationalTransitionCodes.StabilisationObservationNotSupplied, unsupplied.Code);

        var notYet = OperationalTransitionKernel.ClassifyInstant(
            registry, Scope, At(50), StabilisationObservation.WithConditionOutcome(false));

        var satisfied = OperationalTransitionKernel.ClassifyInstant(
            registry, Scope, At(50), StabilisationObservation.WithConditionOutcome(true));

        Assert.Equal(OperationalRegime.Stabilising, notYet.Regime);
        Assert.Equal(OperationalRegime.Stable, satisfied.Regime);
    }

    [Fact]
    public void An_explicit_none_basis_means_steady_state_resumes_immediately()
    {
        var registry = ScopeOnly();
        Assert.True(registry.TryDeclareTransition(Transition(StabilisationBasis.None), out _));

        Assert.Equal(OperationalRegime.Transition,
            OperationalTransitionKernel.ClassifyInstant(registry, Scope, At(35), StabilisationObservation.None).Regime);

        // The instant the transition ends. None is a declaration, not an omission.
        Assert.Equal(OperationalRegime.Stable,
            OperationalTransitionKernel.ClassifyInstant(registry, Scope, At(45), StabilisationObservation.None).Regime);
    }

    [Fact]
    public void An_explicit_none_basis_may_not_also_carry_a_settling_parameter()
    {
        var registry = ScopeOnly();

        Assert.False(registry.TryDeclareTransition(
            Transition(StabilisationBasis.None, duration: TimeSpan.FromMinutes(5)), out var code));

        Assert.Equal(OperationalTransitionCodes.InvalidDeclaration, code);
    }

    [Fact]
    public void The_transition_interval_is_half_open()
    {
        var registry = ScopeOnly();
        Assert.True(registry.TryDeclareTransition(Transition(StabilisationBasis.None), out _));

        Assert.Equal(OperationalRegime.Transition,
            OperationalTransitionKernel.ClassifyInstant(registry, Scope, At(30), StabilisationObservation.None).Regime);

        Assert.Equal(OperationalRegime.Stable,
            OperationalTransitionKernel.ClassifyInstant(registry, Scope, At(45), StabilisationObservation.None).Regime);
    }

    // ------------------------------------------- context decides classification

    [Fact]
    public void The_same_instant_is_classified_differently_under_different_declared_contexts()
    {
        // One moment, two scopes. The sample is identical; what the plant declared about
        // each scope is not, and that is the only thing that decides the regime.
        var registry = new OperationalTransitionRegistry();
        Assert.True(registry.TryDeclareScope(Scope, out _));
        Assert.True(registry.TryDeclareScope(OtherScope, out _));

        Assert.True(registry.TryDeclareTransition(
            Transition(StabilisationBasis.Time, duration: TimeSpan.FromMinutes(15)), out _));

        Assert.True(registry.TryDeclareTransition(
            Transition(StabilisationBasis.None, scope: OtherScope, start: 100, end: 110), out _));

        var underTransition = OperationalTransitionKernel.ClassifyInstant(registry, Scope, At(35), StabilisationObservation.None);
        var underSteady = OperationalTransitionKernel.ClassifyInstant(registry, OtherScope, At(35), StabilisationObservation.None);

        Assert.Equal(OperationalRegime.Transition, underTransition.Regime);
        Assert.Equal(OperationalRegime.Stable, underSteady.Regime);
    }

    [Fact]
    public void The_classification_names_the_declaration_it_came_from()
    {
        var registry = ScopeOnly();
        Assert.True(registry.TryDeclareTransition(
            Transition(StabilisationBasis.Time, duration: TimeSpan.FromMinutes(15)), out _));

        var classification = OperationalTransitionKernel.ClassifyInstant(registry, Scope, At(35), StabilisationObservation.None);

        Assert.NotNull(classification.Transition);
        Assert.Equal(KindCode, classification.Transition!.TransitionKindCode);
        Assert.Equal(FromContext, classification.Transition.FromContextCode);
        Assert.Equal(ToContext, classification.Transition.ToContextCode);
    }

    // ------------------------------------------------- a transition is not downtime

    [Fact]
    public void The_regime_vocabulary_contains_no_downtime_concept()
    {
        // A transition is the process doing something deliberate. Recording it as lost
        // time produces a plant that appears to fail constantly while behaving exactly as
        // intended.
        var regimes = Enum.GetNames(typeof(OperationalRegime));

        Assert.Equal(5, regimes.Length);
        Assert.DoesNotContain(regimes, r => r.IndexOf("Down", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.DoesNotContain(regimes, r => r.IndexOf("Stop", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.DoesNotContain(regimes, r => r.IndexOf("Fault", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.DoesNotContain(regimes, r => r.IndexOf("Loss", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void A_transition_is_a_decided_finding_and_never_a_refusal()
    {
        var registry = ScopeOnly();
        Assert.True(registry.TryDeclareTransition(Transition(StabilisationBasis.None), out _));

        var classification = OperationalTransitionKernel.ClassifyInstant(registry, Scope, At(35), StabilisationObservation.None);

        Assert.True(classification.IsDecided);
        Assert.Equal(TerminalState.Finding, classification.Outcome);
        Assert.Equal(ExclusionAttribution.None, classification.Attribution);
    }

    // ---------------------------------------------------------------- windows

    [Fact]
    public void A_window_inside_one_regime_reports_that_regime()
    {
        var registry = ScopeOnly();
        Assert.True(registry.TryDeclareTransition(
            Transition(StabilisationBasis.Time, duration: TimeSpan.FromMinutes(15)), out _));

        var stable = OperationalTransitionKernel.ClassifyWindow(registry, Scope, At(0), At(30), StabilisationObservation.None);
        var transition = OperationalTransitionKernel.ClassifyWindow(registry, Scope, At(31), At(44), StabilisationObservation.None);

        Assert.Equal(OperationalRegime.Stable, stable.Regime);
        Assert.Equal(OperationalRegime.Transition, transition.Regime);
    }

    [Fact]
    public void A_window_spanning_regimes_is_mixed_under_the_code_the_fixture_names()
    {
        // This is the condition that makes pooling samples invalid, and the string is the
        // one the committed fixture already carries.
        var registry = ScopeOnly();
        Assert.True(registry.TryDeclareTransition(
            Transition(StabilisationBasis.Time, duration: TimeSpan.FromMinutes(15)), out _));

        var window = OperationalTransitionKernel.ClassifyWindow(registry, Scope, At(0), At(60), StabilisationObservation.None);

        Assert.True(window.IsDecided);
        Assert.Equal(OperationalRegime.Mixed, window.Regime);
        Assert.Equal(OperationalTransitionCodes.MixedProcessRegime, window.Code);
        Assert.Equal(ContinuousProcessKnownAnswers.RequiredRegimeRefusalCode, window.Code);
    }

    [Fact]
    public void An_empty_or_inverted_window_refuses()
    {
        var registry = ScopeOnly();

        foreach (var (from, to) in new[] { (At(10), At(10)), (At(20), At(10)) })
        {
            var window = OperationalTransitionKernel.ClassifyWindow(registry, Scope, from, to, StabilisationObservation.None);

            Assert.False(window.IsDecided);
            Assert.Equal(OperationalTransitionCodes.EmptyWindow, window.Code);
        }
    }

    [Fact]
    public void A_window_on_an_undeclared_scope_refuses()
    {
        var registry = new OperationalTransitionRegistry();

        var window = OperationalTransitionKernel.ClassifyWindow(registry, Scope, At(0), At(60), StabilisationObservation.None);

        Assert.False(window.IsDecided);
        Assert.Equal(OperationalTransitionCodes.ScopeNotDeclared, window.Code);
    }

    // ------------------------------------------------- declaration invariants

    [Fact]
    public void An_identical_redeclaration_is_idempotent_and_an_overlapping_one_fails_closed()
    {
        var registry = ScopeOnly();
        var declaration = Transition(StabilisationBasis.None);

        Assert.True(registry.TryDeclareTransition(declaration, out _));
        Assert.True(registry.TryDeclareTransition(declaration, out _));
        Assert.Equal(1, registry.TransitionCount);

        Assert.False(registry.TryDeclareTransition(
            Transition(StabilisationBasis.None, start: 40, end: 50), out var code));

        Assert.Equal(OperationalTransitionCodes.ConflictingDeclaration, code);
        Assert.Equal(1, registry.TransitionCount);
    }

    [Fact]
    public void Adjacent_transitions_on_one_scope_are_permitted()
    {
        var registry = ScopeOnly();

        Assert.True(registry.TryDeclareTransition(Transition(StabilisationBasis.None, start: 30, end: 45), out _));
        Assert.True(registry.TryDeclareTransition(Transition(StabilisationBasis.None, start: 45, end: 60), out _));

        Assert.Equal(2, registry.TransitionCount);
    }

    [Fact]
    public void A_transition_cannot_be_declared_on_an_undeclared_scope()
    {
        var registry = new OperationalTransitionRegistry();

        Assert.False(registry.TryDeclareTransition(Transition(StabilisationBasis.None), out var code));

        Assert.Equal(OperationalTransitionCodes.ScopeNotDeclared, code);
        Assert.Equal(0, registry.TransitionCount);
    }

    [Fact]
    public void An_empty_or_inverted_transition_interval_is_rejected()
    {
        var registry = ScopeOnly();

        foreach (var (start, end) in new[] { (30d, 30d), (45d, 30d) })
        {
            Assert.False(registry.TryDeclareTransition(
                Transition(StabilisationBasis.None, start: start, end: end), out var code));

            Assert.Equal(OperationalTransitionCodes.InvalidDeclaration, code);
        }
    }

    [Fact]
    public void A_transition_without_a_declared_kind_or_context_is_rejected()
    {
        var registry = ScopeOnly();

        foreach (var declaration in new[]
        {
            new TransitionDeclaration(Scope, "  ", FromContext, ToContext, At(30), At(45), StabilisationBasis.None, default, 0, ""),
            new TransitionDeclaration(Scope, KindCode, "  ", ToContext, At(30), At(45), StabilisationBasis.None, default, 0, ""),
            new TransitionDeclaration(Scope, KindCode, FromContext, "  ", At(30), At(45), StabilisationBasis.None, default, 0, "")
        })
        {
            Assert.False(registry.TryDeclareTransition(declaration, out var code));
            Assert.Equal(OperationalTransitionCodes.InvalidDeclaration, code);
        }
    }

    [Fact]
    public void Scope_and_context_identity_use_the_same_trim_only_normalisation()
    {
        var registry = ScopeOnly();
        Assert.True(registry.TryDeclareTransition(
            Transition(StabilisationBasis.None, scope: "  " + Scope + "  "), out _));

        Assert.True(registry.IsScopeDeclared("  " + Scope + " "));
        Assert.Single(registry.TransitionsFor(Scope));

        // Case is identity, not noise.
        Assert.False(registry.IsScopeDeclared(Scope.ToLowerInvariant()));
    }

    [Fact]
    public void The_transition_kind_is_an_opaque_declared_code_not_a_fixed_vocabulary()
    {
        // Whatever the customer calls it, this contract stores and classifies by it. The
        // original enumeration ended in "custom", which is the set admitting it is open.
        var registry = ScopeOnly();

        var kinds = new[] { "context_change", "setup", "cleaning", "configuration_change", "campaign_boundary", "recovery", "anything_the_customer_declares" };

        var start = 0d;

        foreach (var kind in kinds)
        {
            Assert.True(registry.TryDeclareTransition(
                new TransitionDeclaration(Scope, kind, FromContext, ToContext, At(start), At(start + 5),
                    StabilisationBasis.None, default, 0, ""), out _));

            start += 10;
        }

        Assert.Equal(kinds.Length, registry.TransitionCount);
    }
}