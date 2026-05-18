using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Quality;

namespace PlantProcess.Application.Services.Quality;

public interface IQualityService
{
    Task<ApplicationResult<Guid>> AddDefectCatalogAsync(
        AddDefectCatalogCommand command,
        CancellationToken cancellationToken);

    Task<ApplicationResult<Guid>> AddQualityEventAsync(
        AddQualityEventCommand command,
        CancellationToken cancellationToken);
}



