using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlantProcess.Application.Analytics.Advanced;

/// <summary>
/// T-045-R1-A. This record WAS the discard point: the gate produced the
/// measurement and its bounds, and this three-field projection dropped them one
/// hop before anything could show them.
/// </summary>
public sealed record ReadinessDimensionDto(
    string Name,
    string State,
    string Reason,
    double MeasuredValue,
    double ReadyThreshold,
    double PartialThreshold,
    bool HigherIsBetter);

public sealed record AnalysisReadinessDto(
    string Overall,
    bool CanRun,
    IReadOnlyList<ReadinessDimensionDto> Dimensions,
    string OutcomeKey,
    string Grain,
    int WindowDays,
    int IndependentHeats,
    int OutcomeEvents);

public interface IAnalysisReadinessService
{
    Task<AnalysisReadinessDto> EvaluateAsync(AdvancedAnalysisRequest request, CancellationToken ct);
}

/// <summary>
/// P4-02 live per-analysis readiness. Loads the same vectors the engine uses and runs
/// the tested ReadinessGate (via AdvancedReadiness), exposing per-dimension reasons.
/// </summary>
public sealed class AnalysisReadinessService : IAnalysisReadinessService
{
    private readonly IFeatureVectorLoader _loader;
    public AnalysisReadinessService(IFeatureVectorLoader loader) => _loader = loader;

    public async Task<AnalysisReadinessDto> EvaluateAsync(AdvancedAnalysisRequest request, CancellationToken ct)
    {
        var ds = await _loader.LoadAsync(request, ct);
        var report = AdvancedReadiness.Evaluate(ds);
        return new AnalysisReadinessDto(
            report.Overall.ToString(),
            report.CanRun,
            report.Dimensions.Select(d => new ReadinessDimensionDto(
                d.Name, d.State.ToString(), d.Reason,
                d.MeasuredValue, d.ReadyThreshold, d.PartialThreshold,
                d.HigherIsBetter)).ToList(),
            request.OutcomeKey,
            request.Grain,
            request.WindowDays,
            ds.IndependentHeats,
            ds.Outcomes.Count);
    }
}
