using PlantProcess.Application.Analytics.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Contracts.Common;
using PlantProcess.Application.Contracts.DataQuality;
using PlantProcess.Application.Contracts.Materials;
using PlantProcess.Application.Contracts.Process;
using PlantProcess.Application.Contracts.Quality;
using PlantProcess.Application.Integration.Contracts.Commands;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Integration.Contracts.SourceSystems;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Application.Services.Materials;
using PlantProcess.Application.Services.Process;
using PlantProcess.Application.Services.Quality;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Workflow;

// PPIQ_REALIZATION_T027_WORKFLOW_ENDPOINTS_HANDLERS_SPLIT
public static partial class WorkflowEndpoints
{
private static async Task<IResult> InvestigateMaterialAsync(
        Guid materialUnitId,
        [FromQuery] int? observationsTake,
        [FromQuery] int? observationsSkip,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Clamp inputs to sane bounds
        var take = Math.Clamp(observationsTake ?? 500, 1, 5_000);
        var skip = Math.Max(observationsSkip ?? 0, 0);

        // ─────────────────────────────────────────────────────
        // Block on the small, certain queries first
        // ─────────────────────────────────────────────────────
        var material = await dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.Id == materialUnitId && !x.IsDeleted)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
            return ApplicationProblems.NotFound("Material unit not found.");

        var aliases = await dbContext.MaterialAliases
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderBy(x => x.SourceSystem)
            .ThenBy(x => x.AliasCode)
            .ToListAsync(cancellationToken);

        var parentEdges = await dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x => x.ChildMaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderBy(x => x.EffectiveFromUtc)
            .ToListAsync(cancellationToken);

        var childEdges = await dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x => x.ParentMaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderBy(x => x.EffectiveFromUtc)
            .ToListAsync(cancellationToken);

        var processSteps = await dbContext.ProcessStepExecutions
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderBy(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

        // ─────────────────────────────────────────────────────
        // Parameter observations — PAGINATED
        // Get the count first (cheap), then the page.
        // ─────────────────────────────────────────────────────
        var parameterObservationsTotalCount = await dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
            .CountAsync(cancellationToken);

        var parameterObservations = await dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderByDescending(x => x.ObservedAtUtc) // newest first
            .Skip(skip)
            .Take(take)
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
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderBy(x => x.EventAtUtc)
            .ToListAsync(cancellationToken);

        var qualityEvents = await dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderBy(x => x.EventAtUtc)
            .ToListAsync(cancellationToken);

        var riskScores = await dbContext.RiskScores
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderByDescending(x => x.ScoredAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        // ─────────────────────────────────────────────────────
        // Compose the response
        // ─────────────────────────────────────────────────────
        return Results.Ok(new
        {
            material,
            aliases,
            genealogy = new
            {
                parentEdges,
                childEdges,
            },
            processSteps,
            parameterObservations = new
            {
                items = parameterObservations,
                totalCount = parameterObservationsTotalCount,
                pageSkip = skip,
                pageTake = take,
                hasMore = (skip + parameterObservations.Count) < parameterObservationsTotalCount,
            },
            processEvents,
            qualityEvents,
            riskScores,
        });
    }
}
