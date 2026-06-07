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
private static async Task<IResult> CreateImportJobFromMappingAsync(
        [FromBody] CreateImportJobFromMappingRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var mapping = await dbContext.MappingDefinitions
            .FirstOrDefaultAsync(x => x.Id == request.MappingDefinitionId, cancellationToken);

        if (mapping is null)
            return ApplicationProblems.NotFound("Mapping definition not found.");

        if (!mapping.IsActive)
            return ApplicationProblems.Validation("Mapping definition is not active.");

        var jobCode = string.IsNullOrWhiteSpace(request.JobCode)
            ? $"CANONICAL_IMPORT_{mapping.MappingCode}"
            : request.JobCode.Trim();

        var normalizedJobCode = NormalizeCode(jobCode);

        var existing = await dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => x.JobCode == normalizedJobCode, cancellationToken);

        var scheduleExpression = string.IsNullOrWhiteSpace(request.ScheduleExpression)
            ? "Every 15 minutes"
            : request.ScheduleExpression.Trim();

        if (existing is null)
        {
            existing = new JobDefinition(
                jobCode: normalizedJobCode,
                jobName: string.IsNullOrWhiteSpace(request.JobName)
                    ? $"Canonical import - {mapping.MappingName}"
                    : request.JobName.Trim(),
                jobType: JobDefinitionType.CanonicalRefresh,
                scheduleExpression: scheduleExpression,
                isSynthetic: request.IsSynthetic,
                targetId: mapping.Id,
                targetType: "MappingDefinition",
                isEnabled: request.IsEnabled,
                description: request.Description ??
                             $"Imports {mapping.SourceObjectName} into canonical {mapping.TargetEntityName}.",
                sourceSystem: "PlantProcessIQ.Phase1",
                sourceRecordId: mapping.MappingCode);

            dbContext.JobDefinitions.Add(existing);
        }
        else
        {
            existing.UpdateDefinition(
                jobName: string.IsNullOrWhiteSpace(request.JobName)
                    ? existing.JobName
                    : request.JobName.Trim(),
                jobType: JobDefinitionType.CanonicalRefresh,
                scheduleExpression: scheduleExpression,
                targetId: mapping.Id,
                targetType: "MappingDefinition",
                isEnabled: request.IsEnabled,
                description: request.Description ?? existing.Description);

            if (request.IsEnabled)
                existing.Enable(existing.NextRunAtUtc);
            else
                existing.Disable();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ImportJobConfigurationRow(
            existing.Id,
            existing.JobCode,
            existing.JobName,
            existing.JobType.ToString(),
            existing.TargetId,
            existing.TargetType,
            existing.ScheduleExpression,
            existing.IsEnabled,
            existing.LastRunStatus.ToString(),
            existing.LastRunStartedAtUtc,
            existing.LastRunCompletedAtUtc,
            existing.LastRunDurationMs,
            existing.LastFailureReason,
            existing.NextRunAtUtc,
            existing.Description,
            existing.CreatedAtUtc,
            existing.UpdatedAtUtc));
    }
}
