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

    /// <summary>
    /// The declaration binds to a canonical entity that carries no mapped single-column
    /// reference to the subject entity of this population. There is no path, so there is
    /// no answer; inventing a join would invent a number.
    /// </summary>
    public const string SubjectLinkAbsent = "DB06_subject_link_absent";

    /// <summary>
    /// The declaration's entity references the subject entity through more than one
    /// mapped reference. Choosing one would be a guess about meaning, so the engine
    /// refuses and the declaration must state which relationship it means.
    /// </summary>
    public const string SubjectLinkAmbiguous = "DB07_subject_link_ambiguous";

    /// <summary>
    /// The declaration is executable but this composition carries no resolver able to
    /// reach a related entity. A capability that is absent is reported as absent.
    /// </summary>
    public const string SubjectLinkUnavailable = "DB08_subject_link_unavailable";

    /// <summary>
    /// A keyed filter arrived on the wire in a shape that names no code or no
    /// value. It is refused as itself, never silently dropped: a dropped filter
    /// widens the population and reports the wider number as the answer.
    /// </summary>
    public const string FilterMalformed = "DB09_dimension_filter_malformed";
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