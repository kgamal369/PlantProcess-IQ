using PlantProcess.Application.Common.Paging;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Quality;

namespace PlantProcess.Application.Services.Quality;

public interface IQualityQueryService
{
    Task<ApplicationResult<PagedResult<QualityEventReadDto>>> GetQualityEventsAsync(QualityEventQuery query, CancellationToken cancellationToken);
    Task<ApplicationResult<QualityEventReadDto>> GetQualityEventByIdAsync(Guid id, CancellationToken cancellationToken);
}




