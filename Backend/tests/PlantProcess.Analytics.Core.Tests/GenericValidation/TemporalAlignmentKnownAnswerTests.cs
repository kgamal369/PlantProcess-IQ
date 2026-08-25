// Temporal Alignment - deterministic acceptance.
//
// Backlog origin: T-217.
//
// The generic validation fixture supplies the two cases, and the source time authority
// supplies the instants. Exact bounds, so no verdict can pass by rounding luck:
//
//   overlapping pair (100 +/- 1s against 108 +/- 15min):
//       separation is between 0 and 23min 1s
//   disjoint pair    (100 +/- 1s against 140 +/- 15min):
//       separation is between 24min 59s and 55min 1s
//
// Those bounds decide every verdict below, and each case yields all three answers as
// the declared tolerance moves. A kernel that always returns the cautious answer fails
// here, and so does one that always decides.
using System;
using System.Linq;
using PlantProcess.Analytics.Core.Kernel;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

[Trait("BacklogTask", "T-217")]
public sealed class TemporalAlignmentKnownAnswerTests
{
    private const string PolicyKey = "POLICY-A";
    private const string MachineSource = "SOURCE-M";
    private const string ManualSource = "SOURCE-H";
    private const string EventSignal = "SIGNAL-1";
    private const string ArrivalSignal = "SIGNAL-2";

    private static readonly TimeSpan MachineUncertainty = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ManualUncertainty = TimeSpan.FromMinutes(15);

    private static TemporalAlignmentPolicyRegistry PolicyRegistry(TimeSpan tolerance)
    {
        var registry = new TemporalAlignmentPolicyRegistry();
        Assert.True(registry.TryDeclarePolicy(new TemporalAlignmentPolicy(PolicyKey, tolerance), out _));
        return registry;
    }

    private static TemporalInstant Instant(double atMinute, TimeSpan uncertainty, string source,
        TimeRole role = TimeRole.Effective, string signal = EventSignal) =>
        new(FrozenTestEpoch.AtMinute(atMinute), role, source, signal, uncertainty);

    private static TemporalInstant Machine(double atMinute) => Instant(atMinute, MachineUncertainty, MachineSource);
    private static TemporalInstant Manual(double atMinute) => Instant(atMinute, ManualUncertainty, ManualSource);

    // ------------------------------------------------------------- fail closed

    [Fact]
    public void No_tolerance_is_assumed_including_zero()
    {
        var registry = new TemporalAlignmentPolicyRegistry();

        Assert.Equal(0, registry.PolicyCount);

        var verdict = TemporalAlignmentKernel.Align(registry, PolicyKey, Machine(100), Manual(108));

        Assert.False(verdict.IsDecided);
        Assert.Null(verdict.Separation);
        Assert.Equal(TemporalAlignmentCodes.PolicyNotDeclared, verdict.Code);
        Assert.Equal(TerminalState.RefusedByGuard, verdict.Outcome);
        Assert.Equal(ExclusionAttribution.Declaration, verdict.Attribution);
    }

    [Fact]
    public void A_negative_tolerance_is_rejected_at_declaration()
    {
        var registry = new TemporalAlignmentPolicyRegistry();

        Assert.False(registry.TryDeclarePolicy(
            new TemporalAlignmentPolicy(PolicyKey, TimeSpan.FromSeconds(-1)), out var code));

        Assert.Equal(TemporalAlignmentCodes.NegativeTolerance, code);
        Assert.Equal(0, registry.PolicyCount);
    }

    [Fact]
    public void Fewer_than_two_instants_is_not_an_alignment_question()
    {
        var registry = PolicyRegistry(TimeSpan.Zero);

        var verdict = TemporalAlignmentKernel.Align(registry, PolicyKey, new[] { Machine(100) });

        Assert.False(verdict.IsDecided);
        Assert.Equal(TemporalAlignmentCodes.InsufficientInstants, verdict.Code);
    }

    [Fact]
    public void Instants_of_different_roles_are_a_category_error_not_a_close_call()
    {
        // The roles were kept distinct upstream precisely so an arrival stamp cannot be
        // compared against an event time by accident.
        var registry = PolicyRegistry(TimeSpan.FromHours(1));

        var effective = Machine(100);
        var ingestion = Instant(100, MachineUncertainty, MachineSource, TimeRole.Ingestion, ArrivalSignal);

        var verdict = TemporalAlignmentKernel.Align(registry, PolicyKey, effective, ingestion);

        Assert.False(verdict.IsDecided);
        Assert.Null(verdict.Separation);
        Assert.Equal(TemporalAlignmentCodes.IncomparableTimeRoles, verdict.Code);
    }

