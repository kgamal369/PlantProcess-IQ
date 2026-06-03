// PPIQ-GENERATED (T010)
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Analytics.Advanced;

namespace PlantProcess.Application.Analytics.Engines;

/// <summary>PPIQ-T010. The single authoritative correlation interface. Strategies register as
/// ICorrelationEngine; the canonical (Analytics.Core) engine is the documented default.</summary>
public interface ICorrelationEngine
{
    string Key { get; }
    Task<AdvancedAnalysisRunResult> ComputeAsync(AdvancedAnalysisRequest request, CancellationToken cancellationToken = default);
}