using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Dashboarding.Interfaces;

public interface IDashboardWidgetQueryService
{
    Task<ApplicationResult<DashboardWidgetQueryResultDto>> ExecuteAsync(
        DashboardWidgetQueryDto query,
        CancellationToken cancellationToken);
}



