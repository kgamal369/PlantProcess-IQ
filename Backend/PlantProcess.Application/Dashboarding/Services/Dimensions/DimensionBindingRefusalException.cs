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