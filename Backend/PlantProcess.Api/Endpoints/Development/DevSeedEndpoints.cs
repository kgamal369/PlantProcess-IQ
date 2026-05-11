using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Development;

public static class DevSeedEndpoints
{
    public static IEndpointRouteBuilder MapDevSeedEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/dev")
            .WithTags("Development / Database Validation");

        group.MapGet("/database-summary", GetDatabaseSummaryAsync);
        group.MapGet("/material-sample", GetMaterialSampleAsync);

        return app;
    }

    private static async Task<IResult> GetDatabaseSummaryAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var summary = new
        {
            sites = await dbContext.Sites.CountAsync(cancellationToken),
            areas = await dbContext.Areas.CountAsync(cancellationToken),
            equipment = await dbContext.Equipment.CountAsync(cancellationToken),

            sourceSystems = await dbContext.SourceSystemDefinitions.CountAsync(cancellationToken),
            importBatches = await dbContext.ImportBatches.CountAsync(cancellationToken),
            mappingDefinitions = await dbContext.MappingDefinitions.CountAsync(cancellationToken),

            industryTemplates = await dbContext.IndustryTemplates.CountAsync(cancellationToken),
            materialUnitTypeDefinitions = await dbContext.MaterialUnitTypeDefinitions.CountAsync(cancellationToken),
            operationDefinitions = await dbContext.OperationDefinitions.CountAsync(cancellationToken),
            routes = await dbContext.Routes.CountAsync(cancellationToken),
            routeSteps = await dbContext.RouteSteps.CountAsync(cancellationToken),

            materialUnits = await dbContext.MaterialUnits.CountAsync(cancellationToken),
            materialAliases = await dbContext.MaterialAliases.CountAsync(cancellationToken),
            genealogyEdges = await dbContext.GenealogyEdges.CountAsync(cancellationToken),

            processStepExecutions = await dbContext.ProcessStepExecutions.CountAsync(cancellationToken),
            parameterDefinitions = await dbContext.ParameterDefinitions.CountAsync(cancellationToken),
            parameterObservations = await dbContext.ParameterObservations.CountAsync(cancellationToken),
            processEvents = await dbContext.ProcessEvents.CountAsync(cancellationToken),
            downtimeEvents = await dbContext.DowntimeEvents.CountAsync(cancellationToken),

            defectCatalogs = await dbContext.DefectCatalogs.CountAsync(cancellationToken),
            qualityEvents = await dbContext.QualityEvents.CountAsync(cancellationToken),
            dataQualityIssues = await dbContext.DataQualityIssues.CountAsync(cancellationToken),

            riskScores = await dbContext.RiskScores.CountAsync(cancellationToken),
            correlationResults = await dbContext.CorrelationResults.CountAsync(cancellationToken),
            modelRegistries = await dbContext.ModelRegistries.CountAsync(cancellationToken),
            stagingRecords = await dbContext.StagingRecords.CountAsync(cancellationToken)
        };

        return Results.Ok(summary);
    }

    private static async Task<IResult> GetMaterialSampleAsync(
        int? take,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var result = await dbContext.MaterialUnits
            .AsNoTracking()
            .OrderBy(x => x.MaterialCode)
            .Take(take ?? 20)
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
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(result);
    }
}
