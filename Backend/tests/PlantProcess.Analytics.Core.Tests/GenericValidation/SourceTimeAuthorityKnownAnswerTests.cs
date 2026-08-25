// Source Time Authority - deterministic acceptance.
//
// Backlog origin: T-216.
//
// The generic validation fixture is the oracle. It already carries the two temporal
// cases this contract must separate: a machine instant at minute 100 with one second of
// uncertainty against a manual instant at minute 108 with fifteen minutes of it, whose
// intervals overlap; and the same machine instant against a manual one at minute 140,
// whose intervals do not. Overlap means the question has no answer. Disjoint means it
// does. A kernel that always returns the cautious verdict fails here, and so does one
// that always picks.
using System;
using System.Linq;
using PlantProcess.Analytics.Core.Kernel;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

[Trait("BacklogTask", "T-216")]
public sealed class SourceTimeAuthorityKnownAnswerTests
{
    // Opaque keys. Nothing here names a protocol, a vendor or an industry, and nothing
    // needs to.
    private const string MachineSource = "SOURCE-M";
    private const string ManualSource = "SOURCE-H";

    private const string EventSignal = "SIGNAL-1";
    private const string ArrivalSignal = "SIGNAL-2";
    private const string ZoneKey = "ZONE-A";

    private static readonly DateTime Naive = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);

    private static TimeSignalDeclaration Signal(
        string source,
        string signal,
        TimeRole role,
        TimeOffsetOrigin origin,
        TimeSpan fixedOffset,
        string zoneKey,
        TimeSpan resolution,
        TimeSpan skew,
        TimeUncertaintyConvention convention = TimeUncertaintyConvention.ResolutionIsHalfWidth) =>
        new(source, signal, role, origin, fixedOffset, zoneKey, resolution, skew, convention);

    private static SourceTimeAuthorityRegistry RegistryWithSignals()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, TimeSpan.FromSeconds(1), TimeSpan.Zero), out _));

        Assert.True(registry.TryDeclareSignal(Signal(
            ManualSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, TimeSpan.FromMinutes(15), TimeSpan.Zero), out _));

        return registry;
    }

    private static TemporalInstant Resolve(SourceTimeAuthorityRegistry registry, string source, double atMinute)
    {
        // Offsets from the fixture's frozen epoch, so nothing here reads the wall clock.
        var value = DateTime.SpecifyKind(FrozenTestEpoch.AtMinute(atMinute).UtcDateTime, DateTimeKind.Unspecified);

        var resolution = SourceTimeAuthorityKernel.Resolve(
            registry, source, EventSignal, RawSourceTime.WithoutOffset(value));

        Assert.True(resolution.IsResolved);
        return resolution.Instant!;
    }

    // ------------------------------------------------------------- fail closed

    [Fact]
    public void The_registry_starts_empty_with_no_signal_zone_or_assumed_clock_quality()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.Equal(0, registry.SignalCount);

        var resolution = SourceTimeAuthorityKernel.Resolve(
            registry, MachineSource, EventSignal, RawSourceTime.WithoutOffset(Naive));

        Assert.False(resolution.IsResolved);
        Assert.Null(resolution.Instant);
        Assert.Equal(SourceTimeCodes.SignalNotDeclared, resolution.Code);
        Assert.Equal(ExclusionAttribution.Declaration, resolution.Attribution);
    }

    [Fact]
    public void An_undeclared_offset_origin_is_rejected_at_declaration()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.False(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.Undeclared,
            TimeSpan.Zero, string.Empty, TimeSpan.Zero, TimeSpan.Zero), out var code));

        Assert.Equal(SourceTimeCodes.OffsetNotDeclared, code);
        Assert.Equal(0, registry.SignalCount);
    }

    [Fact]
    public void A_refusal_never_carries_an_instant()
    {
        var registry = RegistryWithSignals();

        var resolution = SourceTimeAuthorityKernel.Resolve(
            registry, "SOURCE-NEVER-DECLARED", EventSignal, RawSourceTime.WithoutOffset(Naive));

        Assert.False(resolution.IsResolved);
        Assert.Null(resolution.Instant);
        Assert.Equal(TerminalState.RefusedByGuard, resolution.Outcome);
        Assert.Equal(ExclusionAttribution.Declaration, resolution.Attribution);
    }

    // ------------------------------------------- authority binds to the signal

    [Fact]
    public void Authority_binds_to_a_signal_not_to_a_source()
    {
        // One source, two timestamp fields. The arrival stamp cannot answer when
        // something happened merely because the source also carries a field that can.
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, TimeSpan.Zero, TimeSpan.Zero), out _));

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, ArrivalSignal, TimeRole.Ingestion, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, TimeSpan.Zero, TimeSpan.Zero), out _));

        var effective = SourceTimeAuthorityKernel.ResolveAs(
            registry, MachineSource, EventSignal, TimeRole.Effective, RawSourceTime.WithoutOffset(Naive));

        Assert.True(effective.IsResolved);
        Assert.Equal(TimeRole.Effective, effective.Instant!.Role);

        var substituted = SourceTimeAuthorityKernel.ResolveAs(
            registry, MachineSource, ArrivalSignal, TimeRole.Effective, RawSourceTime.WithoutOffset(Naive));

        Assert.False(substituted.IsResolved);
        Assert.Null(substituted.Instant);
        Assert.Equal(SourceTimeCodes.EffectiveTimeUnavailable, substituted.Code);
    }

    [Fact]
    public void A_caller_cannot_relabel_a_value_by_asking_for_a_different_role()
    {
        // The primary entry point takes no role argument: the instant comes back carrying
        // the role its signal was declared to answer, whatever the caller intended.
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, ArrivalSignal, TimeRole.Ingestion, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, TimeSpan.Zero, TimeSpan.Zero), out _));

        var resolved = SourceTimeAuthorityKernel.Resolve(
            registry, MachineSource, ArrivalSignal, RawSourceTime.WithoutOffset(Naive));

        Assert.True(resolved.IsResolved);
        Assert.Equal(TimeRole.Ingestion, resolved.Instant!.Role);
        Assert.NotEqual(TimeRole.Effective, resolved.Instant.Role);
    }

    [Fact]
    public void Requesting_a_non_effective_role_the_signal_does_not_hold_is_refused_distinctly()
    {
        var registry = RegistryWithSignals();

        var asserted = SourceTimeAuthorityKernel.ResolveAs(
            registry, MachineSource, EventSignal, TimeRole.SourceAsserted, RawSourceTime.WithoutOffset(Naive));

        Assert.False(asserted.IsResolved);
        Assert.Equal(SourceTimeCodes.RoleNotAuthorised, asserted.Code);
        Assert.NotEqual(SourceTimeCodes.EffectiveTimeUnavailable, asserted.Code);
    }

    [Fact]
    public void The_resolved_instant_names_the_signal_it_came_from()
    {
        var registry = RegistryWithSignals();
        var instant = Resolve(registry, MachineSource, 100);

        Assert.Equal(MachineSource, instant.SourceKey);
        Assert.Equal(EventSignal, instant.SignalKey);
    }

    // ------------------------------------------------------ offset handling

    [Fact]
    public void An_embedded_offset_is_preserved_rather_than_flattened_to_utc()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.EmbeddedInValue,
            TimeSpan.Zero, string.Empty, TimeSpan.Zero, TimeSpan.Zero), out _));

        foreach (var offset in new[] { TimeSpan.FromHours(2), TimeSpan.FromHours(-5), TimeSpan.Zero })
        {
            var value = new DateTimeOffset(2026, 8, 24, 15, 20, 0, offset);

            var resolution = SourceTimeAuthorityKernel.Resolve(
                registry, MachineSource, EventSignal, RawSourceTime.WithEmbeddedOffset(value));

            Assert.True(resolution.IsResolved);
            Assert.Equal(offset, resolution.Instant!.Instant.Offset);
            Assert.Equal(value.UtcDateTime, resolution.Instant.Instant.UtcDateTime);
        }
    }

    [Fact]
    public void Distinct_embedded_offsets_stay_distinguishable()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.EmbeddedInValue,
            TimeSpan.Zero, string.Empty, TimeSpan.Zero, TimeSpan.Zero), out _));

        var plusTwo = SourceTimeAuthorityKernel.Resolve(registry, MachineSource, EventSignal,
            RawSourceTime.WithEmbeddedOffset(new DateTimeOffset(2026, 8, 24, 15, 20, 0, TimeSpan.FromHours(2))));

        var minusFive = SourceTimeAuthorityKernel.Resolve(registry, MachineSource, EventSignal,
            RawSourceTime.WithEmbeddedOffset(new DateTimeOffset(2026, 8, 24, 15, 20, 0, TimeSpan.FromHours(-5))));

        Assert.NotEqual(plusTwo.Instant!.Instant.Offset, minusFive.Instant!.Instant.Offset);
        Assert.NotEqual(plusTwo.Instant.Instant.UtcDateTime, minusFive.Instant.Instant.UtcDateTime);
    }

    [Fact]
    public void An_embedded_offset_signal_refuses_a_value_that_carries_none()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.EmbeddedInValue,
            TimeSpan.Zero, string.Empty, TimeSpan.Zero, TimeSpan.Zero), out _));

        var resolution = SourceTimeAuthorityKernel.Resolve(
            registry, MachineSource, EventSignal, RawSourceTime.WithoutOffset(Naive));

        Assert.False(resolution.IsResolved);
        Assert.Equal(SourceTimeCodes.OffsetNotDeclared, resolution.Code);
    }

    [Fact]
    public void A_declared_fixed_offset_is_applied_and_carried()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.FromHours(2), string.Empty, TimeSpan.Zero, TimeSpan.Zero), out _));

        var resolution = SourceTimeAuthorityKernel.Resolve(
            registry, MachineSource, EventSignal, RawSourceTime.WithoutOffset(Naive));

        Assert.True(resolution.IsResolved);
        Assert.Equal(TimeSpan.FromHours(2), resolution.Instant!.Instant.Offset);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), resolution.Instant.Instant.UtcDateTime);
    }

    [Fact]
    public void An_offset_bearing_value_contradicts_a_fixed_offset_signal()
    {
        var registry = RegistryWithSignals();

        var resolution = SourceTimeAuthorityKernel.Resolve(
            registry, MachineSource, EventSignal,
            RawSourceTime.WithEmbeddedOffset(new DateTimeOffset(Naive, TimeSpan.FromHours(3))));

        Assert.False(resolution.IsResolved);
        Assert.Equal(SourceTimeCodes.OffsetDeclarationConflict, resolution.Code);
    }

    [Fact]
    public void Machine_local_time_is_never_authority()
    {
        var registry = RegistryWithSignals();
        var local = DateTime.SpecifyKind(Naive, DateTimeKind.Local);

        var resolution = SourceTimeAuthorityKernel.Resolve(
            registry, MachineSource, EventSignal, RawSourceTime.WithoutOffset(local));

        Assert.False(resolution.IsResolved);
        Assert.Equal(SourceTimeCodes.OffsetNotDeclared, resolution.Code);
    }

    [Fact]
    public void An_out_of_range_offset_is_rejected_wherever_it_comes_from()
    {
        var registry = new SourceTimeAuthorityRegistry();

        // At declaration, for a fixed offset.
        Assert.False(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.FromHours(20), string.Empty, TimeSpan.Zero, TimeSpan.Zero), out var code));

        Assert.Equal(SourceTimeCodes.OffsetDeclarationConflict, code);

        // And at resolution, for a runtime-resolved zone offset.
        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredZoneRule,
            TimeSpan.Zero, ZoneKey, TimeSpan.Zero, TimeSpan.Zero), out _));

        var resolution = SourceTimeAuthorityKernel.Resolve(
            registry, MachineSource, EventSignal,
            RawSourceTime.WithRuntimeResolvedOffset(Naive, TimeSpan.FromHours(-20), false));

        Assert.False(resolution.IsResolved);
        Assert.Equal(SourceTimeCodes.OffsetDeclarationConflict, resolution.Code);
    }

    // ---------------------------------------------------------- zone authority

    [Fact]
    public void A_zone_rule_signal_must_name_which_zone_authority_was_declared()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.False(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredZoneRule,
            TimeSpan.Zero, "   ", TimeSpan.Zero, TimeSpan.Zero), out var code));

        Assert.Equal(SourceTimeCodes.ZoneNotDeclared, code);
        Assert.Equal(0, registry.SignalCount);
    }

    [Fact]
    public void A_zone_key_is_normalised_and_preserved_for_the_runtime_to_resolve()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredZoneRule,
            TimeSpan.Zero, "  " + ZoneKey + "  ", TimeSpan.Zero, TimeSpan.Zero), out _));

        Assert.True(registry.TryGetSignal(MachineSource, EventSignal, out var declaration));
        Assert.Equal(ZoneKey, declaration!.ZoneKey);
    }

    [Fact]
    public void A_fixed_offset_signal_does_not_require_a_zone_and_may_not_carry_two_authorities()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.FromHours(1), string.Empty, TimeSpan.Zero, TimeSpan.Zero), out _));

        Assert.False(registry.TryDeclareSignal(Signal(
            ManualSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.FromHours(1), ZoneKey, TimeSpan.Zero, TimeSpan.Zero), out var code));

        Assert.Equal(SourceTimeCodes.OffsetDeclarationConflict, code);
    }

    [Fact]
    public void A_zone_rule_signal_refuses_until_the_runtime_supplies_the_offset()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredZoneRule,
            TimeSpan.Zero, ZoneKey, TimeSpan.Zero, TimeSpan.Zero), out _));

        var withoutOffset = SourceTimeAuthorityKernel.Resolve(
            registry, MachineSource, EventSignal, RawSourceTime.WithoutOffset(Naive));

        Assert.False(withoutOffset.IsResolved);
        Assert.Equal(SourceTimeCodes.ZoneRuleOffsetNotSupplied, withoutOffset.Code);

        var withOffset = SourceTimeAuthorityKernel.Resolve(
            registry, MachineSource, EventSignal,
            RawSourceTime.WithRuntimeResolvedOffset(Naive, TimeSpan.FromHours(1), false));

        Assert.True(withOffset.IsResolved);
        Assert.Equal(TimeSpan.FromHours(1), withOffset.Instant!.Instant.Offset);
    }

    [Fact]
    public void An_ambiguous_local_time_refuses_rather_than_picking_one_of_the_two()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredZoneRule,
            TimeSpan.Zero, ZoneKey, TimeSpan.Zero, TimeSpan.Zero), out _));

        var resolution = SourceTimeAuthorityKernel.Resolve(
            registry, MachineSource, EventSignal, RawSourceTime.AmbiguousUnderZoneRules(Naive));

        Assert.False(resolution.IsResolved);
        Assert.Null(resolution.Instant);
        Assert.Equal(SourceTimeCodes.ClockAmbiguous, resolution.Code);
    }

    // ------------------------------------------------------------ time quality

    [Fact]
    public void The_uncertainty_convention_is_declared_and_not_assumed()
    {
        // The two readings of Resolution differ by a factor of two. Which one applies is
        // stated on the declaration, never inferred.
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2),
            TimeUncertaintyConvention.ResolutionIsHalfWidth), out _));

        Assert.True(registry.TryDeclareSignal(Signal(
            ManualSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2),
            TimeUncertaintyConvention.ResolutionIsQuantisationStep), out _));

        Assert.True(registry.TryGetSignal(MachineSource, EventSignal, out var halfWidth));
        Assert.True(registry.TryGetSignal(ManualSource, EventSignal, out var quantised));

        // Half-width: the whole 2s contributes, plus 2s of skew.
        Assert.Equal(TimeSpan.FromSeconds(4), halfWidth!.Uncertainty);

        // Quantisation step: a value quantised to 2s lies within 1s of the truth.
        Assert.Equal(TimeSpan.FromSeconds(3), quantised!.Uncertainty);

        Assert.NotEqual(halfWidth.Uncertainty, quantised.Uncertainty);
    }

    [Fact]
    public void Uncertainty_travels_with_the_instant_and_is_never_zero_by_default()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)), out _));

        var instant = SourceTimeAuthorityKernel.Resolve(
            registry, MachineSource, EventSignal, RawSourceTime.WithoutOffset(Naive)).Instant;

        Assert.NotNull(instant);
        Assert.Equal(TimeSpan.FromSeconds(3), instant!.Uncertainty);
        Assert.Equal(TimeSpan.FromSeconds(6), instant.LatestPossible - instant.EarliestPossible);
    }

    [Fact]
    public void Negative_declared_quality_is_rejected()
    {
        var registry = new SourceTimeAuthorityRegistry();

        Assert.False(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, TimeSpan.FromSeconds(-1), TimeSpan.Zero), out var code));

        Assert.Equal(SourceTimeCodes.TimeQualityNotDeclared, code);
    }

    // ------------------------------------------------- declaration invariants

    [Fact]
    public void An_identical_redeclaration_is_idempotent_and_a_conflicting_one_fails_closed()
    {
        var registry = RegistryWithSignals();

        Assert.True(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, TimeSpan.FromSeconds(1), TimeSpan.Zero), out _));

        Assert.Equal(2, registry.SignalCount);

        Assert.False(registry.TryDeclareSignal(Signal(
            MachineSource, EventSignal, TimeRole.Ingestion, TimeOffsetOrigin.DeclaredFixedOffset,
            TimeSpan.Zero, string.Empty, TimeSpan.FromSeconds(1), TimeSpan.Zero), out var code));

        Assert.Equal(SourceTimeCodes.ConflictingDeclaration, code);

        // The original role stands: a later arrival does not win by arriving later.
        Assert.True(registry.TryGetSignal(MachineSource, EventSignal, out var stored));
        Assert.Equal(TimeRole.Effective, stored!.Role);
    }

    [Fact]
    public void Signal_identity_uses_the_same_trim_only_normalisation_as_the_rest_of_the_kernel()
    {
        var registry = RegistryWithSignals();

        foreach (var variant in new[] { MachineSource, "  " + MachineSource, MachineSource + "  " })
        {
            Assert.True(registry.TryGetSignal(variant, "  " + EventSignal + " ", out var declaration));
            Assert.Equal(MachineSource, declaration!.SourceKey);
            Assert.Equal(EventSignal, declaration.SignalKey);
        }

        Assert.Equal(2, registry.SignalCount);

        // Case is identity, not noise.
        Assert.False(registry.TryGetSignal(MachineSource.ToLowerInvariant(), EventSignal, out _));
    }

    // ------------------------------------------------------ ordering oracle

    [Fact]
    public void Overlapping_uncertainty_makes_ordering_indeterminate()
    {
        var registry = RegistryWithSignals();

        var verdict = SourceTimeAuthorityKernel.Order(
            Resolve(registry, MachineSource, 100), Resolve(registry, ManualSource, 108));

        Assert.Equal(TemporalOrdering.Indeterminate, verdict.Ordering);
        Assert.Equal(SourceTimeCodes.OrderingIndeterminate, verdict.Code);
        Assert.True(verdict.OverlapWidth > TimeSpan.Zero);
    }

    [Fact]
    public void Disjoint_uncertainty_yields_a_real_ordering()
    {
        var registry = RegistryWithSignals();

        var machine = Resolve(registry, MachineSource, 100);
        var manual = Resolve(registry, ManualSource, 140);

        Assert.Equal(TemporalOrdering.Before, SourceTimeAuthorityKernel.Order(machine, manual).Ordering);
        Assert.Equal(TemporalOrdering.After, SourceTimeAuthorityKernel.Order(manual, machine).Ordering);
    }

    [Fact]
    public void The_two_fixture_temporal_cases_produce_different_verdicts()
    {
        // One fixture, both answers. A kernel cannot pass by always returning the
        // cautious verdict, and cannot pass by always picking one.
        var registry = RegistryWithSignals();

        var overlapping = SourceTimeAuthorityKernel.Order(
            Resolve(registry, MachineSource, 100), Resolve(registry, ManualSource, 108));

        var disjoint = SourceTimeAuthorityKernel.Order(
            Resolve(registry, MachineSource, 100), Resolve(registry, ManualSource, 140));

        Assert.NotEqual(overlapping.Ordering, disjoint.Ordering);
    }

    [Fact]
    public void The_declared_quality_matches_the_uncertainty_the_fixture_already_carries()
    {
        // The declarations above are not invented to fit: they reproduce the uncertainty
        // the committed fixture records for its machine and manual observations.
        var machineObservation = GenericProcessFixture.TemporalPairOverlapping
            .Single(o => o.Source == ObservationSourceKind.Machine);

        var manualObservation = GenericProcessFixture.TemporalPairOverlapping
            .Single(o => o.Source == ObservationSourceKind.Manual);

        var registry = RegistryWithSignals();

        Assert.True(registry.TryGetSignal(MachineSource, EventSignal, out var machine));
        Assert.True(registry.TryGetSignal(ManualSource, EventSignal, out var manual));

        Assert.Equal(machineObservation.ClockUncertainty, machine!.Uncertainty);
        Assert.Equal(manualObservation.ClockUncertainty, manual!.Uncertainty);
    }

    [Fact]
    public void An_instant_is_never_presented_as_exact()
    {
        var registry = RegistryWithSignals();
        var instant = Resolve(registry, ManualSource, 100);

        Assert.True(instant.Uncertainty > TimeSpan.Zero);
        Assert.True(instant.EarliestPossible < instant.Instant);
        Assert.True(instant.LatestPossible > instant.Instant);
    }

    [Fact]
    public void No_protocol_vocabulary_is_required_to_declare_a_signal()
    {
        // Source, signal and zone are opaque. One industrial protocol's time and quality
        // model must not become the universal product model.
        var registry = new SourceTimeAuthorityRegistry();

        foreach (var key in new[] { "A", "source-42", "SOURCE-WITH-A-LONG-OPAQUE-NAME" })
        {
            Assert.True(registry.TryDeclareSignal(Signal(
                key, EventSignal, TimeRole.Effective, TimeOffsetOrigin.DeclaredFixedOffset,
                TimeSpan.Zero, string.Empty, TimeSpan.FromSeconds(1), TimeSpan.Zero), out _));
        }

        Assert.Equal(3, registry.SignalCount);
    }
}