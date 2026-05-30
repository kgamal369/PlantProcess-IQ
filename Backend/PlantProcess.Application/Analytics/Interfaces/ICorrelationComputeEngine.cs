using PlantProcess.Application.Analytics.Contracts;

namespace PlantProcess.Application.Analytics.Interfaces;

public interface ICorrelationComputeEngine
{
    string EngineKey { get; }

    Task<CorrelationComputeResult> ComputeAsync(
        CorrelationComputeRequest request,
        CancellationToken cancellationToken);
}