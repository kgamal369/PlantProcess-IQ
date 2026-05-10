using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities.Quality;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.DataQuality;

public static class DataQualityScanEndpoints
{
    public static IEndpointRouteBuilder MapDataQualityScanEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/data-quality")
            .WithTags("Data Quality");

        group.MapGet("/scan-preview", async (
            int? take,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var candidates = await BuildDataQualityCandidatesAsync(dbContext, take ?? 500, cancellationToken);
            return Results.Ok(new
            {
                persistIssues = false,
                generatedCandidates = candidates.Count,
                candidates
            });
        });

        group.MapPost("/scan", async (
            bool? persistIssues,
            int? take,
            PlantProcessDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var shouldPersist = persistIssues ?? true;
            var candidates = await BuildDataQualityCandidatesAsync(dbContext, take ?? 500, cancellationToken);
            var insertedCount = 0;

            if (shouldPersist)
            {
                foreach (var candidate in candidates)
                {
                    var exists = await dbContext.DataQualityIssues.AnyAsync(x =>
                        x.IssueType == candidate.IssueType &&
                        x.AffectedEntityName == candidate.AffectedEntityName &&
                        x.AffectedEntityId == candidate.AffectedEntityId,
                        cancellationToken);

                    if (exists)
                        continue;

                    dbContext.DataQualityIssues.Add(new DataQualityIssue(
                        issueType: candidate.IssueType,
                        description: candidate.Description,
                        isSynthetic: candidate.IsSynthetic,
                        materialUnitId: candidate.MaterialUnitId,
                        severity: candidate.Severity,
                        affectedEntityName: candidate.AffectedEntityName,
                        affectedEntityId: candidate.AffectedEntityId,
                        sourceSystem: candidate.SourceSystem,
                        sourceRecordId: candidate.SourceRecordId));

                    insertedCount++;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(new
            {
                persistIssues = shouldPersist,
                generatedCandidates = candidates.Count,
                insertedIssues = insertedCount,
                candidates
            });
        });

        return app;
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
            IssueType: "MissingMaterialAlias",
            Severity: "Warning",
            Description: $"Material '{x.MaterialCode}' has no source-system alias. This reduces cross-system traceability.",
            AffectedEntityName: "MaterialUnit",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.Id,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        var materialsWithoutSteps = await dbContext.MaterialUnits
            .AsNoTracking()
            .Where(material => !dbContext.ProcessStepExecutions.Any(step => step.MaterialUnitId == material.Id))
            .OrderBy(x => x.MaterialCode)
            .Take(maxRows)
            .Select(x => new { x.Id, x.MaterialCode, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(materialsWithoutSteps.Select(x => new DataQualityCandidate(
            IssueType: "MissingProcessHistory",
            Severity: "Warning",
            Description: $"Material '{x.MaterialCode}' has no process step execution. Investigation timeline will be incomplete.",
            AffectedEntityName: "MaterialUnit",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.Id,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        var observationsWithoutValue = await dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => x.NumericValue == null && x.TextValue == null && x.BooleanValue == null)
            .OrderBy(x => x.ObservedAtUtc)
            .Take(maxRows)
            .Select(x => new { x.Id, x.MaterialUnitId, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(observationsWithoutValue.Select(x => new DataQualityCandidate(
            IssueType: "MissingParameterValue",
            Severity: "Error",
            Description: "Parameter observation has no numeric, text or boolean value.",
            AffectedEntityName: "ParameterObservation",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.MaterialUnitId,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        var observationsWithoutStep = await dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => x.ProcessStepExecutionId == null)
            .OrderBy(x => x.ObservedAtUtc)
            .Take(maxRows)
            .Select(x => new { x.Id, x.MaterialUnitId, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(observationsWithoutStep.Select(x => new DataQualityCandidate(
            IssueType: "ParameterObservationWithoutProcessStep",
            Severity: "Warning",
            Description: "Parameter observation is linked to a material but not to a process step. This weakens process-window analytics.",
            AffectedEntityName: "ParameterObservation",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.MaterialUnitId,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        var observationsOutsideStepWindow = await dbContext.ParameterObservations
            .AsNoTracking()
            .Join(
                dbContext.ProcessStepExecutions.AsNoTracking(),
                observation => observation.ProcessStepExecutionId,
                step => step.Id,
                (observation, step) => new { observation, step })
            .Where(x =>
                x.observation.ObservedAtUtc < x.step.StartedAtUtc ||
                (x.step.EndedAtUtc.HasValue && x.observation.ObservedAtUtc > x.step.EndedAtUtc.Value))
            .OrderBy(x => x.observation.ObservedAtUtc)
            .Take(maxRows)
            .Select(x => new
            {
                x.observation.Id,
                x.observation.MaterialUnitId,
                x.observation.ObservedAtUtc,
                StepStartedAtUtc = x.step.StartedAtUtc,
                StepEndedAtUtc = x.step.EndedAtUtc,
                x.observation.IsSynthetic,
                x.observation.SourceSystem,
                x.observation.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        result.AddRange(observationsOutsideStepWindow.Select(x => new DataQualityCandidate(
            IssueType: "ParameterObservationOutsideStepWindow",
            Severity: "Error",
            Description: $"Parameter observation at {x.ObservedAtUtc:o} is outside the linked process step window [{x.StepStartedAtUtc:o} - {x.StepEndedAtUtc:o}].",
            AffectedEntityName: "ParameterObservation",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.MaterialUnitId,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        var defectEventsWithoutCatalog = await dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => x.EventType == "Defect" && x.DefectCatalogId == null)
            .OrderBy(x => x.EventAtUtc)
            .Take(maxRows)
            .Select(x => new { x.Id, x.MaterialUnitId, x.EventType, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(defectEventsWithoutCatalog.Select(x => new DataQualityCandidate(
            IssueType: "DefectEventWithoutCatalog",
            Severity: "Warning",
            Description: "Quality event is marked as Defect but is not linked to a standardized defect catalog record.",
            AffectedEntityName: "QualityEvent",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.MaterialUnitId,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        var highRiskWithoutContributors = await dbContext.RiskScores
            .AsNoTracking()
            .Where(x => x.Score >= 0.70m && (x.MainContributorsJson == null || x.MainContributorsJson == ""))
            .OrderByDescending(x => x.ScoredAtUtc)
            .Take(maxRows)
            .Select(x => new { x.Id, x.MaterialUnitId, x.RiskType, x.Score, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(highRiskWithoutContributors.Select(x => new DataQualityCandidate(
            IssueType: "HighRiskScoreWithoutContributors",
            Severity: "Warning",
            Description: $"Risk score '{x.RiskType}' is high ({x.Score}) but has no contributor explanation JSON.",
            AffectedEntityName: "RiskScore",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.MaterialUnitId,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        var nonReadOnlySources = await dbContext.SourceSystemDefinitions
            .AsNoTracking()
            .Where(x => !x.IsReadOnlySource)
            .OrderBy(x => x.SourceSystemCode)
            .Take(maxRows)
            .Select(x => new { x.Id, x.SourceSystemCode, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(nonReadOnlySources.Select(x => new DataQualityCandidate(
            IssueType: "SourceSystemNotReadOnly",
            Severity: "Warning",
            Description: $"Source system '{x.SourceSystemCode}' is not marked as read-only. MVP/pilot integrations should normally be read-only.",
            AffectedEntityName: "SourceSystemDefinition",
            AffectedEntityId: x.Id,
            MaterialUnitId: null,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

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