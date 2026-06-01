using System.Collections.Generic;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Readiness;
namespace PlantProcess.Analytics.Core.Contracts;
/// <summary>v4 6.2 advanced-result contract. Backend shape behind the T-034 UI; the honesty caveat is mandatory.</summary>
public sealed record AdvancedAnalysisResult(
string FindingId,
AnalysisMethod Method,
double EffectSize,
double QValue,
int SampleSize,
IReadOnlyList<string> Filters,
ReadinessState Readiness,
double StabilityConsistency,
double StabilityLower,
double StabilityUpper,
int ExcludedRecords,
IReadOnlyList<string> DataQualityWarnings,
bool SurvivesStratification,
string HonestyCaveat)
{
public const string DefaultCaveat = "This is a diagnostic association, not a guaranteed root cause.";
/// <summary>A result missing method, sample size, or the honesty caveat must not render (mirrors the T-034 component guard).</summary>
public bool IsRenderable =>
    Method != AnalysisMethod.NotApplicable &&
    SampleSize > 0 &&
    !string.IsNullOrWhiteSpace(HonestyCaveat);
}