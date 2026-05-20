using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Domain.Entities.Quality;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.DataQuality;

public static class DataQualityScanEndpoints
{
    public static IEndpointRouteBuilder MapDataQualityScanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/data-quality")
            .WithTags("Data Quality");

        group.MapGet("/scan-preview", BuildPreviewAsync);
        group.MapPost("/scan/run", RunPersistedScanAsync);

        // Backward-compatible alias for the earlier Phase A/B endpoint.
        group.MapPost("/scan", RunPersistedScanAsync);

        return app;
    }

    private static async Task<IResult> RunPersistedScanAsync(
        int? maxCandidatesPerRule,
        IDataQualityService service,
        CancellationToken cancellationToken)
    {
        var result = await service.RunFullScanAsync(maxCandidatesPerRule ?? 500, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> BuildPreviewAsync(
        int? take,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidates = await BuildDataQualityCandidatesAsync(
                dbContext,
                take ?? 500,
                cancellationToken);

            return Results.Ok(new
            {
                persistIssues = false,
                generatedCandidates = candidates.Count,
                candidates,
                status = "Ok",
                generatedAtUtc = DateTime.UtcNow
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Results.Problem(
                title: "Data quality scan preview failed.",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<List<DataQualityCandidate>> BuildDataQualityCandidatesAsync(
        PlantProcessDbContext dbContext,
        int take,
        CancellationToken cancellationToken)
    {
        var result = new List<DataQualityCandidate>();
        var maxRows = Math.Clamp(take, 1, 5000);

        var materialsWithoutAliases = await dbContext.MaterialUnits
            .AsNoTracking()
            .Where(material => !dbContext.MaterialAliases.Any(alias => alias.MaterialUnitId == material.Id))
            .OrderBy(x => x.MaterialCode)
            .Take(maxRows)
            .Select(x => new { x.Id, x.MaterialCode, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(materialsWithoutAliases.Select(x => new DataQualityCandidate(
            "MissingMaterialAlias",
            "Warning",
            $"Material '{x.MaterialCode}' has no source-system alias. This reduces cross-system traceability.",
            "MaterialUnit",
            x.Id,
            x.Id,
            x.IsSynthetic,
            x.SourceSystem,
            x.SourceRecordId)));

        var materialsWithoutSteps = await dbContext.MaterialUnits
            .AsNoTracking()
            .Where(material => !dbContext.ProcessStepExecutions.Any(step => step.MaterialUnitId == material.Id))
            .OrderBy(x => x.MaterialCode)
            .Take(maxRows)
            .Select(x => new { x.Id, x.MaterialCode, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(materialsWithoutSteps.Select(x => new DataQualityCandidate(
            "MissingProcessHistory",
            "Warning",
            $"Material '{x.MaterialCode}' has no process step execution. Investigation timeline will be incomplete.",
            "MaterialUnit",
            x.Id,
            x.Id,
            x.IsSynthetic,
            x.SourceSystem,
            x.SourceRecordId)));

        var observationsWithoutValue = await dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => x.NumericValue == null && x.TextValue == null && x.BooleanValue == null)
            .OrderBy(x => x.ObservedAtUtc)
            .Take(maxRows)
            .Select(x => new { x.Id, x.MaterialUnitId, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(observationsWithoutValue.Select(x => new DataQualityCandidate(
            "MissingParameterValue",
            "Error",
            "Parameter observation has no numeric, text or boolean value.",
            "ParameterObservation",
            x.Id,
            x.MaterialUnitId,
            x.IsSynthetic,
            x.SourceSystem,
            x.SourceRecordId)));

        var defectEventsWithoutCatalog = await dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => x.EventType == "Defect" && x.DefectCatalogId == null)
            .OrderBy(x => x.EventAtUtc)
            .Take(maxRows)
            .Select(x => new { x.Id, x.MaterialUnitId, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(defectEventsWithoutCatalog.Select(x => new DataQualityCandidate(
            "DefectEventWithoutCatalog",
            "Warning",
            "Quality event is marked as Defect but is not linked to a standardized defect catalog record.",
            "QualityEvent",
            x.Id,
            x.MaterialUnitId,
            x.IsSynthetic,
            x.SourceSystem,
            x.SourceRecordId)));

       var highRiskWithoutContributors = await dbContext.RiskScores
            .AsNoTracking()
            .Where(x => x.Score >= 0.70m && x.MainContributorsJson == null)
            .OrderByDescending(x => x.ScoredAtUtc)
            .Take(maxRows)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.RiskType,
                x.Score,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        result.AddRange(highRiskWithoutContributors.Select(x => new DataQualityCandidate(
            "HighRiskScoreWithoutContributors",
            "Warning",
            $"Risk score '{x.RiskType}' is high ({x.Score}) but has no contributor explanation JSON.",
            "RiskScore",
            x.Id,
            x.MaterialUnitId,
            x.IsSynthetic,
            x.SourceSystem,
            x.SourceRecordId)));
                return result;
            }

    private sealed record DataQualityCandidate(
        string IssueType,
        string Severity,
        string Description,
        string AffectedEntityName,
        Guid AffectedEntityId,
        Guid? MaterialUnitId,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);
}

