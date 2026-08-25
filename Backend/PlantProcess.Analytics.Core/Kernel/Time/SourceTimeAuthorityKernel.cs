// Source Time Authority kernel.
//
// Backlog origin: T-216.
//
// Turns a raw source timestamp into an instant this product is willing to reason with,
// or refuses. It never guesses an offset, never substitutes the machine's timezone,
// never promotes ingestion time to event time, and never orders two instants whose
// uncertainty overlaps.
//
// The primary entry point takes no role argument. Role comes from the declaration of
// the timestamp signal, so a caller cannot relabel an arrival stamp as an event time by
// asking for a different role.
//
// Every path out of this file is an instant carrying its uncertainty, or a refusal
// carrying a code. There is no path that returns a bare instant presented as exact.
using System;

namespace PlantProcess.Analytics.Core.Kernel;

public static class SourceTimeAuthorityKernel
{
    /// <summary>
    /// Resolve a raw timestamp under the declared authority of one timestamp signal. The
    /// resulting instant carries the role that signal was declared to answer.
    /// </summary>
    public static SourceTimeResolution Resolve(
        SourceTimeAuthorityRegistry registry,
        string? sourceKey,
        string? signalKey,
        RawSourceTime raw)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(raw);

        if (!registry.TryGetSignal(sourceKey, signalKey, out var declaration) || declaration is null)
        {
            return Refuse(SourceTimeCodes.SignalNotDeclared);
        }

        var offset = ResolveOffset(declaration, raw, out var localValue, out var offsetCode);

        if (offset is null)
        {
            return Refuse(offsetCode);
        }

        if (!SourceTimeAuthorityRegistry.IsOffsetInRange(offset.Value))
        {
            return Refuse(SourceTimeCodes.OffsetDeclarationConflict);
        }

        var instant = new DateTimeOffset(
            DateTime.SpecifyKind(localValue, DateTimeKind.Unspecified),
            offset.Value);

