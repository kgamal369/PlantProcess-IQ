using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;

namespace PlantProcess.Application.Services.Dashboard.Interfaces;

public interface IDashboardWidgetValidationService
{
    ApplicationResult<DashboardWidgetValidationResultDto> Validate(
        DashboardWidgetQueryDto query);
}