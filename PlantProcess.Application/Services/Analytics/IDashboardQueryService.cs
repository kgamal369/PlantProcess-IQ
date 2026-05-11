using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;

namespace PlantProcess.Application.Services.Analytics;

public interface IDashboardQueryService
{
    Task<ApplicationResult<DashboardOverviewDto>> GetOverviewAsync(Guid? siteId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task<ApplicationResult<QualityDashboardDto>> GetQualityDashboardAsync(Guid? siteId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task<ApplicationResult<RiskDashboardDto>> GetRiskDashboardAsync(Guid? siteId, int highRiskTake, CancellationToken cancellationToken);
    Task<ApplicationResult<DataQualityDashboardDto>> GetDataQualityDashboardAsync(Guid? siteId, CancellationToken cancellationToken);
}
