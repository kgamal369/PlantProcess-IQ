using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PlantProcess.Application.Analytics.Advanced;

public sealed record ReadinessDimensionDto(string Name, string State, string Reason);

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
            report.Dimensions.Select(d => new ReadinessDimensionDto(d.Name, d.State.ToString(), d.Reason)).ToList(),
            request.OutcomeKey,
            request.Grain,
            request.WindowDays,
            ds.IndependentHeats,
            ds.Outcomes.Count);
    }
}
