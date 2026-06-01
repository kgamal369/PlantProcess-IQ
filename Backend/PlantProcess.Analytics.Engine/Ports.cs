using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Analytics.Core.Methods;
using PlantProcess.Analytics.Core.Readiness;
using PlantProcess.Application.Analytics.Contracts;

namespace PlantProcess.Analytics.Engine;

/// <summary>One canonical feature column aligned by row with the outcome vector.</summary>
public sealed record FeatureColumn(string Code, VariableType Type, IReadOnlyList<double?> Values);

/// <summary>The read-only feature matrix the managed engine analyzes for one compute request.</summary>
public sealed record FeatureMatrix(
    string OutcomeKey,
    string Grain,
    IReadOnlyList<double> Outcome,
    IReadOnlyList<FeatureColumn> Parameters,
    ReadinessInput Readiness,
    int ExcludedRecords);

/// <summary>Port: supplies canonical features for a compute request (Postgres adapter = increment 1b).</summary>
public interface ICanonicalFeatureSource
{
    Task<FeatureMatrix> LoadAsync(CorrelationComputeRequest request, CancellationToken cancellationToken);
}

/// <summary>A single disciplined finding produced by the managed engine (no seed rows; computed live).</summary>
public sealed record AnalysisFinding(
    string ParameterCode,
    AnalysisMethod Method,
    double EffectSize,
    double PValue,
    double QValue,
    bool Significant,
    int SampleSize,
    double StabilityLower,
    double StabilityUpper,
    double StabilityConsistency,
    bool Stable);

/// <summary>Port: persists findings for a run (Postgres adapter = increment 1b).</summary>
public interface IAnalysisFindingSink
{
    Task WriteAsync(Guid computeRunId, CorrelationComputeRequest request, IReadOnlyList<AnalysisFinding> findings, CancellationToken cancellationToken);
}