    [Fact]
    public void A_refusal_never_carries_a_separation()
    {
        var registry = new TemporalAlignmentPolicyRegistry();

        var verdict = TemporalAlignmentKernel.Align(registry, "POLICY-NEVER-DECLARED", Machine(100), Manual(108));

        Assert.False(verdict.IsDecided);
        Assert.Null(verdict.Separation);
    }

    // ------------------------------------------------------- separation bounds

    [Fact]
    public void Overlapping_uncertainty_gives_a_minimum_separation_of_zero()
    {
        var separation = TemporalAlignmentKernel.Separation(Machine(100), Manual(108));

        Assert.Equal(TimeSpan.Zero, separation.Minimum);
        Assert.Equal(TimeSpan.FromMinutes(23) + TimeSpan.FromSeconds(1), separation.Maximum);
    }

    [Fact]
    public void Disjoint_uncertainty_gives_a_positive_minimum_separation()
    {
        var separation = TemporalAlignmentKernel.Separation(Machine(100), Manual(140));

        Assert.Equal(TimeSpan.FromMinutes(24) + TimeSpan.FromSeconds(59), separation.Minimum);
        Assert.Equal(TimeSpan.FromMinutes(55) + TimeSpan.FromSeconds(1), separation.Maximum);
    }

    [Fact]
    public void Separation_is_symmetric()
    {
        var forward = TemporalAlignmentKernel.Separation(Machine(100), Manual(140));
        var backward = TemporalAlignmentKernel.Separation(Manual(140), Machine(100));

        Assert.Equal(forward, backward);
    }

    [Fact]
    public void Two_exact_instants_at_the_same_moment_have_no_separation_at_all()
    {
        var exact = Instant(100, TimeSpan.Zero, MachineSource);
        var separation = TemporalAlignmentKernel.Separation(exact, exact);

        Assert.Equal(TimeSpan.Zero, separation.Minimum);
        Assert.Equal(TimeSpan.Zero, separation.Maximum);
    }

    // --------------------------------------------------- the three verdicts

    [Fact]
    public void Overlapping_uncertainty_under_zero_tolerance_is_indeterminate()
    {
        // The source time authority law, carried forward: uncertainty that overlaps
        // cannot be silently ordered, and cannot be silently aligned either.
        var verdict = TemporalAlignmentKernel.Align(PolicyRegistry(TimeSpan.Zero), PolicyKey, Machine(100), Manual(108));

        Assert.True(verdict.IsDecided);
        Assert.Equal(TemporalAlignment.Indeterminate, verdict.Alignment);
        Assert.Equal(TemporalAlignmentCodes.Indeterminate, verdict.Code);
    }

    [Fact]
    public void A_tolerance_above_the_maximum_separation_decides_coincident()
    {
        // Maximum separation is 23min 1s, so 30 minutes settles it either way it falls.
        var verdict = TemporalAlignmentKernel.Align(
            PolicyRegistry(TimeSpan.FromMinutes(30)), PolicyKey, Machine(100), Manual(108));

        Assert.True(verdict.IsDecided);
        Assert.Equal(TemporalAlignment.Coincident, verdict.Alignment);
        Assert.Equal(TemporalAlignmentCodes.Coincident, verdict.Code);
    }

    [Fact]
    public void A_tolerance_below_the_minimum_separation_decides_separated()
    {
        // Minimum separation is 24min 59s, so even the most generous reading of the
        // evidence puts these more than 5 minutes apart.
        var verdict = TemporalAlignmentKernel.Align(
            PolicyRegistry(TimeSpan.FromMinutes(5)), PolicyKey, Machine(100), Manual(140));

        Assert.True(verdict.IsDecided);
        Assert.Equal(TemporalAlignment.Separated, verdict.Alignment);
        Assert.Equal(TemporalAlignmentCodes.Separated, verdict.Code);
    }

    [Fact]
    public void A_tolerance_inside_the_separation_bounds_is_indeterminate_even_for_the_disjoint_pair()
    {
        // 30 minutes sits between 24min 59s and 55min 1s. The evidence is compatible with
        // both answers, and saying so is the honest result.
        var verdict = TemporalAlignmentKernel.Align(
            PolicyRegistry(TimeSpan.FromMinutes(30)), PolicyKey, Machine(100), Manual(140));

        Assert.True(verdict.IsDecided);
        Assert.Equal(TemporalAlignment.Indeterminate, verdict.Alignment);
    }

