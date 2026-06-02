using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Analytics.Core.Methods;   // VariableType, AnalysisMethod
using PlantProcess.Analytics.Core.Readiness;  // ReadinessState

namespace PlantProcess.Application.Analytics.Advanced;

/// <summary>P3 advanced-analysis request: target outcome + grain + window, scoped to a tenant.</summary>
public sealed record AdvancedAnalysisRequest(
    string OutcomeKey,
    string Grain,
    int WindowDays,
    Guid TenantId,
    IReadOnlyList<string>? Filters = null,
    double FdrQ = 0.05,
    double VifThreshold = 5.0,
    int BootstrapIterations = 1000,
    int PermutationIterations = 200,
    string? CorrelationId = null);

public sealed record OutcomeSample(string SampleKey, double Value, string? Category, string? HeatId);
public sealed record FeatureSample(string SampleKey, double? Numeric, string? Category);
public sealed record FeatureSeries(string FeatureKey, VariableType Type, IReadOnlyList<FeatureSample> Samples);

/// <summary>Aligned feature/outcome vectors plus the readiness inputs the gate needs.</summary>
public sealed record AdvancedDataset(
    string OutcomeKey,
    VariableType OutcomeType,
    IReadOnlyList<OutcomeSample> Outcomes,
    IReadOnlyList<FeatureSeries> Features,
    IReadOnlyDictionary<string, string> StrataBySampleKey,
    int IndependentHeats,
    double FreshnessFactor,
    double RequiredFieldCompleteness);

public sealed record AdvancedFinding(
    string FeatureKey,
    AnalysisMethod Method,
    string MethodRationale,
    double EffectSize,
    double PValue,
    double QValue,
    bool Significant,
    int SampleSize,
    double StabilityConsistency,
    double StabilityLower,
    double StabilityUpper,
    bool IsStable,
    bool SurvivesStratification,
    string StratificationReason,
    int ExcludedRecords,
    string ProvenanceHandle,
    string HonestyCaveat);

public sealed record ExcludedFeature(string FeatureKey, string Reason);

public sealed record AdvancedAnalysisRunResult(
    Guid RunId,
    string OutcomeKey,
    VariableType OutcomeType,
    string Grain,
    int WindowDays,
    ReadinessState Readiness,
    IReadOnlyList<string> ReadinessReasons,
    bool CanRun,
    IReadOnlyList<AdvancedFinding> Findings,
    IReadOnlyList<ExcludedFeature> Excluded,
    string Engine,
    string Message);

public interface IFeatureVectorLoader
{
    Task<AdvancedDataset> LoadAsync(AdvancedAnalysisRequest request, CancellationToken ct);
}

public interface IAdvancedResultWriter
{
    Task<Guid> WriteAsync(AdvancedAnalysisRequest request, AdvancedAnalysisRunResult result, CancellationToken ct);
}

public interface IAdvancedCorrelationService
{
    Task<AdvancedAnalysisRunResult> ComputeAsync(AdvancedAnalysisRequest request, CancellationToken ct);
}
