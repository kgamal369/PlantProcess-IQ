// Source Time Authority contract.
//
// Backlog origin: T-216.
//
// Establishes what a timestamp means before anything is allowed to compare, order or
// align two of them. Three roles are kept distinct on purpose:
//
//   SourceAsserted - the instant a source system claims
//   Effective      - when the thing happened in plant reality
//   Ingestion      - when this product received the record
//
// Authority binds to a declared timestamp SIGNAL, not to a source. A source commonly
// carries several timestamp fields at once, and a source-level permission would let a
// caller request Effective while handing over the arrival stamp. Role therefore comes
// from the declaration of that signal; a caller cannot relabel a value by asking for a
// different role.
//
// Nothing here is protocol-specific. Source, signal and zone are opaque declared keys
// with declared offset origin and declared time quality. One industrial protocol's
// time and quality model must not become the universal product model.
//
// Persistence and runtime projection belong to the platform lane. This contract does
// not carry a timezone rules table; it carries which zone authority was declared, so
// that the runtime can resolve it and hand the offset back.
using System;
using System.Collections.Generic;

namespace PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// Which question a timestamp answers. Never interchangeable.
/// </summary>
public enum TimeRole
{
    SourceAsserted,
    Effective,
    Ingestion
}

/// <summary>
/// Where a timestamp's offset from UTC comes from. Undeclared is a real state and the
/// most important one: a value with no declared offset is not UTC by default, it is
/// unusable.
/// </summary>
public enum TimeOffsetOrigin
{
    Undeclared,

    /// <summary>The value itself carries an explicit offset, which is preserved as given.</summary>
    EmbeddedInValue,

    /// <summary>A constant offset declared for this signal.</summary>
    DeclaredFixedOffset,

    /// <summary>
    /// A named zone whose rules vary over the year. This contract records which zone was
    /// declared; the runtime resolves the offset for a given local instant and supplies
    /// it back. No rules table lives here, and no machine timezone is ever substituted.
    /// </summary>
    DeclaredZoneRule
}

/// <summary>
/// What the declared Resolution means. Stated explicitly because the two readings differ
/// by a factor of two, and a product that picks one silently has a hidden statistical
/// convention buried in its timeline.
/// </summary>
public enum TimeUncertaintyConvention
{
    /// <summary>Resolution is already a conservative half-width contribution.</summary>
    ResolutionIsHalfWidth,

    /// <summary>
    /// Resolution is the quantisation step the source can express. A value quantised to a
    /// step lies within half a step of the truth, so the contribution is Step / 2.
    /// </summary>
    ResolutionIsQuantisationStep
}

/// <summary>
/// One timestamp signal on one source, and the single role it is authorised to answer.
/// </summary>
public sealed record TimeSignalDeclaration(
    string SourceKey,
    string SignalKey,
    TimeRole Role,
    TimeOffsetOrigin OffsetOrigin,
    TimeSpan FixedOffset,
    string ZoneKey,
    TimeSpan Resolution,
    TimeSpan MaxClockSkew,
    TimeUncertaintyConvention UncertaintyConvention)
{
    /// <summary>
    /// The half-width of the interval in which the true instant lies. Resolution's
    /// contribution follows the declared convention; skew adds to it. Neither excuses
    /// the other.
    /// </summary>
    public TimeSpan Uncertainty =>
        (UncertaintyConvention == TimeUncertaintyConvention.ResolutionIsQuantisationStep
            ? new TimeSpan(Resolution.Ticks / 2)
            : Resolution)
        + MaxClockSkew;
}

/// <summary>
/// A timestamp as it arrives, before authority has been applied.
/// <para>
/// Exactly one of the two value forms is populated. An offset-bearing value keeps the
/// offset it was given - +02:00 and -05:00 stay distinguishable, and neither becomes
/// UTC. A local value carries no offset at all and is unusable until a declaration or a
/// runtime-resolved offset supplies one.
/// </para>
/// </summary>
public sealed record RawSourceTime(
    DateTimeOffset? OffsetBearingValue,
    DateTime? LocalValue,
    TimeSpan? RuntimeResolvedOffset,
    bool OffsetIsAmbiguous)
{
    /// <summary>A value that genuinely carries its own offset, preserved as supplied.</summary>
    public static RawSourceTime WithEmbeddedOffset(DateTimeOffset value) =>
        new(value, null, null, false);

    /// <summary>A value with no offset. Kind must be Unspecified; machine-local is not authority.</summary>
    public static RawSourceTime WithoutOffset(DateTime localValue) =>
        new(null, localValue, null, false);

    /// <summary>A local value whose offset the runtime resolved from a declared zone.</summary>
    public static RawSourceTime WithRuntimeResolvedOffset(DateTime localValue, TimeSpan resolvedOffset, bool isAmbiguous) =>
        new(null, localValue, resolvedOffset, isAmbiguous);

    /// <summary>A local value the runtime found ambiguous under the declared zone's rules.</summary>
    public static RawSourceTime AmbiguousUnderZoneRules(DateTime localValue) =>
        new(null, localValue, null, true);
}

/// <summary>
/// An instant this product is willing to reason with, carrying the role it was admitted
/// under and the uncertainty it was admitted with. The interval travels with the instant
/// so that no consumer can silently treat it as exact.
/// </summary>
public sealed record TemporalInstant(
    DateTimeOffset Instant,
    TimeRole Role,
    string SourceKey,
    string SignalKey,
    TimeSpan Uncertainty)
{
    public DateTimeOffset EarliestPossible => Instant - Uncertainty;
    public DateTimeOffset LatestPossible => Instant + Uncertainty;
}

