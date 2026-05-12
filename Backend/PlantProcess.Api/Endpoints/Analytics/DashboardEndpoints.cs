using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Contracts.Analytics;
using PlantProcess.Application.Services.Analytics;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Analytics;

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

        return app;
    }

    private static async Task<IResult> GetMetadataAsync(
    IDashboardMetadataService service,
    CancellationToken cancellationToken)
    {
        var result = await service.GetMetadataAsync(cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> QueryWidgetAsync(
        DashboardWidgetQueryDto query,
        IDashboardWidgetQueryService service,
        CancellationToken cancellationToken)
    {
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
}