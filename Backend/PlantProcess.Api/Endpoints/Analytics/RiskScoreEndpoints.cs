using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Contracts.Analytics;
using PlantProcess.Application.Services.Analytics;
using PlantProcess.Application.Services.Analytics.Interfaces;
using PlantProcess.Application.Services.Analytics.Services;
using PlantProcess.Domain.Entities.Analytics;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class RiskScoreEndpoints
{
    public static IEndpointRouteBuilder MapRiskScoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/risk-scores")
            .WithTags("Risk Scores");

        group.MapGet("", GetRiskScoresAsync);
        group.MapGet("/{id:guid}", GetRiskScoreByIdAsync);
        group.MapGet("/material/{materialUnitId:guid}", GetRiskScoresByMaterialAsync);
        group.MapPost("", CreateRiskScoreAsync);

        // Phase H: calculated risk scoring, not only manual storage.
        group.MapPost("/materials/{materialUnitId:guid}/calculate", CalculateMaterialRiskAsync);
        group.MapPost("/calculate-all", CalculateRiskScoresBatchAsync);
        group.MapGet("/models", GetModelRegistryAsync);

        return app;
    }

    private static async Task<IResult> GetRiskScoresAsync(
        Guid? materialUnitId,
        string? riskType,
        string? riskClass,
        int? take,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.RiskScores.AsNoTracking().Where(x => !x.IsDeleted);

        if (materialUnitId.HasValue) query = query.Where(x => x.MaterialUnitId == materialUnitId.Value);
        if (!string.IsNullOrWhiteSpace(riskType)) query = query.Where(x => x.RiskType == riskType);
        if (!string.IsNullOrWhiteSpace(riskClass)) query = query.Where(x => x.RiskClass == riskClass);

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
    }

    private static async Task<IResult> GetRiskScoreByIdAsync(
        Guid id,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = await dbContext.RiskScores
            .AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
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
    }

    private static async Task<IResult> GetRiskScoresByMaterialAsync(
        Guid materialUnitId,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = await dbContext.RiskScores
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
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
    }

    private static async Task<IResult> CreateRiskScoreAsync(
        CreateRiskScoreRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var materialExists = await dbContext.MaterialUnits.AnyAsync(x => x.Id == request.MaterialUnitId && !x.IsDeleted, cancellationToken);
        if (!materialExists) return Results.BadRequest(new { message = "MaterialUnit does not exist." });
        if (request.Score < 0 || request.Score > 1) return Results.BadRequest(new { message = "Risk score must be between 0 and 1." });

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
    }

    private static async Task<IResult> CalculateMaterialRiskAsync(
        Guid materialUnitId,
        CalculateRiskScoreRequest request,
        IRiskScoreService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CalculateRiskScoreCommand(
            materialUnitId,
            string.IsNullOrWhiteSpace(request.RiskType) ? RiskScoreService.DefaultRiskType : request.RiskType,
            request.ModelVersion,
            null,
            null,
            request.StoreResult ?? true,
            httpContext.User?.Identity?.Name,
            httpContext.Items["CorrelationId"]?.ToString() ?? httpContext.TraceIdentifier);

        var result = await service.CalculateAsync(command, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> CalculateRiskScoresBatchAsync(
        CalculateRiskScoresBatchRequest request,
        IRiskScoreService service,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CalculateRiskScoresBatchCommand(
            request.SiteId,
            string.IsNullOrWhiteSpace(request.RiskType) ? RiskScoreService.DefaultRiskType : request.RiskType,
            request.MaxMaterials ?? 100,
            request.StoreResult ?? true,
            httpContext.User?.Identity?.Name,
            httpContext.Items["CorrelationId"]?.ToString() ?? httpContext.TraceIdentifier);

        var result = await service.CalculateBatchAsync(command, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetModelRegistryAsync(
        bool? activeOnly,
        string? riskType,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ModelRegistries.AsNoTracking().Where(x => !x.IsDeleted);
        if (activeOnly == true) query = query.Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(riskType)) query = query.Where(x => x.RiskType == riskType);

        var result = await query
            .OrderBy(x => x.RiskType)
            .ThenByDescending(x => x.RegisteredAtUtc)
            .Select(x => new
            {
                x.Id,
                x.ModelCode,
                x.ModelName,
                x.ModelType,
                x.ModelVersion,
                x.RiskType,
                x.Description,
                x.ArtifactUri,
                x.TrainingDataSummaryJson,
                x.MetricsJson,
                x.IsActive,
                x.RegisteredAtUtc,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(result);
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