        return new SourceTimeResolution(
            IsResolved: true,
            new TemporalInstant(
                instant,
                declaration.Role,
                declaration.SourceKey,
                declaration.SignalKey,
                declaration.Uncertainty),
            SourceTimeCodes.TimeResolved,
            TerminalState.Finding,
            ExclusionAttribution.None);
    }

    /// <summary>
    /// Resolve a raw timestamp and require that the signal was declared to answer a
    /// particular question. The role is verified against the declaration, never imposed
    /// on it.
    /// </summary>
    public static SourceTimeResolution ResolveAs(
        SourceTimeAuthorityRegistry registry,
        string? sourceKey,
        string? signalKey,
        TimeRole requiredRole,
        RawSourceTime raw)
    {
        ArgumentNullException.ThrowIfNull(registry);

        if (!registry.TryGetSignal(sourceKey, signalKey, out var declaration) || declaration is null)
        {
            return Refuse(SourceTimeCodes.SignalNotDeclared);
        }

        if (declaration.Role != requiredRole)
        {
            // Arrival time is always available and almost never the answer. A product
            // that substitutes it produces a complete, plausible, wrong timeline.
            return Refuse(requiredRole == TimeRole.Effective
                ? SourceTimeCodes.EffectiveTimeUnavailable
                : SourceTimeCodes.RoleNotAuthorised);
        }

        return Resolve(registry, sourceKey, signalKey, raw);
    }

    /// <summary>
    /// Order two resolved instants. Overlapping uncertainty yields Indeterminate rather
    /// than a coin flip dressed as a fact.
    /// </summary>
    public static TemporalOrderingVerdict Order(TemporalInstant first, TemporalInstant second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.LatestPossible < second.EarliestPossible)
        {
            return new TemporalOrderingVerdict(TemporalOrdering.Before, SourceTimeCodes.Ordered, TimeSpan.Zero);
        }

        if (second.LatestPossible < first.EarliestPossible)
        {
            return new TemporalOrderingVerdict(TemporalOrdering.After, SourceTimeCodes.Ordered, TimeSpan.Zero);
        }

        var overlapStart = first.EarliestPossible > second.EarliestPossible ? first.EarliestPossible : second.EarliestPossible;
        var overlapEnd = first.LatestPossible < second.LatestPossible ? first.LatestPossible : second.LatestPossible;
        var width = overlapEnd - overlapStart;

        if (width < TimeSpan.Zero) width = TimeSpan.Zero;

        return new TemporalOrderingVerdict(
            TemporalOrdering.Indeterminate,
            SourceTimeCodes.OrderingIndeterminate,
            width);
    }

    private static TimeSpan? ResolveOffset(
        TimeSignalDeclaration declaration,
        RawSourceTime raw,
        out DateTime localValue,
        out string code)
    {
        localValue = default;
        code = string.Empty;

        switch (declaration.OffsetOrigin)
        {
            case TimeOffsetOrigin.EmbeddedInValue:
                if (raw.OffsetBearingValue is null)
                {
                    // Declared to carry its own offset, and it does not. Nothing here will
                    // invent one, and Unspecified is not UTC.
                    code = SourceTimeCodes.OffsetNotDeclared;
                    return null;
                }

                // Preserved exactly as supplied: +02:00 and -05:00 stay distinguishable,
                // and neither is quietly normalised away.
                localValue = raw.OffsetBearingValue.Value.DateTime;
                return raw.OffsetBearingValue.Value.Offset;

            case TimeOffsetOrigin.DeclaredFixedOffset:
                if (raw.OffsetBearingValue is not null)
                {
                    // A value that already carries an offset contradicts a signal declared
                    // to carry none. Two answers is not better than one.
                    code = SourceTimeCodes.OffsetDeclarationConflict;
                    return null;
                }

                if (!TryTakeLocal(raw, out localValue, out code)) return null;

                return declaration.FixedOffset;

            case TimeOffsetOrigin.DeclaredZoneRule:
                if (raw.OffsetBearingValue is not null)
                {
                    code = SourceTimeCodes.OffsetDeclarationConflict;
                    return null;
                }

                if (raw.OffsetIsAmbiguous)
                {
                    // A local time that occurs twice under this zone's rules. Picking the
                    // first is a guess with an hour of error attached and no way to notice
                    // it later.
                    code = SourceTimeCodes.ClockAmbiguous;
                    return null;
                }

                if (raw.RuntimeResolvedOffset is null)
                {
                    // Zone rule resolution belongs to the platform lane. This kernel will
                    // not carry a rules table, and will not borrow the machine's zone.
                    code = SourceTimeCodes.ZoneRuleOffsetNotSupplied;
                    return null;
                }

                if (!TryTakeLocal(raw, out localValue, out code)) return null;

                return raw.RuntimeResolvedOffset;

            default:
                code = SourceTimeCodes.OffsetNotDeclared;
                return null;
        }
    }

    private static bool TryTakeLocal(RawSourceTime raw, out DateTime localValue, out string code)
    {
        localValue = default;
        code = string.Empty;

        if (raw.LocalValue is null)
        {
            code = SourceTimeCodes.OffsetNotDeclared;
            return false;
        }

        // Local means the machine that happened to run the import, which is not a
        // property of the plant. Utc carries an implied offset the declaration says is
        // not there.
        if (raw.LocalValue.Value.Kind != DateTimeKind.Unspecified)
        {
            code = raw.LocalValue.Value.Kind == DateTimeKind.Local
                ? SourceTimeCodes.OffsetNotDeclared
                : SourceTimeCodes.OffsetDeclarationConflict;

            return false;
        }

        localValue = raw.LocalValue.Value;
        return true;
    }

    private static SourceTimeResolution Refuse(string code) =>
        new(IsResolved: false,
            Instant: null,
            code,
            TerminalState.RefusedByGuard,
            ExclusionAttribution.Declaration);
}