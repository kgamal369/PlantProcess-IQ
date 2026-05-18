using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Common.Paging;
using PlantProcess.Application.Services.PlantLayout;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.PlantLayout;

public static class PlantLayoutEndpoints
{
    public static IEndpointRouteBuilder MapPlantLayoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/plant-layout")
            .WithTags("Plant Layout");

        // Application-layer query endpoints with proper page/pageSize pagination.
        group.MapGet("/sites", GetSitesAsync);
        group.MapGet("/areas", GetAreasAsync);
        group.MapGet("/equipment", GetEquipmentAsync);
        group.MapGet("/areas/{id:guid}/children", GetAreaChildrenAsync);
        group.MapGet("/equipment/{id:guid}/children", GetEquipmentChildrenAsync);
        group.MapGet("/equipment/{id:guid}/materials", GetMaterialsByEquipmentAsync);

        // Write endpoints remain thin API handlers for now; next phase can move them to Application commands.
        group.MapGet("/sites/{id:guid}", GetSiteByIdAsync);
        group.MapPost("/sites", CreateSiteAsync);
        group.MapPost("/areas", CreateAreaAsync);
        group.MapPost("/equipment", CreateEquipmentAsync);

        return app;
    }

    private static async Task<IResult> GetSitesAsync(
        int? page,
        int? pageSize,
        IPlantLayoutQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetSitesAsync(new PageRequest(page ?? 1, pageSize ?? 50), cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetAreasAsync(
        Guid? siteId,
        int? page,
        int? pageSize,
        IPlantLayoutQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAreasAsync(siteId, new PageRequest(page ?? 1, pageSize ?? 50), cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetEquipmentAsync(
        Guid? siteId,
        Guid? areaId,
        int? page,
        int? pageSize,
        IPlantLayoutQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetEquipmentAsync(siteId, areaId, new PageRequest(page ?? 1, pageSize ?? 50), cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetAreaChildrenAsync(
        Guid id,
        IPlantLayoutQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAreaChildrenAsync(id, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetEquipmentChildrenAsync(
        Guid id,
        IPlantLayoutQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetEquipmentChildrenAsync(id, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetMaterialsByEquipmentAsync(
        Guid id,
        int? page,
        int? pageSize,
        IPlantLayoutQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetMaterialsByEquipmentAsync(id, new PageRequest(page ?? 1, pageSize ?? 50), cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetSiteByIdAsync(
        Guid id,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var site = await dbContext.Sites
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.SiteCode,
                x.SiteName,
                x.CompanyName,
                x.CountryCode,
                x.TimeZoneId,
                x.IsSynthetic
            })
            .FirstOrDefaultAsync(cancellationToken);

        return site is null ? Results.NotFound() : Results.Ok(site);
    }

    private static async Task<IResult> CreateSiteAsync(
        CreateSiteRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Sites.AnyAsync(x => x.SiteCode == request.SiteCode, cancellationToken);
        if (exists)
            return Results.Conflict(new { message = "Site code already exists." });

        var site = new Site(
            siteCode: request.SiteCode,
            siteName: request.SiteName,
            isSynthetic: request.IsSynthetic,
            companyName: request.CompanyName,
            countryCode: request.CountryCode,
            timeZoneId: request.TimeZoneId,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.Sites.Add(site);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/plant-layout/sites/{site.Id}", new { site.Id, site.SiteCode, site.SiteName });
    }

    private static async Task<IResult> CreateAreaAsync(
        CreateAreaRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var siteExists = await dbContext.Sites.AnyAsync(x => x.Id == request.SiteId, cancellationToken);
        if (!siteExists)
            return Results.BadRequest(new { message = "Site does not exist." });

        if (request.ParentAreaId.HasValue)
        {
            var parentExists = await dbContext.Areas.AnyAsync(x => x.Id == request.ParentAreaId.Value, cancellationToken);
            if (!parentExists)
                return Results.BadRequest(new { message = "Parent area does not exist." });
        }

        var area = new Area(
            siteId: request.SiteId,
            areaCode: request.AreaCode,
            areaName: request.AreaName,
            isSynthetic: request.IsSynthetic,
            parentAreaId: request.ParentAreaId,
            areaType: request.AreaType,
            sortOrder: request.SortOrder,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.Areas.Add(area);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/plant-layout/areas/{area.Id}", new { area.Id, area.AreaCode, area.AreaName });
    }

    private static async Task<IResult> CreateEquipmentAsync(
        CreateEquipmentRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var siteExists = await dbContext.Sites.AnyAsync(x => x.Id == request.SiteId, cancellationToken);
        if (!siteExists)
            return Results.BadRequest(new { message = "Site does not exist." });

        if (request.AreaId.HasValue)
        {
            var areaExists = await dbContext.Areas.AnyAsync(x => x.Id == request.AreaId.Value, cancellationToken);
            if (!areaExists)
                return Results.BadRequest(new { message = "Area does not exist." });
        }

        if (request.ParentEquipmentId.HasValue)
        {
            var parentExists = await dbContext.Equipment.AnyAsync(x => x.Id == request.ParentEquipmentId.Value, cancellationToken);
            if (!parentExists)
                return Results.BadRequest(new { message = "Parent equipment does not exist." });
        }

        var equipment = new Equipment(
            siteId: request.SiteId,
            equipmentCode: request.EquipmentCode,
            equipmentName: request.EquipmentName,
            equipmentType: request.EquipmentType,
            isSynthetic: request.IsSynthetic,
            areaId: request.AreaId,
            parentEquipmentId: request.ParentEquipmentId,
            manufacturer: request.Manufacturer,
            sortOrder: request.SortOrder,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        dbContext.Equipment.Add(equipment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/plant-layout/equipment/{equipment.Id}", new { equipment.Id, equipment.EquipmentCode, equipment.EquipmentName });
    }

    public sealed record CreateSiteRequest(
        string SiteCode,
        string SiteName,
        bool IsSynthetic,
        string? CompanyName,
        string? CountryCode,
        string TimeZoneId,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record CreateAreaRequest(
        Guid SiteId,
        Guid? ParentAreaId,
        string AreaCode,
        string AreaName,
        string? AreaType,
        int? SortOrder,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record CreateEquipmentRequest(
        Guid SiteId,
        Guid? AreaId,
        Guid? ParentEquipmentId,
        string EquipmentCode,
        string EquipmentName,
        string EquipmentType,
        string? Manufacturer,
        int? SortOrder,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);
}

