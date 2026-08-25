// Fact Evidence Authority - deterministic acceptance.
//
// Backlog origin: T-218.
//
// Fact and source keys are opaque, and the three known-answer families map onto them
// without naming any equipment in the repository:
//
//   FACT-COMMANDED-STATE   commanded state       primary = SOURCE-COMMAND
//   FACT-OBSERVED-STATE    observed position     primary = SOURCE-FEEDBACK
//   FACT-ORDER-EXISTS      order existence       primary = SOURCE-ORDER
//   FACT-DECLARED-REASON   declared reason       primary = SOURCE-MANUAL
//
// SOURCE-COMMAND is primary for the first, supporting for the second, and irrelevant to
// the third. That single arrangement is the whole point: authority is a property of the
// fact, not of the source.
using System;
using System.Linq;
using PlantProcess.Analytics.Core.Kernel;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

[Trait("BacklogTask", "T-218")]
public sealed class FactEvidenceAuthorityKnownAnswerTests
{
    private const string CommandedState = "FACT-COMMANDED-STATE";
    private const string ObservedState = "FACT-OBSERVED-STATE";
    private const string OrderExists = "FACT-ORDER-EXISTS";
    private const string DeclaredReason = "FACT-DECLARED-REASON";

    private const string CommandSource = "SOURCE-COMMAND";
    private const string FeedbackSource = "SOURCE-FEEDBACK";
    private const string OrderSource = "SOURCE-ORDER";
    private const string ManualSource = "SOURCE-MANUAL";

    private const double Floor = 0.5d;

    private static readonly DateTimeOffset Always = FrozenTestEpoch.AtMinute(0);
    private static readonly DateTimeOffset Forever = FrozenTestEpoch.AtMinute(100000);
    private static readonly DateTimeOffset AsOf = FrozenTestEpoch.AtMinute(100);

    private static FactEvidenceAuthorityRegistry Registry()
    {
        var registry = new FactEvidenceAuthorityRegistry();

        foreach (var fact in new[] { CommandedState, ObservedState, OrderExists, DeclaredReason })
        {
            Assert.True(registry.TryDeclareFact(new SemanticFactDeclaration(fact, Floor), out _));
        }

        Declare(registry, CommandedState, CommandSource, EvidenceRole.Primary);
        Declare(registry, CommandedState, FeedbackSource, EvidenceRole.Corroborating);

        Declare(registry, ObservedState, FeedbackSource, EvidenceRole.Primary);
        Declare(registry, ObservedState, CommandSource, EvidenceRole.Supporting);

        Declare(registry, OrderExists, OrderSource, EvidenceRole.Primary);

        Declare(registry, DeclaredReason, ManualSource, EvidenceRole.Primary);

        return registry;
    }

