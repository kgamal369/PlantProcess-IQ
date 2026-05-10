using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities.Analytics;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class RiskScoreEndpoints
{
    public static IEndpointRouteBuilder MapRiskScoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/risk-scores")
            .WithTags("Risk Scores");

        group.MapGet("", async (
            Guid? materialUnitId,
            string? riskType,
            string? riskClass,
            int? take,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var query = dbContext.RiskScores.AsNoTracking();

            if (materialUnitId.HasValue)
                query = query.Where(x => x.MaterialUnitId == materialUnitId.Value);

            if (!string.IsNullOrWhiteSpace(riskType))
                query = query.Where(x => x.RiskType == riskType);

            if (!string.IsNullOrWhiteSpace(riskClass))
                query = query.Where(x => x.RiskClass == riskClass);

            var result = await query
                .OrderByDescending(x => x.ScoredAtUtc)
                .Take(take ?? 200)
                .Select(x => new
                {
                    x.Id,
                    x.MaterialUnitId,
                    x.RiskType,
                    x.Score,
                    x.RiskClass,
                    x.MainContributorsJson,
                    x.ModelVersion,
                    x.ScoredAtUtc,
                    x.ScoredAtLocal,
                    x.PlantTimeZoneId,
                    x.PlantUtcOffsetMinutes,
                    x.SourceSystem,
                    x.SourceRecordId,
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
            var result = await dbContext.RiskScores
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.MaterialUnitId,
                    x.RiskType,
                    x.Score,
                    x.RiskClass,
                    x.MainContributorsJson,
                    x.ModelVersion,
                    x.ScoredAtUtc,
                    x.ScoredAtLocal,
                    x.PlantTimeZoneId,
                    x.PlantUtcOffsetMinutes,
                    x.SourceSystem,
                    x.SourceRecordId,
                    x.IsSynthetic
                })
                .FirstOrDefaultAsync(cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapGet("/material/{materialUnitId:guid}", async (
            Guid materialUnitId,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var result = await dbContext.RiskScores
                .AsNoTracking()
                .Where(x => x.MaterialUnitId == materialUnitId)
                .OrderByDescending(x => x.ScoredAtUtc)
                .Select(x => new
                {
                    x.Id,
                    x.RiskType,
                    x.Score,
                    x.RiskClass,
                    x.MainContributorsJson,
                    x.ModelVersion,
                    x.ScoredAtUtc
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(result);
        });

        group.MapPost("", async (
            CreateRiskScoreRequest request,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var materialExists = await dbContext.MaterialUnits
                .AnyAsync(x => x.Id == request.MaterialUnitId, cancellationToken);

            if (!materialExists)
                return Results.BadRequest(new { message = "MaterialUnit does not exist." });

            var riskScore = new RiskScore(
                materialUnitId: request.MaterialUnitId,
                riskType: request.RiskType,
                score: request.Score,
                isSynthetic: request.IsSynthetic,
                riskClass: request.RiskClass,
                mainContributorsJson: request.MainContributorsJson,
                modelVersion: request.ModelVersion,
                sourceSystem: request.SourceSystem,
                sourceRecordId: request.SourceRecordId,
                plantTimeZoneId: request.PlantTimeZoneId ?? "Europe/Berlin",
                plantUtcOffsetMinutes: request.PlantUtcOffsetMinutes ?? 60);

            dbContext.RiskScores.Add(riskScore);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Created($"/risk-scores/{riskScore.Id}", new
            {
                riskScore.Id,
                riskScore.MaterialUnitId,
                riskScore.RiskType,
                riskScore.Score,
                riskScore.RiskClass
            });
        });

        return app;
    }

    public sealed record CreateRiskScoreRequest(
        Guid MaterialUnitId,
        string RiskType,
        decimal Score,
        string? RiskClass,
        string? MainContributorsJson,
        string? ModelVersion,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId,
        string? PlantTimeZoneId,
        int? PlantUtcOffsetMinutes);
}