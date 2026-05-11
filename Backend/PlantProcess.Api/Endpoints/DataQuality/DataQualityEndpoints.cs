using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities.Quality;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.DataQuality;

public static class DataQualityEndpoints
{
    public static IEndpointRouteBuilder MapDataQualityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/data-quality")
            .WithTags("Data Quality");

        group.MapGet("/issues", async (
            Guid? materialUnitId,
            string? severity,
            string? issueType,
            string? affectedEntityName,
            int? take,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.DataQualityIssues.AsNoTracking();

            if (materialUnitId.HasValue)
                query = query.Where(x => x.MaterialUnitId == materialUnitId.Value);

            if (!string.IsNullOrWhiteSpace(severity))
                query = query.Where(x => x.Severity == severity);

            if (!string.IsNullOrWhiteSpace(issueType))
                query = query.Where(x => x.IssueType == issueType);

            if (!string.IsNullOrWhiteSpace(affectedEntityName))
                query = query.Where(x => x.AffectedEntityName == affectedEntityName);

            var result = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(take ?? 200)
                .Select(x => new
                {
                    x.Id,
                    x.MaterialUnitId,
                    x.IssueType,
                    x.Severity,
                    x.Description,
                    x.AffectedEntityName,
                    x.AffectedEntityId,
                    x.SourceSystem,
                    x.SourceRecordId,
                    x.CreatedAtUtc,
                    x.IsSynthetic
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(result);
        });

        group.MapGet("/issues/{id:guid}", async (
            Guid id,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var result = await dbContext.DataQualityIssues
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.MaterialUnitId,
                    x.IssueType,
                    x.Severity,
                    x.Description,
                    x.AffectedEntityName,
                    x.AffectedEntityId,
                    x.SourceSystem,
                    x.SourceRecordId,
                    x.CreatedAtUtc,
                    x.IsSynthetic
                })
                .FirstOrDefaultAsync(cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/issues/material/{materialUnitId:guid}", async (
            Guid materialUnitId,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var result = await dbContext.DataQualityIssues
                .AsNoTracking()
                .Where(x => x.MaterialUnitId == materialUnitId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new
                {
                    x.Id,
                    x.IssueType,
                    x.Severity,
                    x.Description,
                    x.AffectedEntityName,
                    x.AffectedEntityId,
                    x.SourceSystem,
                    x.CreatedAtUtc
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(result);
        });

        group.MapPost("/issues", async (
            CreateDataQualityIssueRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (request.MaterialUnitId.HasValue)
            {
                var materialExists = await dbContext.MaterialUnits
                    .AnyAsync(x => x.Id == request.MaterialUnitId.Value, cancellationToken);

                if (!materialExists)
                    return Results.BadRequest(new { message = "MaterialUnit does not exist." });
            }

            var issue = new DataQualityIssue(
                issueType: request.IssueType,
                description: request.Description,
                isSynthetic: request.IsSynthetic,
                materialUnitId: request.MaterialUnitId,
                severity: request.Severity ?? "Warning",
                affectedEntityName: request.AffectedEntityName,
                affectedEntityId: request.AffectedEntityId,
                sourceSystem: request.SourceSystem,
                sourceRecordId: request.SourceRecordId);

            dbContext.DataQualityIssues.Add(issue);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/data-quality/issues/{issue.Id}", new
            {
                issue.Id,
                issue.IssueType,
                issue.Severity,
                issue.MaterialUnitId
            });
        });

        return app;
    }

    public sealed record CreateDataQualityIssueRequest(
        Guid? MaterialUnitId,
        string IssueType,
        string? Severity,
        string Description,
        string? AffectedEntityName,
        Guid? AffectedEntityId,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);
}