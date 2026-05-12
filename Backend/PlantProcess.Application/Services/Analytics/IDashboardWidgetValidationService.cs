using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;

namespace PlantProcess.Application.Services.Analytics;

public interface IDashboardWidgetValidationService
{
    ApplicationResult<DashboardWidgetValidationResultDto> Validate(
        DashboardWidgetQueryDto query);
}