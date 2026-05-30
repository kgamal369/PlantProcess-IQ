using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Dashboarding.Contracts;

namespace PlantProcess.Application.Dashboarding.Interfaces;

public interface IWidgetQueryExpressionService
{
    ApplicationResult<DashboardWidgetQueryDto> Parse(WidgetQueryExpressionRequest request);

    ApplicationResult<CompiledWidgetQueryExpression> Compile(WidgetQueryExpressionRequest request);
}
