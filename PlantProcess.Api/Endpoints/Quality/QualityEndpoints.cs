using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Contracts.Quality;
using PlantProcess.Application.Services.Quality;
using PlantProcess.Domain.Entities.Quality;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Quality;

public static class QualityEndpoints
{
    public static IEndpointRouteBuilder MapQualityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/quality")
            .WithTags("Quality");

        group.MapGet("/defects", GetDefectsAsync);
        group.MapGet("/defects/{id:guid}", GetDefectByIdAsync);
        group.MapPost("/defects", CreateDefectAsync);

        // Enriched quality-event read model through Application layer.
        group.MapGet("/events", GetQualityEventsAsync);
        group.MapGet("/events/{id:guid}", GetQualityEventByIdAsync);

        group.MapPost("/events", CreateQualityEventAsync);
        group.MapDelete("/events/{id:guid}", SoftDeleteQualityEventAsync);

        return app;
    }

    private static async Task<IResult> GetDefectsAsync(
        string? category,
        string? industryTemplate,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
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
    }

    private static async Task<IResult> GetDefectByIdAsync(
        Guid id,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
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
    }

    private static async Task<IResult> CreateDefectAsync(
        CreateDefectCatalogRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
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
    }

    private static async Task<IResult> GetQualityEventsAsync(
        Guid? materialUnitId,
        Guid? defectCatalogId,
        string? eventType,
        string? decision,
        string? severity,
        DateTime? fromUtc,
        DateTime? toUtc,
        int? page,
        int? pageSize,
        IQualityQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetQualityEventsAsync(
            new QualityEventQuery(
                materialUnitId,
                defectCatalogId,
                eventType,
                decision,
                severity,
                fromUtc,
                toUtc,
                page ?? 1,
                pageSize ?? 50),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetQualityEventByIdAsync(
        Guid id,
        IQualityQueryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetQualityEventByIdAsync(id, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> CreateQualityEventAsync(
        CreateQualityEventRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
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
    }

    private static async Task<IResult> SoftDeleteQualityEventAsync(
        Guid id,
        SoftDeleteQualityEventRequest? request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var qualityEvent = await dbContext.QualityEvents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (qualityEvent is null)
            return Results.NotFound(new { message = "QualityEvent not found." });

        qualityEvent.SoftDelete(request?.Reason ?? "Soft-deleted via API.");
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { qualityEvent.Id, qualityEvent.MaterialUnitId, qualityEvent.IsDeleted, qualityEvent.DeletedAtUtc });
    }

    public sealed record SoftDeleteQualityEventRequest(string? Reason);

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
