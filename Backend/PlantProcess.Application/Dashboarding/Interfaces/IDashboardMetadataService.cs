using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Dashboarding.Interfaces;

public interface IDashboardMetadataService
{
    Task<ApplicationResult<DashboardMetadataDto>> GetMetadataAsync(
        CancellationToken cancellationToken);
}


