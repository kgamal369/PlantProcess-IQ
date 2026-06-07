using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;
using PlantProcess.Infrastructure.Persistence;

using PlantProcess.Api.ErrorHandling;

namespace PlantProcess.Api.Endpoints.Admin;

// PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_HANDLERS_SPLIT
public static partial class Phase1WorkflowTruthEndpoints
{
private static async Task<IResult> GetImportJobConfigurationBoardAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var mappings = await dbContext.MappingDefinitions
            .AsNoTracking()
            .OrderBy(x => x.MappingCode)
            .Select(x => new
            {
                x.Id,
                x.MappingCode,
                x.MappingName,
                x.SourceObjectName,
                x.TargetEntityName,
                x.IsActive,
                x.Description
            })
            .ToListAsync(cancellationToken);

        var jobs = await dbContext.JobDefinitions
            .AsNoTracking()
            .Where(x => x.TargetType == "MappingDefinition")
            .OrderBy(x => x.JobCode)
            .Select(x => new ImportJobConfigurationRow(
                x.Id,
                x.JobCode,
                x.JobName,
                x.JobType.ToString(),
                x.TargetId,
                x.TargetType,
                x.ScheduleExpression,
                x.IsEnabled,
                x.LastRunStatus.ToString(),
                x.LastRunStartedAtUtc,
                x.LastRunCompletedAtUtc,
                x.LastRunDurationMs,
                x.LastFailureReason,
                x.NextRunAtUtc,
                x.Description,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var rows = mappings.Select(mapping =>
        {
            var existing = jobs.FirstOrDefault(x => x.TargetId == mapping.Id);

            return new MappingImportJobCandidateRow(
                mapping.Id,
                mapping.MappingCode,
                mapping.MappingName,
                mapping.SourceObjectName,
                mapping.TargetEntityName,
                mapping.IsActive,
                existing?.JobDefinitionId,
                existing?.JobCode,
                existing?.IsEnabled ?? false,
                existing?.ScheduleExpression,
                existing?.LastRunStatus,
                existing?.NextRunAtUtc);
        }).ToList();

        return Results.Ok(new ImportJobConfigurationBoardResponse(
            DateTime.UtcNow,
            "This board supports the Admin > DB Configuration > Importing Data tab. It turns approved mappings into scheduled canonical import jobs.",
            rows,
            jobs));
    }
}
