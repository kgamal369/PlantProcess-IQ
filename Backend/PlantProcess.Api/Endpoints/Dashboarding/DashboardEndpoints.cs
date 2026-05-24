using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Interfaces;
using PlantProcess.Infrastructure.Persistence;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;

namespace PlantProcess.Api.Endpoints.Dashboarding;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analytics/dashboard")
            .WithTags("Dashboard");

        // Existing-friendly GET endpoints, now accepting full filter query.
        group.MapGet("/overview", GetOverviewAsync);
        group.MapGet("/quality", GetQualityAsync);
        group.MapGet("/risk", GetRiskAsync);
        group.MapGet("/data-quality", GetDataQualityAsync);

        // Phase 8/9 professional endpoints.
        group.MapPost("/workspace", GetWorkspaceAsync);
        group.MapGet("/materials", SearchMaterialsAsync);
        group.MapGet("/reference-data", GetReferenceDataAsync);
        group.MapGet("/metadata", GetMetadataAsync);
        group.MapPost("/widgets/query", QueryWidgetAsync);
        
        
        // Phase 9 materialized view support.
        group.MapPost("/read-models/refresh", RefreshDashboardReadModelsAsync);

                // Dashboard persistence / builder endpoints.
        group.MapGet("/definitions", GetDashboardDefinitionsAsync)
            .WithSummary("List saved dashboard definitions")
            .WithDescription("Returns saved dashboards, including active widgets. Used by the React dashboard builder.");

        group.MapGet("/definitions/{dashboardDefinitionId:guid}", GetDashboardDefinitionByIdAsync)
            .WithSummary("Get saved dashboard definition")
            .WithDescription("Returns one dashboard with its widget definitions and persisted layout JSON.");

        group.MapPost("/definitions", CreateDashboardDefinitionAsync)
            .WithSummary("Create dashboard definition")
            .WithDescription("Creates a user or system dashboard definition.");

        group.MapPut("/definitions/{dashboardDefinitionId:guid}", UpdateDashboardDefinitionAsync)
            .WithSummary("Update dashboard definition")
            .WithDescription("Updates dashboard name, description, active flag and default flag.");

        group.MapPatch("/definitions/{dashboardDefinitionId:guid}/layout", UpdateDashboardLayoutAsync)
            .WithSummary("Persist dashboard grid layout")
            .WithDescription("Persists React grid layout JSON in the backend instead of localStorage.");

        group.MapDelete("/definitions/{dashboardDefinitionId:guid}", DeactivateDashboardDefinitionAsync)
            .WithSummary("Deactivate dashboard definition")
            .WithDescription("Soft-deactivates a dashboard definition without deleting historical configuration.");

        group.MapPost("/definitions/{dashboardDefinitionId:guid}/widgets", CreateDashboardWidgetDefinitionAsync)
            .WithSummary("Create dashboard widget definition")
            .WithDescription("Saves a widget created by the widget builder wizard.");

        group.MapPut("/definitions/{dashboardDefinitionId:guid}/widgets/{widgetDefinitionId:guid}", UpdateDashboardWidgetDefinitionAsync)
            .WithSummary("Update dashboard widget definition")
            .WithDescription("Updates saved widget query/configuration metadata.");

        group.MapPatch("/definitions/{dashboardDefinitionId:guid}/widgets/{widgetDefinitionId:guid}/layout", UpdateDashboardWidgetLayoutAsync)
            .WithSummary("Persist widget layout")
            .WithDescription("Persists one saved widget layout JSON and sort order.");

        group.MapPost("/definitions/{dashboardDefinitionId:guid}/widgets/{widgetDefinitionId:guid}/clone", CloneDashboardWidgetDefinitionAsync)
            .WithSummary("Clone dashboard widget definition")
            .WithDescription("Duplicates a saved widget definition in the same dashboard.");

        group.MapDelete("/definitions/{dashboardDefinitionId:guid}/widgets/{widgetDefinitionId:guid}", DeactivateDashboardWidgetDefinitionAsync)
            .WithSummary("Deactivate dashboard widget definition")
            .WithDescription("Soft-deactivates a saved widget without deleting its configuration.");

        group.MapPost("/definitions/system-templates/ensure", EnsureSystemDashboardTemplatesAsync)
            .WithSummary("Ensure system dashboard templates")
            .WithDescription("Creates default system dashboard templates when they do not already exist.");
        
        group.MapPost("/definitions/system-templates/repair", RepairSystemDashboardTemplatesAsync)
            .WithSummary("Repair system dashboard templates")
            .WithDescription(
        "Repairs existing system-template widgets so their dimension and measure codes match " +
        "the DashboardWidgetQuerySafetyRegistry. Safe to run multiple times.");
        
        group.MapPost("/widgets/execute", ExecuteWidgetExpressionAsync)
            .WithSummary("Execute widget query expression DSL")
            .WithDescription("Parses a small safe widget expression DSL and executes it using the normal whitelisted dashboard widget query engine.");
        
        return app;
                
    }

    private static async Task<IResult> GetMetadataAsync(
    IDashboardMetadataService service,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DashboardMetadataVerification");

        logger.LogInformation(
            "H-10 verification: Dashboard metadata requested by widget builder at {RequestedAtUtc}.",
            DateTime.UtcNow);

        var result = await service.GetMetadataAsync(cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> QueryWidgetAsync(
        DashboardWidgetQueryDto query,
        IDashboardWidgetQueryService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("DashboardWidgetPreviewVerification");

        logger.LogInformation(
            "H-10 verification: Widget preview query received. ChartType={ChartType}, Dimension={DimensionCode}, Measure={MeasureCode}, Parameter={ParameterCode}",
            query.ChartType,
            query.DimensionCode,
            query.MeasureCode,
            query.ParameterCode);

        var result = await service.ExecuteAsync(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetWorkspaceAsync(
        DashboardQueryDto query,
        IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetWorkspaceAsync(query, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetOverviewAsync(
        Guid? siteId,
        Guid? areaId,
        Guid? equipmentId,
        string? materialCode,
        string? sourceSystem,
        string? defectType,
        string? riskClass,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? shiftCode,
        IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetOverviewAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, 25, null, null),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetQualityAsync(
        Guid? siteId,
        Guid? areaId,
        Guid? equipmentId,
        string? materialCode,
        string? sourceSystem,
        string? defectType,
        string? riskClass,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? shiftCode,
        IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetQualityDashboardAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, 25, null, null),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetRiskAsync(
        Guid? siteId,
        Guid? areaId,
        Guid? equipmentId,
        string? materialCode,
        string? sourceSystem,
        string? defectType,
        string? riskClass,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? shiftCode,
        int? highRiskTake,
        IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetRiskDashboardAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, highRiskTake ?? 25, null, null),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetDataQualityAsync(
        Guid? siteId,
        Guid? areaId,
        Guid? equipmentId,
        string? materialCode,
        string? sourceSystem,
        string? defectType,
        string? riskClass,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? shiftCode,
        IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetDataQualityDashboardAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, 1, 25, null, null),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> SearchMaterialsAsync(
        Guid? siteId,
        Guid? areaId,
        Guid? equipmentId,
        string? materialCode,
        string? sourceSystem,
        string? defectType,
        string? riskClass,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? shiftCode,
        int? page,
        int? pageSize,
        string? sortBy,
        string? sortDirection,
        IDashboardQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.SearchMaterialsAsync(
            BuildQuery(siteId, areaId, equipmentId, materialCode, sourceSystem, defectType, riskClass, fromUtc, toUtc, shiftCode, page ?? 1, pageSize ?? 25, sortBy, sortDirection),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetReferenceDataAsync(
        Guid? siteId,
        IMemoryCache cache,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"dashboard-reference-data:{siteId?.ToString() ?? "all"}";

        if (cache.TryGetValue(cacheKey, out DashboardReferenceDataDto? cached) && cached is not null)
            return Results.Ok(cached);

        var sites = await dbContext.Sites
            .AsNoTracking()
            .OrderBy(x => x.SiteCode)
            .Select(x => new DashboardReferenceItemDto(
                x.Id.ToString(),
                x.SiteCode,
                x.SiteName,
                x.CountryCode,
                0))
            .ToListAsync(cancellationToken);

        var areasQuery = dbContext.Areas.AsNoTracking();
        if (siteId.HasValue) areasQuery = areasQuery.Where(x => x.SiteId == siteId.Value);

        var areas = await areasQuery
            .OrderBy(x => x.AreaCode)
            .Select(x => new DashboardReferenceItemDto(
                x.Id.ToString(),
                x.AreaCode,
                x.AreaName,
                "Area",
                0))
            .ToListAsync(cancellationToken);

        var equipmentQuery =
            from equipment in dbContext.Equipment.AsNoTracking()
            join area in dbContext.Areas.AsNoTracking()
                on equipment.AreaId equals area.Id
            select new
            {
                equipment.Id,
                equipment.EquipmentCode,
                equipment.EquipmentName,
                equipment.EquipmentType,
                area.SiteId
            };

        if (siteId.HasValue)
            equipmentQuery = equipmentQuery.Where(x => x.SiteId == siteId.Value);

        var equipmentItems = await equipmentQuery
            .OrderBy(x => x.EquipmentCode)
            .Select(x => new DashboardReferenceItemDto(
                x.Id.ToString(),
                x.EquipmentCode,
                x.EquipmentName,
                x.EquipmentType,
                0))
            .ToListAsync(cancellationToken);

        var sourceSystems = await dbContext.SourceSystemDefinitions
            .AsNoTracking()
            .OrderBy(x => x.SourceSystemCode)
            .Select(x => new DashboardReferenceItemDto(
                x.SourceSystemCode,
                x.SourceSystemCode,
                x.SourceSystemName,
                x.SourceSystemType,
                0))
            .ToListAsync(cancellationToken);

        var defects = await dbContext.DefectCatalogs
            .AsNoTracking()
            .OrderBy(x => x.DefectCode)
            .Select(x => new DashboardReferenceItemDto(
                x.DefectCode,
                x.DefectCode,
                x.DefectName,
                x.DefectCategory,
                0))
            .ToListAsync(cancellationToken);

        var parameters = await dbContext.ParameterDefinitions
            .AsNoTracking()
            .OrderBy(x => x.ParameterCode)
            .Select(x => new DashboardReferenceItemDto(
                x.ParameterCode,
                x.ParameterCode,
                x.ParameterName,
                x.ParameterCategory,
                0))
            .ToListAsync(cancellationToken);

        var riskClassRows = await dbContext.RiskScores
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Where(x => x.RiskClass != null && x.RiskClass != "")
            .GroupBy(x => x.RiskClass)
            .Select(x => new
            {
                Code = x.Key!,
                Count = x.Count()
            })
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        var riskClasses = riskClassRows
            .Select(x => new DashboardReferenceItemDto(
                x.Code,
                x.Code,
                x.Code,
                "RiskClass",
                x.Count))
            .ToList();

        var shiftRows = await dbContext.ProcessStepExecutions
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Where(x => x.CrewCode != null && x.CrewCode != "")
            .GroupBy(x => x.CrewCode)
            .Select(x => new
            {
                Code = x.Key!,
                Count = x.Count()
            })
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        var shifts = shiftRows
            .Select(x => new DashboardReferenceItemDto(
                x.Code,
                x.Code,
                x.Code,
                "Crew/Shift",
                x.Count))
            .ToList();

        var result = new DashboardReferenceDataDto(
            DateTime.UtcNow,
            sites,
            areas,
            equipmentItems,
            sourceSystems,
            defects,
            parameters,
            riskClasses,
            shifts);

        cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));

        return Results.Ok(result);
    }

    private static async Task<IResult> RefreshDashboardReadModelsAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Requires running database/scripts/060_phase_8_9_dashboard_materialized_views.sql first.
        await dbContext.Database.ExecuteSqlRawAsync(
            "CALL refresh_plantprocess_dashboard_read_models();",
            cancellationToken);

        return Results.Ok(new
        {
            message = "Dashboard read models refreshed.",
            refreshedAtUtc = DateTime.UtcNow
        });
    }
    private static async Task<IResult> GetDashboardDefinitionsAsync(
    bool? includeInactive,
    bool? includeSystemTemplates,
    IDashboardDefinitionService service,
    CancellationToken cancellationToken)
    {
        var result = await service.GetDashboardsAsync(
            includeInactive ?? false,
            includeSystemTemplates ?? true,
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetDashboardDefinitionByIdAsync(
        Guid dashboardDefinitionId,
        IDashboardDefinitionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetDashboardByIdAsync(dashboardDefinitionId, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> CreateDashboardDefinitionAsync(
        CreateDashboardDefinitionRequest request,
        IDashboardDefinitionService service,
        PlantProcessDbContext dbContext,
        ILicenseService licenseService,
        CancellationToken cancellationToken)
    {
        var featureGate = licenseService.EnsureFeatureEnabled(LicenseFeature.DashboardPageBuilder);
        if (featureGate.IsFailure)
            return featureGate.ToHttpResult(() => Results.NoContent());

        var activeDashboardCount = await dbContext.DashboardDefinitions
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.IsActive && !x.IsSystemTemplate, cancellationToken);

        var countGate = licenseService.EnsureDashboardCountAllowed(activeDashboardCount);
        if (countGate.IsFailure)
            return countGate.ToHttpResult(() => Results.NoContent());

        var result = await service.CreateDashboardAsync(request, cancellationToken);
        return result.ToHttpResult(id =>
            Results.Created($"/analytics/dashboard/definitions/{id}", new { id }));
    }

    private static async Task<IResult> UpdateDashboardDefinitionAsync(
        Guid dashboardDefinitionId,
        UpdateDashboardDefinitionRequest request,
        IDashboardDefinitionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateDashboardAsync(dashboardDefinitionId, request, cancellationToken);
        return result.ToHttpResult(() => Results.Ok(new
        {
            dashboardDefinitionId,
            updatedAtUtc = DateTime.UtcNow
        }));
    }

    private static async Task<IResult> UpdateDashboardLayoutAsync(
        Guid dashboardDefinitionId,
        UpdateDashboardLayoutRequest request,
        IDashboardDefinitionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateDashboardLayoutAsync(dashboardDefinitionId, request, cancellationToken);
        return result.ToHttpResult(() => Results.Ok(new
        {
            dashboardDefinitionId,
            layoutPersisted = true,
            updatedAtUtc = DateTime.UtcNow
        }));
    }

    private static async Task<IResult> DeactivateDashboardDefinitionAsync(
        Guid dashboardDefinitionId,
        IDashboardDefinitionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeactivateDashboardAsync(dashboardDefinitionId, cancellationToken);
        return result.ToHttpResult(() => Results.Ok(new
        {
            dashboardDefinitionId,
            isActive = false,
            updatedAtUtc = DateTime.UtcNow
        }));
    }

    private static async Task<IResult> CreateDashboardWidgetDefinitionAsync(
        Guid dashboardDefinitionId,
        CreateDashboardWidgetDefinitionRequest request,
        IDashboardDefinitionService service,
        ILicenseService licenseService,
        CancellationToken cancellationToken)
    {
        var featureGate = licenseService.EnsureFeatureEnabled(LicenseFeature.DashboardWidgetBuilder);
        if (featureGate.IsFailure)
            return featureGate.ToHttpResult(() => Results.NoContent());

        var result = await service.CreateWidgetAsync(dashboardDefinitionId, request, cancellationToken);
        return result.ToHttpResult(id =>
            Results.Created(
                $"/analytics/dashboard/definitions/{dashboardDefinitionId}/widgets/{id}",
                new { id, dashboardDefinitionId }));
    }

    private static async Task<IResult> UpdateDashboardWidgetDefinitionAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        UpdateDashboardWidgetDefinitionRequest request,
        IDashboardDefinitionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateWidgetAsync(
            dashboardDefinitionId,
            widgetDefinitionId,
            request,
            cancellationToken);

        return result.ToHttpResult(() => Results.Ok(new
        {
            dashboardDefinitionId,
            widgetDefinitionId,
            updatedAtUtc = DateTime.UtcNow
        }));
    }

    private static async Task<IResult> UpdateDashboardWidgetLayoutAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        UpdateDashboardWidgetLayoutRequest request,
        IDashboardDefinitionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateWidgetLayoutAsync(
            dashboardDefinitionId,
            widgetDefinitionId,
            request,
            cancellationToken);

        return result.ToHttpResult(() => Results.Ok(new
        {
            dashboardDefinitionId,
            widgetDefinitionId,
            layoutPersisted = true,
            updatedAtUtc = DateTime.UtcNow
        }));
    }

    private static async Task<IResult> CloneDashboardWidgetDefinitionAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        CloneDashboardWidgetRequest request,
        IDashboardDefinitionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CloneWidgetAsync(
            dashboardDefinitionId,
            widgetDefinitionId,
            request,
            cancellationToken);

        return result.ToHttpResult(id =>
            Results.Created(
                $"/analytics/dashboard/definitions/{dashboardDefinitionId}/widgets/{id}",
                new { id, dashboardDefinitionId, clonedFromWidgetId = widgetDefinitionId }));
    }

    private static async Task<IResult> DeactivateDashboardWidgetDefinitionAsync(
        Guid dashboardDefinitionId,
        Guid widgetDefinitionId,
        IDashboardDefinitionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeactivateWidgetAsync(
            dashboardDefinitionId,
            widgetDefinitionId,
            cancellationToken);

        return result.ToHttpResult(() => Results.Ok(new
        {
            dashboardDefinitionId,
            widgetDefinitionId,
            isActive = false,
            updatedAtUtc = DateTime.UtcNow
        }));
    }

    private static async Task<IResult> EnsureSystemDashboardTemplatesAsync(
        IDashboardDefinitionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.EnsureSystemTemplatesAsync(cancellationToken);
        return result.ToHttpResult(created => Results.Ok(new
        {
            created,
            message = created == 0
                ? "System dashboard templates already exist."
                : "System dashboard templates created.",
            ensuredAtUtc = DateTime.UtcNow
        }));
    }


    private static DashboardQueryDto BuildQuery(
        Guid? siteId,
        Guid? areaId,
        Guid? equipmentId,
        string? materialCode,
        string? sourceSystem,
        string? defectType,
        string? riskClass,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? shiftCode,
        int page,
        int pageSize,
        string? sortBy,
        string? sortDirection)
    {
        return new DashboardQueryDto(
            siteId,
            areaId,
            equipmentId,
            materialCode,
            sourceSystem,
            defectType,
            riskClass,
            fromUtc,
            toUtc,
            shiftCode,
            page,
            pageSize,
            sortBy,
            sortDirection);
    }

    private static async Task<IResult> RepairSystemDashboardTemplatesAsync(
        IDashboardDefinitionService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RepairSystemTemplatesAsync(cancellationToken);

        return result.ToHttpResult(repaired => Results.Ok(new
        {
            repaired,
            repairedAtUtc = DateTime.UtcNow
        }));
    }

    private static async Task<IResult> ExecuteWidgetExpressionAsync(
        WidgetQueryExpressionRequest request,
        IWidgetQueryExpressionService expressionService,
        IDashboardWidgetQueryService widgetQueryService,
        CancellationToken cancellationToken)
    {
        var parseResult = expressionService.Parse(request);

        if (!parseResult.IsSuccess)
        {
            // Generic ApplicationResult<T>.ToHttpResult requires Func<T, IResult>.
            // This lambda is never executed on failure; it only satisfies the generic overload.
            return parseResult.ToHttpResult(_ => Results.NoContent());
        }

        var executeResult = await widgetQueryService.ExecuteAsync(
            parseResult.Value!,
            cancellationToken);

        return executeResult.ToHttpResult(value => Results.Ok(value));
    }

}


