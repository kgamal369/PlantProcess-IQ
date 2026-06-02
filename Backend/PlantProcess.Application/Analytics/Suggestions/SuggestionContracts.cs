using PlantProcess.Application.Provenance;

namespace PlantProcess.Application.Analytics.Suggestions;

public enum FindingKind { Risk, Correlation, ValueImpact, DataQuality, Job }

public enum SuggestionStatus { Open, Assigned, Accepted, Rejected, Closed, Dismissed }

/// <summary>An APPROVED finding the engine may turn into a suggestion. Carries its own resolvable handle.</summary>
public sealed record ApprovedFinding(
    FindingKind Kind,
    string FindingRef,
    ProvenanceHandle Handle,
    string? Subject,            // e.g. coil id, parameter code
    string? DefectCode,
    int SampleSize,
    double Stability,          // 0..1
    double DataQuality,        // 0..1
    decimal? ImpactLow,        // from the value engine (ranged)
    decimal? ImpactHigh,
    bool IsSynthetic = false);

public sealed record SuggestionCard(
    Guid Id,
    string SuggestionKey,
    string Title,
    string ActionType,
    IReadOnlyList<ProvenanceHandle> EvidenceHandles,
    decimal? ImpactLow,
    decimal? ImpactHigh,
    double Confidence,
    string HonestyText,
    IReadOnlyList<string> SourceFindingRefs,
    SuggestionStatus Status = SuggestionStatus.Open);