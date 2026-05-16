using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;

namespace PlantProcess.Application.Services.Analytics.Interfaces;

public interface IRiskScoreService
{
    Task<ApplicationResult<Guid>> StoreAsync(
        StoreRiskScoreCommand command,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CalculateRiskScoreResult>> CalculateAsync(
        CalculateRiskScoreCommand command,
        CancellationToken cancellationToken);

    Task<ApplicationResult<CalculateRiskScoresBatchResult>> CalculateBatchAsync(
        CalculateRiskScoresBatchCommand command,
        CancellationToken cancellationToken);
}
