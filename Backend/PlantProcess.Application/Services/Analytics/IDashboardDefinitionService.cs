using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;

namespace PlantProcess.Application.Services.Analytics;

public interface IDashboardDefinitionService
{
    Task<ApplicationResult<IReadOnlyList<DashboardDefinitionDto>>> GetDashboardsAsync(
        bool includeInactive,
        bool includeSystemTemplates,
        CancellationToken cancellationToken);

    Task<ApplicationResult<DashboardDefinitionDto>> GetDashboardByIdAsync(
        Guid dashboardDefinitionId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<Guid>> CreateDashboardAsync(
        CreateDashboardDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult> UpdateDashboardAsync(
        Guid dashboardDefinitionId,
        UpdateDashboardDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult> UpdateDashboardLayoutAsync(
        Guid dashboardDefinitionId,
        UpdateDashboardLayoutRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult> DeactivateDashboardAsync(
        Guid dashboardDefinitionId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<Guid>> CreateWidgetAsync(
        Guid dashboardDefinitionId,
        CreateDashboardWidgetDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult> UpdateWidgetAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        UpdateDashboardWidgetDefinitionRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult> UpdateWidgetLayoutAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        UpdateDashboardWidgetLayoutRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult<Guid>> CloneWidgetAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        CloneDashboardWidgetRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationResult> DeactivateWidgetAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<int>> EnsureSystemTemplatesAsync(
        CancellationToken cancellationToken);
}