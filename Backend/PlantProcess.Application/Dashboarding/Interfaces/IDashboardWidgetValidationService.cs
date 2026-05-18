using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Dashboarding.Interfaces;

public interface IDashboardWidgetValidationService
{
    ApplicationResult<DashboardWidgetValidationResultDto> Validate(
        DashboardWidgetQueryDto query);
}



