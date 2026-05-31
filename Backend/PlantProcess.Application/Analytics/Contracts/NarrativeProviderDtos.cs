
namespace PlantProcess.Application.Analytics.Contracts;

public sealed record NarrativeFindingInput(
    string FindingStatus,
    string FeatureKey,
    string OutcomeKey,
    string Method,
    double? EffectSize,
    double? QValue,
    int? EffectiveN,
    string? Direction,
    string? StrengthBucket,
    string? ConfoundingNote,
    IReadOnlyDictionary<string, string>? SafeMetadata = null);

public sealed record NarrativeRequest(
    string Purpose,
    IReadOnlyList<NarrativeFindingInput> Findings,
    string? Audience = null,
    bool AllowExternalProvider = false);

public sealed record NarrativeResult(
    string Text,
    string ProviderKey,
    string ModelKey,
    bool UsedExternalApi,
    bool RawPlantDataIncluded,
    IReadOnlyList<string> SafetyNotes);