    [Fact]
    public void Each_fixture_pair_yields_all_three_verdicts_as_the_tolerance_moves()
    {
        // One pair, three answers. A kernel cannot pass by always deciding, and cannot
        // pass by always refusing to.
        var pair = new[] { Machine(100), Manual(140) };

        var separated = TemporalAlignmentKernel.Align(PolicyRegistry(TimeSpan.FromMinutes(5)), PolicyKey, pair);
        var indeterminate = TemporalAlignmentKernel.Align(PolicyRegistry(TimeSpan.FromMinutes(30)), PolicyKey, pair);
        var coincident = TemporalAlignmentKernel.Align(PolicyRegistry(TimeSpan.FromMinutes(60)), PolicyKey, pair);

        Assert.Equal(TemporalAlignment.Separated, separated.Alignment);
        Assert.Equal(TemporalAlignment.Indeterminate, indeterminate.Alignment);
        Assert.Equal(TemporalAlignment.Coincident, coincident.Alignment);

        Assert.Equal(3, new[] { separated.Alignment, indeterminate.Alignment, coincident.Alignment }.Distinct().Count());
    }

    [Fact]
    public void Exact_instants_at_the_same_moment_are_coincident_even_at_zero_tolerance()
    {
        var exact = Instant(100, TimeSpan.Zero, MachineSource);
        var alsoExact = Instant(100, TimeSpan.Zero, ManualSource);

        var verdict = TemporalAlignmentKernel.Align(PolicyRegistry(TimeSpan.Zero), PolicyKey, exact, alsoExact);

        Assert.Equal(TemporalAlignment.Coincident, verdict.Alignment);
    }

    [Fact]
    public void The_tolerance_boundary_itself_counts_as_within_tolerance()
    {
        // Maximum separation is exactly 23min 1s. A tolerance of exactly that decides
        // coincident; one tick less does not.
        var maximum = TimeSpan.FromMinutes(23) + TimeSpan.FromSeconds(1);

        Assert.Equal(TemporalAlignment.Coincident,
            TemporalAlignmentKernel.Align(PolicyRegistry(maximum), PolicyKey, Machine(100), Manual(108)).Alignment);

        Assert.Equal(TemporalAlignment.Indeterminate,
            TemporalAlignmentKernel.Align(PolicyRegistry(maximum - TimeSpan.FromTicks(1)), PolicyKey, Machine(100), Manual(108)).Alignment);
    }

    [Fact]
    public void A_decided_verdict_always_reports_its_separation_bounds()
    {
        var verdict = TemporalAlignmentKernel.Align(
            PolicyRegistry(TimeSpan.FromMinutes(30)), PolicyKey, Machine(100), Manual(108));

        Assert.NotNull(verdict.Separation);
        Assert.True(verdict.Separation!.Minimum <= verdict.Separation.Maximum);
    }

    [Fact]
    public void An_indeterminate_verdict_is_a_finding_not_a_refusal()
    {
        // The evidence was admissible. What it supports is "cannot tell from this", which
        // is a result about the plant, not a gap in the declaration.
        var verdict = TemporalAlignmentKernel.Align(PolicyRegistry(TimeSpan.Zero), PolicyKey, Machine(100), Manual(108));

        Assert.True(verdict.IsDecided);
        Assert.Equal(TerminalState.Finding, verdict.Outcome);
        Assert.Equal(ExclusionAttribution.None, verdict.Attribution);
    }

    // ------------------------------------------------------------ sets

    [Fact]
    public void A_set_is_coincident_only_when_every_pair_is()
    {
        var registry = PolicyRegistry(TimeSpan.FromMinutes(30));

        var verdict = TemporalAlignmentKernel.Align(registry, PolicyKey,
            new[] { Machine(100), Manual(108), Machine(101) });

        Assert.Equal(TemporalAlignment.Coincident, verdict.Alignment);
    }

    [Fact]
    public void One_provably_separated_pair_separates_the_whole_set()
    {
        // Instants that cannot all be the same moment are not aligned, however close the
        // rest of them are.
        var registry = PolicyRegistry(TimeSpan.FromMinutes(5));

        var verdict = TemporalAlignmentKernel.Align(registry, PolicyKey,
            new[] { Machine(100), Machine(101), Manual(140) });

        Assert.Equal(TemporalAlignment.Separated, verdict.Alignment);
    }

    [Fact]
    public void An_undecidable_pair_makes_the_set_indeterminate_rather_than_coincident()
    {
        var registry = PolicyRegistry(TimeSpan.FromMinutes(20));

        var verdict = TemporalAlignmentKernel.Align(registry, PolicyKey,
            new[] { Machine(100), Machine(101), Manual(108) });

        Assert.Equal(TemporalAlignment.Indeterminate, verdict.Alignment);
    }

    [Fact]
    public void The_reported_bounds_belong_to_the_widest_pair_in_the_set()
    {
        var registry = PolicyRegistry(TimeSpan.FromHours(2));

        var verdict = TemporalAlignmentKernel.Align(registry, PolicyKey,
            new[] { Machine(100), Machine(101), Manual(140) });

        Assert.Equal(TemporalAlignmentKernel.Separation(Machine(100), Manual(140)), verdict.Separation);
    }

