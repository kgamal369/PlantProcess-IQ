// PPIQ-GENERATED (T010)
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Analytics.Advanced;

namespace PlantProcess.Application.Analytics.Engines;

/// <summary>Canonical engine = the deterministic Analytics.Core path (V7 §7.3-7.4): method
/// auto-selection, BH-FDR, bootstrap stability, stratification, provenance + honesty caveat.</summary>
public sealed class CanonicalCorrelationEngine : ICorrelationEngine
{
    public const string CanonicalKey = "canonical";
    private readonly IAdvancedCorrelationService _inner;
    public CanonicalCorrelationEngine(IAdvancedCorrelationService inner) { _inner = inner; }
    public string Key => CanonicalKey;
    public Task<AdvancedAnalysisRunResult> ComputeAsync(AdvancedAnalysisRequest request, CancellationToken cancellationToken = default)
        => _inner.ComputeAsync(request, cancellationToken);
}