/// <summary>
/// Whether two instants can be ordered at all. Indeterminate is a result, not a failure
/// to compute one: overlapping uncertainty means the question has no answer, and saying
/// so is more useful than picking.
/// </summary>
public enum TemporalOrdering
{
    Indeterminate,
    Before,
    After
}

public sealed record SourceTimeResolution(
    bool IsResolved,
    TemporalInstant? Instant,
    string Code,
    TerminalState Outcome,
    ExclusionAttribution Attribution);

public sealed record TemporalOrderingVerdict(
    TemporalOrdering Ordering,
    string Code,
    TimeSpan OverlapWidth);

/// <summary>
/// Refusal and permission codes. Stable strings, so a consumer can branch on them
/// without parsing prose.
/// </summary>
public static class SourceTimeCodes
{
    public const string SignalNotDeclared = "ST01 time_signal_not_declared";
    public const string OffsetNotDeclared = "ST02 time_offset_not_declared";
    public const string RoleNotAuthorised = "ST03 time_role_not_authorised";
    public const string EffectiveTimeUnavailable = "ST04 effective_time_unavailable";
    public const string TimeQualityNotDeclared = "ST05 time_quality_not_declared";
    public const string ClockAmbiguous = "ST06 clock_ambiguous";
    public const string ConflictingDeclaration = "ST07 conflicting_declaration";
    public const string ZoneRuleOffsetNotSupplied = "ST08 zone_rule_offset_not_supplied";
    public const string OffsetDeclarationConflict = "ST09 offset_declaration_conflict";
    public const string OrderingIndeterminate = "ST10 ordering_indeterminate";
    public const string ZoneNotDeclared = "ST11 zone_authority_not_declared";

    public const string TimeResolved = "ST20 source_time_resolved";
    public const string Ordered = "ST21 instants_ordered";
}

/// <summary>
/// The timestamp signal declarations in force. Starts empty: no source, no signal, no
/// default zone, no assumed clock quality. Declaration invariants match the analysis
/// subject and grain registry, because a durable store should not hold two different
/// ideas about what redeclaration means.
/// </summary>
public sealed class SourceTimeAuthorityRegistry
{
    /// <summary>The widest offset from UTC any real zone uses.</summary>
    public static readonly TimeSpan MaximumOffset = TimeSpan.FromHours(14);

    private readonly Dictionary<(string Source, string Signal), TimeSignalDeclaration> _signals = new();

    public int SignalCount => _signals.Count;

    public static bool IsOffsetInRange(TimeSpan offset) => offset >= -MaximumOffset && offset <= MaximumOffset;

    public bool TryDeclareSignal(TimeSignalDeclaration declaration, out string code)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        if (!DeclaredKey.TryNormalise(declaration.SourceKey, out var sourceKey) ||
            !DeclaredKey.TryNormalise(declaration.SignalKey, out var signalKey))
        {
            code = SourceTimeCodes.SignalNotDeclared;
            return false;
        }

        if (declaration.OffsetOrigin == TimeOffsetOrigin.Undeclared)
        {
            code = SourceTimeCodes.OffsetNotDeclared;
            return false;
        }

        var hasZoneKey = DeclaredKey.TryNormalise(declaration.ZoneKey, out var zoneKey);

        if (declaration.OffsetOrigin == TimeOffsetOrigin.DeclaredZoneRule)
        {
            // Knowing that a zone applies is not knowing which one. The runtime cannot
            // resolve an offset from an anonymous zone, and must never fall back to the
            // machine's own.
            if (!hasZoneKey)
            {
                code = SourceTimeCodes.ZoneNotDeclared;
                return false;
            }
        }
        else if (hasZoneKey)
        {
            // Two offset authorities for one signal is not more information, it is a
            // question about which one wins.
            code = SourceTimeCodes.OffsetDeclarationConflict;
            return false;
        }

        if (declaration.OffsetOrigin == TimeOffsetOrigin.DeclaredFixedOffset &&
            !IsOffsetInRange(declaration.FixedOffset))
        {
            code = SourceTimeCodes.OffsetDeclarationConflict;
            return false;
        }

        // Silence about clock quality is not a claim of perfection.
        if (declaration.Resolution < TimeSpan.Zero || declaration.MaxClockSkew < TimeSpan.Zero)
        {
            code = SourceTimeCodes.TimeQualityNotDeclared;
            return false;
        }

        var normalised = declaration with
        {
            SourceKey = sourceKey,
            SignalKey = signalKey,
            ZoneKey = hasZoneKey ? zoneKey : string.Empty
        };

        var key = (sourceKey, signalKey);

        if (_signals.TryGetValue(key, out var existing))
        {
            if (existing == normalised)
            {
                code = string.Empty;
                return true;
            }

            code = SourceTimeCodes.ConflictingDeclaration;
            return false;
        }

        _signals[key] = normalised;
        code = string.Empty;
        return true;
    }

    public bool TryGetSignal(string? sourceKey, string? signalKey, out TimeSignalDeclaration? declaration)
    {
        declaration = null;

        if (!DeclaredKey.TryNormalise(sourceKey, out var source) ||
            !DeclaredKey.TryNormalise(signalKey, out var signal))
        {
            return false;
        }

        return _signals.TryGetValue((source, signal), out declaration);
    }
}