    // ------------------------------------------------- declaration invariants

    [Fact]
    public void An_identical_redeclaration_is_idempotent_and_a_conflicting_one_fails_closed()
    {
        var registry = PolicyRegistry(TimeSpan.FromMinutes(5));

        Assert.True(registry.TryDeclarePolicy(new TemporalAlignmentPolicy(PolicyKey, TimeSpan.FromMinutes(5)), out _));
        Assert.Equal(1, registry.PolicyCount);

        Assert.False(registry.TryDeclarePolicy(new TemporalAlignmentPolicy(PolicyKey, TimeSpan.FromMinutes(9)), out var code));
        Assert.Equal(TemporalAlignmentCodes.ConflictingDeclaration, code);

        // The original tolerance stands: a later arrival does not win by arriving later.
        Assert.True(registry.TryGetPolicy(PolicyKey, out var stored));
        Assert.Equal(TimeSpan.FromMinutes(5), stored!.Tolerance);
    }

    [Fact]
    public void Policy_identity_uses_the_same_trim_only_normalisation_as_the_rest_of_the_kernel()
    {
        var registry = PolicyRegistry(TimeSpan.FromMinutes(5));

        foreach (var variant in new[] { PolicyKey, "  " + PolicyKey, PolicyKey + "  " })
        {
            Assert.True(registry.TryGetPolicy(variant, out var policy));
            Assert.Equal(PolicyKey, policy!.PolicyKey);
        }

        Assert.Equal(1, registry.PolicyCount);

        // Case is identity, not noise.
        Assert.False(registry.TryGetPolicy(PolicyKey.ToLowerInvariant(), out _));
    }

    // ------------------------------------------- consumes the committed contract

    [Fact]
    public void Instants_resolved_by_the_source_time_authority_align_without_adaptation()
    {
        // End to end from the committed upstream contract: declare two signals, resolve
        // both, then align. Nothing is reconstructed by hand.
        var sources = new SourceTimeAuthorityRegistry();

        Assert.True(sources.TryDeclareSignal(new TimeSignalDeclaration(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, MachineUncertainty, TimeSpan.Zero,
            TimeUncertaintyConvention.ResolutionIsHalfWidth), out _));

        Assert.True(sources.TryDeclareSignal(new TimeSignalDeclaration(
            ManualSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, ManualUncertainty, TimeSpan.Zero,
            TimeUncertaintyConvention.ResolutionIsHalfWidth), out _));

        static RawSourceTime At(double minute) =>
            RawSourceTime.WithoutOffset(DateTime.SpecifyKind(FrozenTestEpoch.AtMinute(minute).UtcDateTime, DateTimeKind.Unspecified));

        var machine = SourceTimeAuthorityKernel.Resolve(sources, MachineSource, EventSignal, At(100)).Instant;
        var manual = SourceTimeAuthorityKernel.Resolve(sources, ManualSource, EventSignal, At(108)).Instant;

        Assert.NotNull(machine);
        Assert.NotNull(manual);

        var verdict = TemporalAlignmentKernel.Align(PolicyRegistry(TimeSpan.Zero), PolicyKey, machine!, manual!);

        Assert.True(verdict.IsDecided);
        Assert.Equal(TemporalAlignment.Indeterminate, verdict.Alignment);
        Assert.Equal(TimeSpan.Zero, verdict.Separation!.Minimum);
    }

    [Fact]
    public void The_declared_uncertainties_match_the_ones_the_fixture_already_carries()
    {
        var machineObservation = GenericProcessFixture.TemporalPairOverlapping
            .Single(o => o.Source == ObservationSourceKind.Machine);

        var manualObservation = GenericProcessFixture.TemporalPairOverlapping
            .Single(o => o.Source == ObservationSourceKind.Manual);

        Assert.Equal(machineObservation.ClockUncertainty, MachineUncertainty);
        Assert.Equal(manualObservation.ClockUncertainty, ManualUncertainty);
    }

    [Fact]
    public void Alignment_reports_temporal_compatibility_and_nothing_about_what_it_means()
    {
        // This contract answers whether instants may be treated as the same moment. What
        // a disagreement implies about the plant belongs downstream.
        var verdicts = Enum.GetNames(typeof(TemporalAlignment));

        Assert.Equal(3, verdicts.Length);
        Assert.Contains("Indeterminate", verdicts);
        Assert.DoesNotContain(verdicts, v => v.IndexOf("Conflict", StringComparison.OrdinalIgnoreCase) >= 0);
        Assert.DoesNotContain(verdicts, v => v.IndexOf("Misclassified", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}