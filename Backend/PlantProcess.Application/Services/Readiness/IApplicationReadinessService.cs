using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Readiness;

namespace PlantProcess.Application.Services.Readiness;

public interface IApplicationReadinessService
{
    Task<ApplicationResult<ApplicationReadinessDto>> GetReadinessAsync(
        CancellationToken cancellationToken);
}


