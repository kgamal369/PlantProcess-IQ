using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.PlantLayout;

public static class PlantLayoutEndpoints
{
    public static IEndpointRouteBuilder MapPlantLayoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/plant-layout")
            .WithTags("Plant Layout");

        group.MapGet("/sites", async (
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var sites = await dbContext.Sites
                .AsNoTracking()
                .OrderBy(x => x.SiteCode)
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
                .ToListAsync(cancellationToken);

            return Results.Ok(sites);
        });

        group.MapGet("/sites/{id:guid}", async (
            Guid id,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
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
        });

        group.MapPost("/sites", async (
            CreateSiteRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var exists = await dbContext.Sites
                .AnyAsync(x => x.SiteCode == request.SiteCode, cancellationToken);

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

            return Results.Created($"/plant-layout/sites/{site.Id}", new
            {
                site.Id,
                site.SiteCode,
                site.SiteName
            });
        });

        group.MapGet("/areas", async (
            Guid? siteId,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.Areas.AsNoTracking();

            if (siteId.HasValue)
                query = query.Where(x => x.SiteId == siteId.Value);

            var areas = await query
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.AreaCode)
                .Select(x => new
                {
                    x.Id,
                    x.SiteId,
                    x.ParentAreaId,
                    x.AreaCode,
                    x.AreaName,
                    x.AreaType,
                    x.SortOrder,
                    x.IsSynthetic
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(areas);
        });

        group.MapPost("/areas", async (
            CreateAreaRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var siteExists = await dbContext.Sites
                .AnyAsync(x => x.Id == request.SiteId, cancellationToken);

            if (!siteExists)
                return Results.BadRequest(new { message = "Site does not exist." });

            if (request.ParentAreaId.HasValue)
            {
                var parentExists = await dbContext.Areas
                    .AnyAsync(x => x.Id == request.ParentAreaId.Value, cancellationToken);

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

            return Results.Created($"/plant-layout/areas/{area.Id}", new
            {
                area.Id,
                area.AreaCode,
                area.AreaName
            });
        });

        group.MapGet("/equipment", async (
            Guid? siteId,
            Guid? areaId,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.Equipment.AsNoTracking();

            if (siteId.HasValue)
                query = query.Where(x => x.SiteId == siteId.Value);

            if (areaId.HasValue)
                query = query.Where(x => x.AreaId == areaId.Value);

            var equipment = await query
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.EquipmentCode)
                .Select(x => new
                {
                    x.Id,
                    x.SiteId,
                    x.AreaId,
                    x.ParentEquipmentId,
                    x.EquipmentCode,
                    x.EquipmentName,
                    x.EquipmentType,
                    x.Manufacturer,
                    x.IsActive,
                    x.SortOrder,
                    x.IsSynthetic
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(equipment);
        });

        group.MapPost("/equipment", async (
            CreateEquipmentRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var siteExists = await dbContext.Sites
                .AnyAsync(x => x.Id == request.SiteId, cancellationToken);

            if (!siteExists)
                return Results.BadRequest(new { message = "Site does not exist." });

            if (request.AreaId.HasValue)
            {
                var areaExists = await dbContext.Areas
                    .AnyAsync(x => x.Id == request.AreaId.Value, cancellationToken);

                if (!areaExists)
                    return Results.BadRequest(new { message = "Area does not exist." });
            }

            if (request.ParentEquipmentId.HasValue)
            {
                var parentExists = await dbContext.Equipment
                    .AnyAsync(x => x.Id == request.ParentEquipmentId.Value, cancellationToken);

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

            return Results.Created($"/plant-layout/equipment/{equipment.Id}", new
            {
                equipment.Id,
                equipment.EquipmentCode,
                equipment.EquipmentName
            });
        });

        return app;
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