using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Dashboarding.Interfaces;

public interface IDashboardQueryService
{
    Task<ApplicationResult<DashboardWorkspaceDto>> GetWorkspaceAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DashboardOverviewDto>> GetOverviewAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken);

    Task<ApplicationResult<QualityDashboardDto>> GetQualityDashboardAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken);

    Task<ApplicationResult<RiskDashboardDto>> GetRiskDashboardAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DataQualityDashboardDto>> GetDataQualityDashboardAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DashboardPagedResultDto<DashboardMaterialRowDto>>> SearchMaterialsAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken);
}


