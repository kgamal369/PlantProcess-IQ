using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints;

public static class DevSeedEndpoints
{
    public static IEndpointRouteBuilder MapDevSeedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/dev")
            .WithTags("Development Seed");

        group.MapPost("/seed-basic-genealogy", SeedBasicGenealogyAsync);
        group.MapGet("/genealogy/basic", GetBasicGenealogyAsync);

        return app;
    }

    private static async Task<IResult> SeedBasicGenealogyAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        const string sourceSystem = "dev-seed-v1";

        var alreadyExists = await dbContext.MaterialUnits
            .AnyAsync(x => x.MaterialCode == "H1001", cancellationToken);

        if (alreadyExists)
        {
            return Results.Ok(new
            {
                message = "Basic genealogy seed already exists.",
                genealogy = "H1001 → C2001 → S3001 → COIL4001"
            });
        }

        var siteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var offset = TimeSpan.FromHours(1);
        var startUtc = new DateTime(2026, 1, 1, 6, 0, 0, DateTimeKind.Utc);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var heat = new MaterialUnit(
            materialCode: "H1001",
            materialUnitType: "Heat",
            siteId: siteId,
            productFamily: "FlatSteel",
            gradeOrRecipe: "G2",
            isSynthetic: true,
            sourceSystem: sourceSystem,
            sourceRecordId: "SEED-HEAT-H1001");

        heat.SetProductionWindow(
            startUtc,
            startUtc.AddMinutes(55),
            offset,
            "Europe/Berlin");

        heat.AddAlias("MES_HEAT_H1001", sourceSystem, "MES");
        heat.AddAlias("L2_HEAT_H1001", sourceSystem, "Level2");

        var cast = new MaterialUnit(
            materialCode: "C2001",
            materialUnitType: "Cast",
            siteId: siteId,
            productFamily: "FlatSteel",
            gradeOrRecipe: "G2",
            isSynthetic: true,
            sourceSystem: sourceSystem,
            sourceRecordId: "SEED-CAST-C2001");

        cast.SetProductionWindow(
            startUtc.AddMinutes(60),
            startUtc.AddHours(3),
            offset,
            "Europe/Berlin");

        cast.AddAlias("CAST_SEQ_C2001", sourceSystem, "CasterSequence");

        var slab = new MaterialUnit(
            materialCode: "S3001",
            materialUnitType: "Slab",
            siteId: siteId,
            productFamily: "FlatSteel",
            gradeOrRecipe: "G2",
            isSynthetic: true,
            sourceSystem: sourceSystem,
            sourceRecordId: "SEED-SLAB-S3001");

        slab.SetProductionWindow(
            startUtc.AddMinutes(75),
            startUtc.AddMinutes(82),
            offset,
            "Europe/Berlin");

        slab.AddAlias("L2_SLAB_S3001", sourceSystem, "Level2");
        slab.AddAlias("CASTER_SLAB_S3001", sourceSystem, "Caster");

        var coil = new MaterialUnit(
            materialCode: "COIL4001",
            materialUnitType: "Coil",
            siteId: siteId,
            productFamily: "FlatSteel",
            gradeOrRecipe: "G2",
            isSynthetic: true,
            sourceSystem: sourceSystem,
            sourceRecordId: "SEED-COIL-COIL4001");

        coil.SetProductionWindow(
            startUtc.AddHours(30),
            startUtc.AddHours(30).AddMinutes(12),
            offset,
            "Europe/Berlin");

        coil.AddAlias("HSM_COIL_COIL4001", sourceSystem, "HSM");
        coil.AddAlias("QMS_COIL_COIL4001", sourceSystem, "QMS");

        dbContext.MaterialUnits.AddRange(heat, cast, slab, coil);
        await dbContext.SaveChangesAsync(cancellationToken);

        var heatToCast = new GenealogyEdge(
            parentMaterialUnitId: heat.Id,
            childMaterialUnitId: cast.Id,
            relationshipType: "ProducedInto",
            isSynthetic: true,
            sourceSystem: sourceSystem,
            sourceRecordId: "SEED-EDGE-H1001-C2001");

        var castToSlab = new GenealogyEdge(
            parentMaterialUnitId: cast.Id,
            childMaterialUnitId: slab.Id,
            relationshipType: "SplitInto",
            isSynthetic: true,
            sourceSystem: sourceSystem,
            sourceRecordId: "SEED-EDGE-C2001-S3001");

        var slabToCoil = new GenealogyEdge(
            parentMaterialUnitId: slab.Id,
            childMaterialUnitId: coil.Id,
            relationshipType: "RolledInto",
            isSynthetic: true,
            sourceSystem: sourceSystem,
            sourceRecordId: "SEED-EDGE-S3001-COIL4001");

        dbContext.GenealogyEdges.AddRange(heatToCast, castToSlab, slabToCoil);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(new
        {
            message = "Basic genealogy seed created successfully.",
            materialUnits = new[]
            {
                new { heat.Id, heat.MaterialCode, heat.MaterialUnitType },
                new { cast.Id, cast.MaterialCode, cast.MaterialUnitType },
                new { slab.Id, slab.MaterialCode, slab.MaterialUnitType },
                new { coil.Id, coil.MaterialCode, coil.MaterialUnitType }
            },
            genealogy = "H1001 → C2001 → S3001 → COIL4001"
        });
    }

    private static async Task<IResult> GetBasicGenealogyAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var materials = await dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x =>
                x.MaterialCode == "H1001" ||
                x.MaterialCode == "C2001" ||
                x.MaterialCode == "S3001" ||
                x.MaterialCode == "COIL4001")
            .OrderBy(x => x.ProductionStartUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialCode,
                x.MaterialUnitType,
                x.ProductFamily,
                x.GradeOrRecipe,
                x.ProductionStartUtc,
                x.ProductionEndUtc,
                x.ProductionStartLocal,
                x.ProductionEndLocal,
                x.PlantTimeZoneId,
                x.PlantUtcOffsetMinutes
            })
            .ToListAsync(cancellationToken);

        var materialIds = materials.Select(x => x.Id).ToList();

        var edges = await dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x =>
                materialIds.Contains(x.ParentMaterialUnitId) ||
                materialIds.Contains(x.ChildMaterialUnitId))
            .Select(x => new
            {
                x.Id,
                x.ParentMaterialUnitId,
                x.ChildMaterialUnitId,
                x.RelationshipType,
                x.SourceSystem
            })
            .ToListAsync(cancellationToken);

        var aliases = await dbContext.MaterialAliases
            .AsNoTracking()
            .Where(x => materialIds.Contains(x.MaterialUnitId))
            .Select(x => new
            {
                x.MaterialUnitId,
                x.AliasCode,
                x.AliasType,
                x.SourceSystem
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            path = "H1001 → C2001 → S3001 → COIL4001",
            materials,
            edges,
            aliases
        });
    }
}