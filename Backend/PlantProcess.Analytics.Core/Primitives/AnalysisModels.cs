using System.Collections.Generic;

namespace PlantProcess.Analytics.Core.Primitives;

public enum AnalysisStatus { Ok, InsufficientData }

/// <summary>Metadata that every transparent-analysis result must carry (v4 6.1).</summary>
public sealed record AnalysisMetadata(
    string Formula,
    string Dataset,
    IReadOnlyList<string> Filters,
    string TimeWindow,
    DateTimeOffset RefreshedAtUtc,
    int SampleSize,
    string? Unit);

public sealed record AnalysisContext(
    string Dataset,
    IReadOnlyList<string> Filters,
    string TimeWindow,
    DateTimeOffset RefreshedAtUtc,
    string? Unit = null);

public sealed record AnalysisResult(
    string Primitive,
    double? Value,
    AnalysisStatus Status,
    AnalysisMetadata Metadata,
    IReadOnlyDictionary<string, double>? Extras = null,
    string? Label = null,
    string? Message = null)
{
    public bool MetadataComplete =>
        !string.IsNullOrWhiteSpace(Metadata.Formula) &&
        !string.IsNullOrWhiteSpace(Metadata.Dataset) &&
        Metadata.Filters != null &&
        !string.IsNullOrWhiteSpace(Metadata.TimeWindow) &&
        Metadata.RefreshedAtUtc != default;
}