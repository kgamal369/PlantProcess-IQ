using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;

namespace PlantProcess.Application.Services.Analytics;

public interface IRiskScoreService
{
    Task<ApplicationResult<Guid>> StoreAsync(
        StoreRiskScoreCommand command,
        CancellationToken cancellationToken);
}