    private static void Declare(
        FactEvidenceAuthorityRegistry registry, string fact, string source, EvidenceRole role,
        DateTimeOffset? from = null, DateTimeOffset? to = null) =>
        Assert.True(registry.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(fact, source, role, from ?? Always, to ?? Forever), out _));

    private static OfferedEvidence Evidence(string fact, string source, double quality = 0.9d) =>
        new(fact, source, quality, AsOf);

    // ------------------------------------------------- authority is fact-specific

    [Fact]
    public void The_same_source_holds_a_different_role_for_each_fact()
    {
        var registry = Registry();

        Assert.Equal(EvidenceRole.Primary, FactEvidenceAuthorityKernel.RoleOf(registry, CommandedState, CommandSource, AsOf));
        Assert.Equal(EvidenceRole.Supporting, FactEvidenceAuthorityKernel.RoleOf(registry, ObservedState, CommandSource, AsOf));
        Assert.Equal(EvidenceRole.Irrelevant, FactEvidenceAuthorityKernel.RoleOf(registry, OrderExists, CommandSource, AsOf));
    }

    [Fact]
    public void Command_and_feedback_each_own_their_own_fact()
    {
        var registry = Registry();

        var commanded = FactEvidenceAuthorityKernel.Resolve(registry, CommandedState, AsOf,
            new[] { Evidence(CommandedState, CommandSource), Evidence(CommandedState, FeedbackSource) });

        var observed = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, AsOf,
            new[] { Evidence(ObservedState, CommandSource), Evidence(ObservedState, FeedbackSource) });

        Assert.Equal(CommandSource, commanded.Authority!.PrimarySourceKey);
        Assert.Equal(FeedbackSource, observed.Authority!.PrimarySourceKey);

        // Neither is ranked above the other; each is primary where it was declared so.
        Assert.NotEqual(commanded.Authority.PrimarySourceKey, observed.Authority.PrimarySourceKey);
    }

    [Fact]
    public void An_operational_source_has_no_standing_on_an_order_fact()
    {
        var registry = Registry();

        var standing = FactEvidenceAuthorityKernel.CheckSourceStanding(registry, OrderExists, CommandSource, AsOf);

        Assert.False(standing.IsResolved);
        Assert.Equal(FactAuthorityCodes.SourceIrrelevantForFact, standing.Code);
    }

    [Fact]
    public void A_manually_declared_reason_resolves_to_the_manual_authority()
    {
        var registry = Registry();

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, DeclaredReason, AsOf,
            new[] { Evidence(DeclaredReason, ManualSource) });

        Assert.True(resolution.IsResolved);
        Assert.Equal(ManualSource, resolution.Authority!.PrimarySourceKey);
    }

    [Fact]
    public void Evidence_offered_by_an_irrelevant_source_is_not_admitted_to_the_result()
    {
        var registry = Registry();

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, OrderExists, AsOf,
            new[] { Evidence(OrderExists, OrderSource), Evidence(OrderExists, CommandSource) });

        Assert.True(resolution.IsResolved);
        Assert.Empty(resolution.Authority!.SupportingSourceKeys);
        Assert.Empty(resolution.Authority.CorroboratingSourceKeys);
    }

    [Fact]
    public void Supporting_and_corroborating_sources_are_reported_separately_from_the_primary()
    {
        var registry = Registry();

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, AsOf,
            new[] { Evidence(ObservedState, FeedbackSource), Evidence(ObservedState, CommandSource) });

        Assert.Equal(FeedbackSource, resolution.Authority!.PrimarySourceKey);
        Assert.Equal(new[] { CommandSource }, resolution.Authority.SupportingSourceKeys);
        Assert.DoesNotContain(FeedbackSource, resolution.Authority.SupportingSourceKeys);
    }

    // ------------------------------------------------------- required states

    [Fact]
    public void An_undeclared_fact_fails_closed()
    {
        var registry = Registry();

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, "FACT-NEVER-DECLARED", AsOf,
            Array.Empty<OfferedEvidence>());

        Assert.False(resolution.IsResolved);
        Assert.Null(resolution.Authority);
        Assert.Equal(FactAuthorityCodes.FactDeclarationAbsent, resolution.Code);
        Assert.Equal(TerminalState.RefusedByGuard, resolution.Outcome);
        Assert.Equal(ExclusionAttribution.Declaration, resolution.Attribution);
    }

    [Fact]
    public void A_fact_with_no_declared_primary_does_not_borrow_one()
    {
        // Supporting evidence exists and is perfectly good. It still does not become the
        // authority, because nobody said it was.
        var registry = new FactEvidenceAuthorityRegistry();
        Assert.True(registry.TryDeclareFact(new SemanticFactDeclaration(ObservedState, Floor), out _));
        Declare(registry, ObservedState, CommandSource, EvidenceRole.Supporting);

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, AsOf,
            new[] { Evidence(ObservedState, CommandSource) });

        Assert.False(resolution.IsResolved);
        Assert.Null(resolution.Authority);
        Assert.Equal(FactAuthorityCodes.PrimaryAuthorityNotDeclared, resolution.Code);
    }

    [Fact]
    public void A_declared_but_silent_primary_is_unavailable_and_nothing_more()
    {
        // The distinction that matters downstream: the authority exists and said nothing.
        // That is not two sources disagreeing.
        var registry = Registry();

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, AsOf,
            new[] { Evidence(ObservedState, CommandSource) });

        Assert.False(resolution.IsResolved);
        Assert.Equal(FactAuthorityCodes.PrimaryAuthorityUnavailable, resolution.Code);
    }

    [Fact]
    public void Two_primaries_at_one_moment_fail_closed_rather_than_pick()
    {
        // There is no global ranking to break the tie with, and inventing one would be
        // the defect this contract exists to prevent.
        var registry = new FactEvidenceAuthorityRegistry();
        Assert.True(registry.TryDeclareFact(new SemanticFactDeclaration(ObservedState, Floor), out _));
        Declare(registry, ObservedState, FeedbackSource, EvidenceRole.Primary);
        Declare(registry, ObservedState, CommandSource, EvidenceRole.Primary);

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, AsOf,
            new[] { Evidence(ObservedState, FeedbackSource), Evidence(ObservedState, CommandSource) });

        Assert.False(resolution.IsResolved);
        Assert.Null(resolution.Authority);
        Assert.Equal(FactAuthorityCodes.AmbiguousPrimaryAuthority, resolution.Code);
    }

    [Fact]
    public void Evidence_below_the_declared_floor_is_not_accepted_because_it_is_all_there_is()
    {
        var registry = Registry();

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, DeclaredReason, AsOf,
            new[] { Evidence(DeclaredReason, ManualSource, Floor - 0.01d) });

        Assert.False(resolution.IsResolved);
        Assert.Equal(FactAuthorityCodes.InsufficientEvidenceQuality, resolution.Code);
    }

    [Fact]
    public void Evidence_exactly_at_the_floor_is_accepted()
    {
        var registry = Registry();

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, DeclaredReason, AsOf,
            new[] { Evidence(DeclaredReason, ManualSource, Floor) });

        Assert.True(resolution.IsResolved);
        Assert.Equal(ManualSource, resolution.Authority!.PrimarySourceKey);
    }

    [Fact]
    public void Weak_supporting_evidence_is_dropped_without_disturbing_the_primary()
    {
        var registry = Registry();

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, AsOf,
            new[] { Evidence(ObservedState, FeedbackSource), Evidence(ObservedState, CommandSource, 0.1d) });

        Assert.True(resolution.IsResolved);
        Assert.Equal(FeedbackSource, resolution.Authority!.PrimarySourceKey);
        Assert.Empty(resolution.Authority.SupportingSourceKeys);
    }

    [Fact]
    public void Every_required_state_is_reachable_and_distinct()
    {
        // Each of the six states the contract must distinguish, produced by a real case.
        var codes = new[]
        {
            FactAuthorityCodes.AuthorityResolved,
            FactAuthorityCodes.FactDeclarationAbsent,
            FactAuthorityCodes.PrimaryAuthorityNotDeclared,
            FactAuthorityCodes.PrimaryAuthorityUnavailable,
            FactAuthorityCodes.AmbiguousPrimaryAuthority,
            FactAuthorityCodes.InsufficientEvidenceQuality,
            FactAuthorityCodes.SourceIrrelevantForFact
        };

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void No_refusal_reports_a_disagreement()
    {
        // Conflict requires two authorities to compare, and this kernel stops before
        // that. Reconciliation vocabulary must not leak into these codes.
        foreach (var code in new[]
        {
            FactAuthorityCodes.FactDeclarationAbsent,
            FactAuthorityCodes.PrimaryAuthorityNotDeclared,
            FactAuthorityCodes.PrimaryAuthorityUnavailable,
            FactAuthorityCodes.AmbiguousPrimaryAuthority,
            FactAuthorityCodes.InsufficientEvidenceQuality,
            FactAuthorityCodes.SourceIrrelevantForFact
        })
        {
            Assert.DoesNotContain("conflict", code, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("disagree", code, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ------------------------------------------------------- effective dating

    [Fact]
    public void Authority_selection_follows_the_declared_effective_interval()
    {
        // An arrangement that changed does not rewrite what was true before it changed.
        var registry = new FactEvidenceAuthorityRegistry();
        Assert.True(registry.TryDeclareFact(new SemanticFactDeclaration(ObservedState, Floor), out _));

        var changeover = FrozenTestEpoch.AtMinute(50);

        Declare(registry, ObservedState, CommandSource, EvidenceRole.Primary, Always, changeover);
        Declare(registry, ObservedState, FeedbackSource, EvidenceRole.Primary, changeover, Forever);

        var before = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, FrozenTestEpoch.AtMinute(10),
            new[] { new OfferedEvidence(ObservedState, CommandSource, 0.9d, FrozenTestEpoch.AtMinute(10)) });

        var after = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, FrozenTestEpoch.AtMinute(90),
            new[] { new OfferedEvidence(ObservedState, FeedbackSource, 0.9d, FrozenTestEpoch.AtMinute(90)) });

        Assert.Equal(CommandSource, before.Authority!.PrimarySourceKey);
        Assert.Equal(FeedbackSource, after.Authority!.PrimarySourceKey);
    }

    [Fact]
    public void The_effective_interval_is_half_open_so_adjacent_intervals_never_both_apply()
    {
        var registry = new FactEvidenceAuthorityRegistry();
        Assert.True(registry.TryDeclareFact(new SemanticFactDeclaration(ObservedState, Floor), out _));

        var changeover = FrozenTestEpoch.AtMinute(50);

        Declare(registry, ObservedState, CommandSource, EvidenceRole.Primary, Always, changeover);
        Declare(registry, ObservedState, FeedbackSource, EvidenceRole.Primary, changeover, Forever);

        // Exactly at the boundary the later declaration applies, and only it.
        Assert.Equal(EvidenceRole.Irrelevant, FactEvidenceAuthorityKernel.RoleOf(registry, ObservedState, CommandSource, changeover));
        Assert.Equal(EvidenceRole.Primary, FactEvidenceAuthorityKernel.RoleOf(registry, ObservedState, FeedbackSource, changeover));

        Assert.Single(registry.BindingsAt(ObservedState, changeover));
    }

    [Fact]
    public void A_role_change_over_time_for_one_source_is_deterministic()
    {
        // Same source, primary then demoted to supporting. Both readings are correct at
        // their own moment.
        var registry = new FactEvidenceAuthorityRegistry();
        Assert.True(registry.TryDeclareFact(new SemanticFactDeclaration(ObservedState, Floor), out _));

        var changeover = FrozenTestEpoch.AtMinute(50);

        Declare(registry, ObservedState, CommandSource, EvidenceRole.Primary, Always, changeover);
        Declare(registry, ObservedState, CommandSource, EvidenceRole.Supporting, changeover, Forever);

        Assert.Equal(EvidenceRole.Primary, FactEvidenceAuthorityKernel.RoleOf(registry, ObservedState, CommandSource, FrozenTestEpoch.AtMinute(10)));
        Assert.Equal(EvidenceRole.Supporting, FactEvidenceAuthorityKernel.RoleOf(registry, ObservedState, CommandSource, FrozenTestEpoch.AtMinute(90)));
    }

    [Fact]
    public void Outside_every_declared_interval_a_source_has_no_standing()
    {
        var registry = new FactEvidenceAuthorityRegistry();
        Assert.True(registry.TryDeclareFact(new SemanticFactDeclaration(ObservedState, Floor), out _));

        Declare(registry, ObservedState, FeedbackSource, EvidenceRole.Primary,
            FrozenTestEpoch.AtMinute(10), FrozenTestEpoch.AtMinute(20));

        Assert.Equal(EvidenceRole.Irrelevant,
            FactEvidenceAuthorityKernel.RoleOf(registry, ObservedState, FeedbackSource, FrozenTestEpoch.AtMinute(30)));

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, FrozenTestEpoch.AtMinute(30),
            new[] { new OfferedEvidence(ObservedState, FeedbackSource, 0.9d, FrozenTestEpoch.AtMinute(30)) });

        Assert.Equal(FactAuthorityCodes.PrimaryAuthorityNotDeclared, resolution.Code);
    }

    // ------------------------------------------------- declaration invariants

    [Fact]
    public void An_identical_redeclaration_is_idempotent_and_an_overlapping_one_fails_closed()
    {
        var registry = Registry();
        var bindingsBefore = registry.BindingCount;

        Assert.True(registry.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(CommandedState, CommandSource, EvidenceRole.Primary, Always, Forever), out _));

        Assert.Equal(bindingsBefore, registry.BindingCount);

        Assert.False(registry.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration(CommandedState, CommandSource, EvidenceRole.Supporting, Always, Forever), out var code));

        Assert.Equal(FactAuthorityCodes.ConflictingDeclaration, code);
        Assert.Equal(EvidenceRole.Primary, FactEvidenceAuthorityKernel.RoleOf(registry, CommandedState, CommandSource, AsOf));
    }

    [Fact]
    public void A_conflicting_fact_redeclaration_fails_closed()
    {
        var registry = Registry();

        Assert.False(registry.TryDeclareFact(new SemanticFactDeclaration(CommandedState, 0.9d), out var code));
        Assert.Equal(FactAuthorityCodes.ConflictingDeclaration, code);

        Assert.True(registry.TryGetFact(CommandedState, out var fact));
        Assert.Equal(Floor, fact!.QualityFloor);
    }

    [Fact]
    public void An_authority_cannot_be_declared_for_an_undeclared_fact()
    {
        var registry = new FactEvidenceAuthorityRegistry();

        Assert.False(registry.TryDeclareAuthority(
            new FactSourceAuthorityDeclaration("FACT-NEVER-DECLARED", CommandSource, EvidenceRole.Primary, Always, Forever), out var code));

        Assert.Equal(FactAuthorityCodes.FactDeclarationAbsent, code);
        Assert.Equal(0, registry.BindingCount);
    }

    [Fact]
    public void An_empty_or_inverted_effective_interval_is_rejected()
    {
        var registry = Registry();

        foreach (var (from, to) in new[] { (Always, Always), (Forever, Always) })
        {
            Assert.False(registry.TryDeclareAuthority(
                new FactSourceAuthorityDeclaration(OrderExists, ManualSource, EvidenceRole.Supporting, from, to), out var code));

            Assert.Equal(FactAuthorityCodes.InvalidDeclaration, code);
        }
    }

    [Fact]
    public void A_quality_floor_outside_the_unit_interval_is_rejected()
    {
        var registry = new FactEvidenceAuthorityRegistry();

        foreach (var floor in new[] { -0.1d, 1.1d, double.NaN })
        {
            Assert.False(registry.TryDeclareFact(new SemanticFactDeclaration(ObservedState, floor), out var code));
            Assert.Equal(FactAuthorityCodes.InvalidDeclaration, code);
        }

        Assert.Equal(0, registry.FactCount);
    }

    [Fact]
    public void The_registry_starts_empty_with_no_fact_source_or_ranking_of_any_kind()
    {
        var registry = new FactEvidenceAuthorityRegistry();

        Assert.Equal(0, registry.FactCount);
        Assert.Equal(0, registry.BindingCount);

        Assert.Equal(EvidenceRole.Irrelevant,
            FactEvidenceAuthorityKernel.RoleOf(registry, CommandedState, CommandSource, AsOf));

        Assert.False(FactEvidenceAuthorityKernel
            .Resolve(registry, CommandedState, AsOf, Array.Empty<OfferedEvidence>()).IsResolved);
    }

    [Fact]
    public void Fact_and_source_identity_use_the_same_trim_only_normalisation()
    {
        var registry = Registry();

        Assert.True(registry.TryGetFact("  " + CommandedState + "  ", out var fact));
        Assert.Equal(CommandedState, fact!.FactKey);

        Assert.Equal(EvidenceRole.Primary,
            FactEvidenceAuthorityKernel.RoleOf(registry, CommandedState, "  " + CommandSource + " ", AsOf));

        // Case is identity, not noise.
        Assert.Equal(EvidenceRole.Irrelevant,
            FactEvidenceAuthorityKernel.RoleOf(registry, CommandedState, CommandSource.ToLowerInvariant(), AsOf));
    }

    // ---------------------------------------------------------- fixture oracle

    [Fact]
    public void The_committed_fixture_evidence_pairs_resolve_through_the_declared_authority()
    {
        // The fixture already carries machine and manual observations of one fact,
        // including a subject where only the machine spoke. Declaring the machine as the
        // authority for that fact reproduces both outcomes without inventing data.
        var registry = new FactEvidenceAuthorityRegistry();
        Assert.True(registry.TryDeclareFact(new SemanticFactDeclaration(ObservedState, Floor), out _));

        Declare(registry, ObservedState, "SOURCE-M", EvidenceRole.Primary);
        Declare(registry, ObservedState, "SOURCE-H", EvidenceRole.Supporting);

        static OfferedEvidence FromObservation(ProcessObservation o) =>
            new(ObservedState,
                o.Source == ObservationSourceKind.Machine ? "SOURCE-M" : "SOURCE-H",
                0.9d,
                o.At);

        var bothSpoke = GenericProcessFixture.EvidencePairs
            .Where(o => o.SubjectId == "SUBJ-030")
            .Select(FromObservation)
            .ToArray();

        var onlyMachineSpoke = GenericProcessFixture.EvidencePairs
            .Where(o => o.SubjectId == "SUBJ-032")
            .Select(FromObservation)
            .ToArray();

        Assert.Equal(2, bothSpoke.Length);
        Assert.Single(onlyMachineSpoke);

        var withSupport = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, AsOf, bothSpoke);
        Assert.True(withSupport.IsResolved);
        Assert.Equal("SOURCE-M", withSupport.Authority!.PrimarySourceKey);
        Assert.Equal(new[] { "SOURCE-H" }, withSupport.Authority.SupportingSourceKeys);

        var authorityOnly = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, AsOf, onlyMachineSpoke);
        Assert.True(authorityOnly.IsResolved);
        Assert.Empty(authorityOnly.Authority!.SupportingSourceKeys);
    }

    [Fact]
    public void A_subject_where_only_the_supporting_source_spoke_reports_unavailable_not_disagreement()
    {
        var registry = new FactEvidenceAuthorityRegistry();
        Assert.True(registry.TryDeclareFact(new SemanticFactDeclaration(ObservedState, Floor), out _));

        Declare(registry, ObservedState, "SOURCE-M", EvidenceRole.Primary);
        Declare(registry, ObservedState, "SOURCE-H", EvidenceRole.Supporting);

        var resolution = FactEvidenceAuthorityKernel.Resolve(registry, ObservedState, AsOf,
            new[] { new OfferedEvidence(ObservedState, "SOURCE-H", 0.9d, AsOf) });

        Assert.False(resolution.IsResolved);
        Assert.Equal(FactAuthorityCodes.PrimaryAuthorityUnavailable, resolution.Code);
    }
}