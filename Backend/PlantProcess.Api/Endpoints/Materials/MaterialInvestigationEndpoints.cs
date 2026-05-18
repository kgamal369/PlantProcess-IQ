using Microsoft.EntityFrameworkCore;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Api.Endpoints.Materials;

public static class MaterialInvestigationEndpoints
{
    public static IEndpointRouteBuilder MapMaterialInvestigationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/materials")
            .WithTags("Material Investigation");

        group.MapGet("/{materialUnitId:guid}/investigation-full", InvestigateMaterialFullAsync);

        return app;
    }

    private static async Task<IResult> InvestigateMaterialFullAsync(
        Guid materialUnitId,
        int? maxDepth,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var materialExists = await dbContext.MaterialUnits
            .AsNoTracking()
            .AnyAsync(x => x.Id == materialUnitId, cancellationToken);

        if (!materialExists)
            return Results.NotFound(new { message = "Material unit not found." });

        var depthLimit = Math.Clamp(maxDepth ?? 5, 1, 20);
        var relatedMaterialIds = await ResolveGenealogyMaterialIdsAsync(
            dbContext,
            materialUnitId,
            depthLimit,
            cancellationToken);

        var materialIds = relatedMaterialIds.ToList();

        var materials = await dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => materialIds.Contains(x.Id))
            .OrderBy(x => x.ProductionStartUtc)
            .ThenBy(x => x.MaterialCode)
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

        var aliases = await dbContext.MaterialAliases
            .AsNoTracking()
            .Where(x => materialIds.Contains(x.MaterialUnitId))
            .OrderBy(x => x.SourceSystem)
            .ThenBy(x => x.AliasCode)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.AliasCode,
                x.AliasType,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var genealogyEdges = await dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x => materialIds.Contains(x.ParentMaterialUnitId) || materialIds.Contains(x.ChildMaterialUnitId))
            .OrderBy(x => x.EffectiveFromUtc)
            .Select(x => new
            {
                x.Id,
                x.ParentMaterialUnitId,
                x.ChildMaterialUnitId,
                x.RelationshipType,
                x.EffectiveFromUtc,
                x.EffectiveToUtc,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var processSteps = await dbContext.ProcessStepExecutions
            .AsNoTracking()
            .Where(x => materialIds.Contains(x.MaterialUnitId))
            .OrderBy(x => x.StartedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.EquipmentId,
                x.OperationType,
                x.OperationCode,
                x.CrewCode,
                x.StartedAtUtc,
                x.EndedAtUtc,
                x.StartedAtLocal,
                x.EndedAtLocal,
                x.PlantTimeZoneId,
                x.PlantUtcOffsetMinutes,
                x.ExecutionStatus,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var processStepIds = processSteps.Select(x => x.Id).ToList();

        var parameterObservations = await dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => materialIds.Contains(x.MaterialUnitId) ||
                        (x.ProcessStepExecutionId.HasValue && processStepIds.Contains(x.ProcessStepExecutionId.Value)))
            .OrderBy(x => x.ObservedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.ProcessStepExecutionId,
                x.ParameterDefinitionId,
                x.EquipmentId,
                x.ObservedAtUtc,
                x.ObservedAtLocal,
                x.NumericValue,
                x.TextValue,
                x.BooleanValue,
                x.UnitOfMeasure,
                x.QualityFlag,
                x.RawValue,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var processEvents = await dbContext.ProcessEvents
            .AsNoTracking()
            .Where(x =>
                (x.MaterialUnitId.HasValue && materialIds.Contains(x.MaterialUnitId.Value)) ||
                (x.ProcessStepExecutionId.HasValue && processStepIds.Contains(x.ProcessStepExecutionId.Value)))
            .OrderBy(x => x.EventAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.ProcessStepExecutionId,
                x.EquipmentId,
                x.EventType,
                x.EventAtUtc,
                x.EventAtLocal,
                x.EventValue,
                x.Description,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var downtimeEvents = await dbContext.DowntimeEvents
            .AsNoTracking()
            .Where(x =>
                (x.MaterialUnitId.HasValue && materialIds.Contains(x.MaterialUnitId.Value)) ||
                (x.ProcessStepExecutionId.HasValue && processStepIds.Contains(x.ProcessStepExecutionId.Value)))
            .OrderBy(x => x.StartedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.ProcessStepExecutionId,
                x.EquipmentId,
                x.StartedAtUtc,
                x.EndedAtUtc,
                x.StartedAtLocal,
                x.EndedAtLocal,
                x.DowntimeType,
                x.ReasonCode,
                x.Description,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var qualityEvents = await dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => materialIds.Contains(x.MaterialUnitId))
            .OrderBy(x => x.EventAtUtc)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.DefectCatalogId,
                x.EventType,
                x.EventAtUtc,
                x.EventAtLocal,
                x.Severity,
                x.Decision,
                x.Description,
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var riskScores = await dbContext.RiskScores
            .AsNoTracking()
            .Where(x => materialIds.Contains(x.MaterialUnitId))
            .OrderByDescending(x => x.ScoredAtUtc)
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
                x.SourceSystem,
                x.SourceRecordId,
                x.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        var dataQualityIssues = await dbContext.DataQualityIssues
            .AsNoTracking()
            .Where(x => x.MaterialUnitId.HasValue && materialIds.Contains(x.MaterialUnitId.Value))
            .OrderByDescending(x => x.CreatedAtUtc)
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

        return Results.Ok(new
        {
            requestedMaterialUnitId = materialUnitId,
            maxDepth = depthLimit,
            summary = new
            {
                materials = materials.Count,
                aliases = aliases.Count,
                genealogyEdges = genealogyEdges.Count,
                processSteps = processSteps.Count,
                parameterObservations = parameterObservations.Count,
                processEvents = processEvents.Count,
                downtimeEvents = downtimeEvents.Count,
                qualityEvents = qualityEvents.Count,
                riskScores = riskScores.Count,
                dataQualityIssues = dataQualityIssues.Count
            },
            materials,
            aliases,
            genealogyEdges,
            processSteps,
            parameterObservations,
            processEvents,
            downtimeEvents,
            qualityEvents,
            riskScores,
            dataQualityIssues
        });
    }

    private static async Task<HashSet<Guid>> ResolveGenealogyMaterialIdsAsync(
        PlantProcessDbContext dbContext,
        Guid rootMaterialUnitId,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { rootMaterialUnitId };
        var frontier = new HashSet<Guid> { rootMaterialUnitId };

        for (var depth = 0; depth < maxDepth && frontier.Count > 0; depth++)
        {
            var frontierIds = frontier.ToList();

            var edges = await dbContext.GenealogyEdges
                .AsNoTracking()
                .Where(x => frontierIds.Contains(x.ParentMaterialUnitId) || frontierIds.Contains(x.ChildMaterialUnitId))
                .Select(x => new { x.ParentMaterialUnitId, x.ChildMaterialUnitId })
                .ToListAsync(cancellationToken);

            var next = new HashSet<Guid>();

            foreach (var edge in edges)
            {
                if (visited.Add(edge.ParentMaterialUnitId))
                    next.Add(edge.ParentMaterialUnitId);

                if (visited.Add(edge.ChildMaterialUnitId))
                    next.Add(edge.ChildMaterialUnitId);
            }

            frontier = next;
        }

        return visited;
    }
}
