using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities.Quality;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Quality;

public static class QualityEndpoints
{
    public static IEndpointRouteBuilder MapQualityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/quality")
            .WithTags("Quality");

        // ------------------------------------------------------------
        // Defect Catalog
        // ------------------------------------------------------------
        group.MapGet("/defects", async (
            string? category,
            string? industryTemplate,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.DefectCatalogs.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(x => x.DefectCategory == category);

            if (!string.IsNullOrWhiteSpace(industryTemplate))
                query = query.Where(x => x.IndustryTemplate == industryTemplate);

            var result = await query
                .OrderBy(x => x.DefectCode)
                .Select(x => new
                {
                    x.Id,
                    x.DefectCode,
                    x.DefectName,
                    x.DefectCategory,
                    x.IndustryTemplate,
                    x.SourceSystem,
                    x.SourceRecordId,
                    x.IsSynthetic
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(result);
        });

        group.MapGet("/defects/{id:guid}", async (
            Guid id,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var result = await dbContext.DefectCatalogs
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.DefectCode,
                    x.DefectName,
                    x.DefectCategory,
                    x.IndustryTemplate,
                    x.SourceSystem,
                    x.SourceRecordId,
                    x.IsSynthetic
                })
                .FirstOrDefaultAsync(cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/defects", async (
            CreateDefectCatalogRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var exists = await dbContext.DefectCatalogs
                .AnyAsync(x => x.DefectCode == request.DefectCode, cancellationToken);

            if (exists)
                return Results.Conflict(new { message = "Defect code already exists." });

            var defect = new DefectCatalog(
                defectCode: request.DefectCode,
                defectName: request.DefectName,
                defectCategory: request.DefectCategory,
                industryTemplate: request.IndustryTemplate,
                isSynthetic: request.IsSynthetic,
                sourceSystem: request.SourceSystem,
                sourceRecordId: request.SourceRecordId);

            dbContext.DefectCatalogs.Add(defect);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/quality/defects/{defect.Id}", new
            {
                defect.Id,
                defect.DefectCode,
                defect.DefectName
            });
        });

        // ------------------------------------------------------------
        // Quality Events
        // ------------------------------------------------------------
        group.MapGet("/events", async (
            Guid? materialUnitId,
            Guid? defectCatalogId,
            string? eventType,
            string? decision,
            int? take,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.QualityEvents.AsNoTracking();

            if (materialUnitId.HasValue)
                query = query.Where(x => x.MaterialUnitId == materialUnitId.Value);

            if (defectCatalogId.HasValue)
                query = query.Where(x => x.DefectCatalogId == defectCatalogId.Value);

            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(x => x.EventType == eventType);

            if (!string.IsNullOrWhiteSpace(decision))
                query = query.Where(x => x.Decision == decision);

            var result = await query
                .OrderByDescending(x => x.EventAtUtc)
                .Take(take ?? 200)
                .Select(x => new
                {
                    x.Id,
                    x.MaterialUnitId,
                    x.DefectCatalogId,
                    x.EventType,
                    x.EventAtUtc,
                    x.EventAtLocal,
                    x.PlantTimeZoneId,
                    x.PlantUtcOffsetMinutes,
                    x.Severity,
                    x.Decision,
                    x.Description,
                    x.SourceSystem,
                    x.SourceRecordId,
                    x.IsSynthetic
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(result);
        });

        group.MapGet("/events/{id:guid}", async (
            Guid id,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var result = await dbContext.QualityEvents
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.MaterialUnitId,
                    x.DefectCatalogId,
                    x.EventType,
                    x.EventAtUtc,
                    x.EventAtLocal,
                    x.PlantTimeZoneId,
                    x.PlantUtcOffsetMinutes,
                    x.Severity,
                    x.Decision,
                    x.Description,
                    x.SourceSystem,
                    x.SourceRecordId,
                    x.IsSynthetic
                })
                .FirstOrDefaultAsync(cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPost("/events", async (
            CreateQualityEventRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var materialExists = await dbContext.MaterialUnits
                .AnyAsync(x => x.Id == request.MaterialUnitId, cancellationToken);

            if (!materialExists)
                return Results.BadRequest(new { message = "MaterialUnit does not exist." });

            if (request.DefectCatalogId.HasValue)
            {
                var defectExists = await dbContext.DefectCatalogs
                    .AnyAsync(x => x.Id == request.DefectCatalogId.Value, cancellationToken);

                if (!defectExists)
                    return Results.BadRequest(new { message = "DefectCatalog does not exist." });
            }

            var qualityEvent = new QualityEvent(
                materialUnitId: request.MaterialUnitId,
                eventType: request.EventType,
                eventAtUtc: request.EventAtUtc,
                isSynthetic: request.IsSynthetic,
                defectCatalogId: request.DefectCatalogId,
                severity: request.Severity,
                decision: request.Decision,
                description: request.Description,
                sourceSystem: request.SourceSystem,
                sourceRecordId: request.SourceRecordId,
                plantTimeZoneId: request.PlantTimeZoneId ?? "Europe/Berlin",
                plantUtcOffsetMinutes: request.PlantUtcOffsetMinutes ?? 60);

            dbContext.QualityEvents.Add(qualityEvent);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/quality/events/{qualityEvent.Id}", new
            {
                qualityEvent.Id,
                qualityEvent.MaterialUnitId,
                qualityEvent.EventType,
                qualityEvent.Decision
            });
        });

        return app;
    }

    public sealed record CreateDefectCatalogRequest(
        string DefectCode,
        string DefectName,
        string? DefectCategory,
        string? IndustryTemplate,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);

    public sealed record CreateQualityEventRequest(
        Guid MaterialUnitId,
        Guid? DefectCatalogId,
        string EventType,
        DateTime EventAtUtc,
        string? Severity,
        string? Decision,
        string? Description,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);
}