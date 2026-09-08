using System;

namespace PlantProcess.Application.Dashboarding.Services.Dimensions;

/// <summary>
/// Typed governed refusal. Registry presence alone is not executable authority: a
/// declared dimension the engine cannot bind for the requested source is refused by
/// code, never guessed, never substituted, never folded into an empty result.
/// </summary>
public static class DimensionBindingRefusalCodes
{
    public const string Undeclared = "DB01_dimension_undeclared";
    public const string Unbindable = "DB02_source_field_unbindable";
    public const string SourceMismatch = "DB03_source_grain_mismatch";
    public const string MeasureUnsupported = "DB04_measure_not_declared_capable";
    public const string TenantUnresolved = "DB05_tenant_unresolved";

    // DB06, DB07 and DB08 are retired.
    //
    // DB06 (no subject link) and DB07 (ambiguous subject link) existed because the
    // interim T-094 path chose a cross-entity link itself by counting mapped
    // references. That choice now belongs to the relationship authority, whose own
    // codes name the same events: no governed path is RL03 and an ungoverned choice
    // between paths is RL01. Keeping DB06/DB07 would keep a second vocabulary for one
    // event. DB08 named a composition without a resolver; that composition is no
    // longer a lawful production state, and a missing required service is a
    // construction defect rather than a customer refusal.

    /// <summary>
    /// A keyed filter arrived on the wire in a shape that names no code or no
    /// value. It is refused as itself, never silently dropped: a dropped filter
    /// widens the population and reports the wider number as the answer.
    /// </summary>
    public const string FilterMalformed = "DB09_dimension_filter_malformed";

    /// <summary>
    /// T-094 stage 2B. A retired generic filter parameter was supplied.
    ///
    /// DECLARED IN 2B-i, RAISED IN 2B-ii. The constant exists now so the cutover
    /// commit can wire the retirement guard without also introducing a new code
    /// in the same change. Nothing raises it while the current frontend still
    /// legitimately sends the legacy parameters.
    ///
    /// When it is wired, the guard recognises the old key ONLY to refuse it.
    /// Translating it would preserve a second semantic authority, and ignoring it
    /// would widen the population and report the wider number as a valid answer.
    /// </summary>
    public const string LegacyFilterUnsupported = "DB10_legacy_dimension_filter_unsupported";
}

public sealed class DimensionBindingRefusalException : Exception
{
    public DimensionBindingRefusalException(string refusalCode, string dimensionCode, string message)
        : base(message)
    {
        RefusalCode = refusalCode;
        DimensionCode = dimensionCode;
    }

    public string RefusalCode { get; }

    public string DimensionCode { get; }
}