using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Validation;

public static class ValidationEndpoints
{
    public static IEndpointRouteBuilder MapValidationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/validation")
            .WithTags("Validation / Model Sync");

        group.MapGet("/sync-report", GetSyncReportAsync);

        return app;
    }

    private static async Task<IResult> GetSyncReportAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var materialTypesWithoutDefinition = await dbContext.MaterialUnits
            .AsNoTracking()
            .Where(material => !dbContext.MaterialUnitTypeDefinitions
                .Any(type => type.MaterialUnitTypeCode == material.MaterialUnitType))
            .Select(x => new
            {
                x.Id,
                x.MaterialCode,
                x.MaterialUnitType
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var processStepsWithoutOperationDefinition = await dbContext.ProcessStepExecutions
            .AsNoTracking()
            .Where(step => !dbContext.OperationDefinitions
                .Any(operation =>
                    operation.OperationCode == step.OperationType ||
                    operation.OperationCode == step.OperationCode))
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.OperationType,
                x.OperationCode,
                x.StartedAtUtc
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var observationsWithoutAnyValue = await dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => x.NumericValue == null &&
                        x.TextValue == null &&
                        x.BooleanValue == null)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.ParameterDefinitionId,
                x.ObservedAtUtc
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var observationsOutsideStepWindow = await dbContext.ParameterObservations
            .AsNoTracking()
            .Join(
                dbContext.ProcessStepExecutions.AsNoTracking(),
                observation => observation.ProcessStepExecutionId,
                step => step.Id,
                (observation, step) => new
                {
                    Observation = observation,
                    Step = step
                })
            .Where(x =>
                x.Observation.ObservedAtUtc < x.Step.StartedAtUtc ||
                (x.Step.EndedAtUtc.HasValue &&
                 x.Observation.ObservedAtUtc > x.Step.EndedAtUtc.Value))
            .Select(x => new
            {
                x.Observation.Id,
                x.Observation.MaterialUnitId,
                x.Observation.ParameterDefinitionId,
                x.Observation.ObservedAtUtc,
                x.Step.StartedAtUtc,
                x.Step.EndedAtUtc
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var processEventsWithoutAnyReference = await dbContext.ProcessEvents
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == null &&
                        x.ProcessStepExecutionId == null &&
                        x.EquipmentId == null)
            .Select(x => new
            {
                x.Id,
                x.EventType,
                x.EventAtUtc
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var downtimeEventsWithoutAnyReference = await dbContext.DowntimeEvents
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == null &&
                        x.ProcessStepExecutionId == null &&
                        x.EquipmentId == null)
            .Select(x => new
            {
                x.Id,
                x.DowntimeType,
                x.StartedAtUtc,
                x.EndedAtUtc
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var defectEventsWithoutCatalog = await dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => x.EventType == "Defect" && x.DefectCatalogId == null)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.EventType,
                x.EventAtUtc,
                x.Decision
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var highRiskScoresWithoutContributors = await dbContext.RiskScores
            .AsNoTracking()
            .Where(x => x.Score >= 0.70m &&
                        (x.MainContributorsJson == null ||
                         x.MainContributorsJson == ""))
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.RiskType,
                x.Score,
                x.RiskClass
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var invalidRiskScores = await dbContext.RiskScores
            .AsNoTracking()
            .Where(x => x.Score < 0m || x.Score > 1m)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.RiskType,
                x.Score
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var nonReadOnlySourceSystems = await dbContext.SourceSystemDefinitions
            .AsNoTracking()
            .Where(x => !x.IsReadOnlySource)
            .Select(x => new
            {
                x.Id,
                x.SourceSystemCode,
                x.SourceSystemName,
                x.SourceSystemType
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var validMappingTargets = new[]
        {
            "Site",
            "Area",
            "Equipment",
            "IndustryTemplate",
            "MaterialUnitTypeDefinition",
            "OperationDefinition",
            "Route",
            "RouteStep",
            "MaterialUnit",
            "MaterialAlias",
            "GenealogyEdge",
            "ProcessStepExecution",
            "ParameterDefinition",
            "ParameterObservation",
            "ProcessEvent",
            "DowntimeEvent",
            "DefectCatalog",
            "QualityEvent",
            "DataQualityIssue",
            "RiskScore"
        };

        var mappingsWithUnknownTarget = await dbContext.MappingDefinitions
            .AsNoTracking()
            .Where(x => !validMappingTargets.Contains(x.TargetEntityName))
            .Select(x => new
            {
                x.Id,
                x.MappingCode,
                x.SourceObjectName,
                x.TargetEntityName
            })
            .Take(100)
            .ToListAsync(cancellationToken);

        var issueCounts = new
        {
            materialTypesWithoutDefinition = materialTypesWithoutDefinition.Count,
            processStepsWithoutOperationDefinition = processStepsWithoutOperationDefinition.Count,
            observationsWithoutAnyValue = observationsWithoutAnyValue.Count,
            observationsOutsideStepWindow = observationsOutsideStepWindow.Count,
            processEventsWithoutAnyReference = processEventsWithoutAnyReference.Count,
            downtimeEventsWithoutAnyReference = downtimeEventsWithoutAnyReference.Count,
            defectEventsWithoutCatalog = defectEventsWithoutCatalog.Count,
            highRiskScoresWithoutContributors = highRiskScoresWithoutContributors.Count,
            invalidRiskScores = invalidRiskScores.Count,
            nonReadOnlySourceSystems = nonReadOnlySourceSystems.Count,
            mappingsWithUnknownTarget = mappingsWithUnknownTarget.Count
        };

        var blockingIssues =
            issueCounts.materialTypesWithoutDefinition +
            issueCounts.processStepsWithoutOperationDefinition +
            issueCounts.observationsWithoutAnyValue +
            issueCounts.observationsOutsideStepWindow +
            issueCounts.processEventsWithoutAnyReference +
            issueCounts.downtimeEventsWithoutAnyReference +
            issueCounts.invalidRiskScores +
            issueCounts.mappingsWithUnknownTarget;

        var warningIssues =
            issueCounts.defectEventsWithoutCatalog +
            issueCounts.highRiskScoresWithoutContributors +
            issueCounts.nonReadOnlySourceSystems;

        var status = blockingIssues > 0
            ? "Fail"
            : warningIssues > 0
                ? "Warning"
                : "Pass";

        return Results.Ok(new
        {
            product = "PlantProcess IQ",
            validationPurpose = "Validate that domain data, configuration, process records, quality records and workflow data are synchronized.",
            status,
            issueCounts,
            findings = new
            {
                materialTypesWithoutDefinition,
                processStepsWithoutOperationDefinition,
                observationsWithoutAnyValue,
                observationsOutsideStepWindow,
                processEventsWithoutAnyReference,
                downtimeEventsWithoutAnyReference,
                defectEventsWithoutCatalog,
                highRiskScoresWithoutContributors,
                invalidRiskScores,
                nonReadOnlySourceSystems,
                mappingsWithUnknownTarget
            }
        });
    }
}
