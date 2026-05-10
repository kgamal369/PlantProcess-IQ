using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Materials;

public static class MaterialEndpoints
{
    public static IEndpointRouteBuilder MapMaterialEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/materials")
            .WithTags("Materials");

        group.MapGet("", async (
            string? code,
            string? type,
            int? take,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.MaterialUnits.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(code))
                query = query.Where(x => x.MaterialCode.Contains(code));

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(x => x.MaterialUnitType == type);

            var result = await query
                .OrderBy(x => x.MaterialCode)
                .Take(take ?? 100)
                .Select(x => new
                {
                    x.Id,
                    x.MaterialCode,
                    x.MaterialUnitType,
                    x.ProductFamily,
                    x.GradeOrRecipe,
                    x.SiteId,
                    x.ProductionStartUtc,
                    x.ProductionEndUtc,
                    x.ProductionStartLocal,
                    x.ProductionEndLocal,
                    x.IsSynthetic
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (
            Guid id,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var material = await dbContext.MaterialUnits
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.MaterialCode,
                    x.MaterialUnitType,
                    x.ProductFamily,
                    x.GradeOrRecipe,
                    x.SiteId,
                    x.ProductionStartUtc,
                    x.ProductionEndUtc,
                    x.ProductionStartLocal,
                    x.ProductionEndLocal,
                    x.PlantTimeZoneId,
                    x.PlantUtcOffsetMinutes,
                    x.IsSynthetic
                })
                .FirstOrDefaultAsync(cancellationToken);

            return material is null ? Results.NotFound() : Results.Ok(material);
        });

        group.MapPost("", async (
            CreateMaterialRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var siteExists = await dbContext.Sites
                .AnyAsync(x => x.Id == request.SiteId, cancellationToken);

            if (!siteExists)
                return Results.BadRequest(new { message = "Site does not exist." });

            var exists = await dbContext.MaterialUnits
             .AnyAsync(x =>
                 x.SiteId == request.SiteId &&
                 x.MaterialCode == request.MaterialCode,
                 cancellationToken);

            if (exists)
                return Results.Conflict(new { message = "Material code already exists." });

            var material = new MaterialUnit(
                materialCode: request.MaterialCode,
                materialUnitType: request.MaterialUnitType,
                siteId: request.SiteId,
                productFamily: request.ProductFamily,
                gradeOrRecipe: request.GradeOrRecipe,
                isSynthetic: request.IsSynthetic,
                sourceSystem: request.SourceSystem,
                sourceRecordId: request.SourceRecordId);

            if (request.ProductionStartUtc.HasValue)
            {
                material.SetProductionWindow(
                    request.ProductionStartUtc.Value,
                    request.ProductionEndUtc,
                    TimeSpan.FromMinutes(request.PlantUtcOffsetMinutes ?? 60),
                    request.PlantTimeZoneId ?? "Europe/Berlin");
            }

            dbContext.MaterialUnits.Add(material);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/materials/{material.Id}", new
            {
                material.Id,
                material.MaterialCode,
                material.MaterialUnitType
            });
        });

        group.MapPost("/{id:guid}/aliases", async (
            Guid id,
            AddMaterialAliasRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var material = await dbContext.MaterialUnits
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (material is null)
                return Results.NotFound();

            material.AddAlias(request.AliasCode, request.SourceSystem, request.AliasType);

            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new
            {
                material.Id,
                request.AliasCode,
                request.AliasType,
                request.SourceSystem
            });
        });

        group.MapPost("/genealogy-edges", async (
            CreateGenealogyEdgeRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var parentExists = await dbContext.MaterialUnits
                .AnyAsync(x => x.Id == request.ParentMaterialUnitId, cancellationToken);

            var childExists = await dbContext.MaterialUnits
                .AnyAsync(x => x.Id == request.ChildMaterialUnitId, cancellationToken);

            if (!parentExists || !childExists)
                return Results.BadRequest(new { message = "Parent or child material does not exist." });

            var edge = new GenealogyEdge(
                parentMaterialUnitId: request.ParentMaterialUnitId,
                childMaterialUnitId: request.ChildMaterialUnitId,
                relationshipType: request.RelationshipType,
                isSynthetic: request.IsSynthetic,
                sourceSystem: request.SourceSystem,
                sourceRecordId: request.SourceRecordId);

            dbContext.GenealogyEdges.Add(edge);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/materials/genealogy-edges/{edge.Id}", new
            {
                edge.Id,
                edge.ParentMaterialUnitId,
                edge.ChildMaterialUnitId,
                edge.RelationshipType
            });
        });

        group.MapGet("/{id:guid}/genealogy", async (
            Guid id,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var material = await dbContext.MaterialUnits
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (material is null)
                return Results.NotFound();

            var edges = await dbContext.GenealogyEdges
                .AsNoTracking()
                .Where(x => x.ParentMaterialUnitId == id || x.ChildMaterialUnitId == id)
                .Select(x => new
                {
                    x.Id,
                    x.ParentMaterialUnitId,
                    x.ChildMaterialUnitId,
                    x.RelationshipType,
                    x.SourceSystem
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(new
            {
                material = new
                {
                    material.Id,
                    material.MaterialCode,
                    material.MaterialUnitType
                },
                edges
            });
        });

        return app;
    }

    public sealed record CreateMaterialRequest(
        string MaterialCode,
        string MaterialUnitType,
        Guid SiteId,
        string? ProductFamily,
        string? GradeOrRecipe,
        DateTime? ProductionStartUtc,
        DateTime? ProductionEndUtc,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record AddMaterialAliasRequest(
        string AliasCode,
        string AliasType,
        string SourceSystem);

    public sealed record CreateGenealogyEdgeRequest(
        Guid ParentMaterialUnitId,
        Guid ChildMaterialUnitId,
        string RelationshipType,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);
}