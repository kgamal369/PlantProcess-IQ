using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;

namespace PlantProcess.Application.Services.Analytics;

public interface IDashboardMetadataService
{
    Task<ApplicationResult<DashboardMetadataDto>> GetMetadataAsync(
        CancellationToken cancellationToken);
}