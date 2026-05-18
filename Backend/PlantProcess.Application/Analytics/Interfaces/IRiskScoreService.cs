using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Analytics.Interfaces;

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





