using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;

namespace PlantProcess.Application.Services.Analytics;

public interface IDashboardWidgetQueryService
{
    Task<ApplicationResult<DashboardWidgetQueryResultDto>> ExecuteAsync(
        DashboardWidgetQueryDto query,
        CancellationToken cancellationToken);
}