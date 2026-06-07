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
private static async Task<IResult> GetSourceScheduleBoardAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var rows = await (
                from dataset in dbContext.SourceDatasetDefinitions.AsNoTracking()
                join profile in dbContext.ConnectionProfiles.AsNoTracking()
                    on dataset.ConnectionProfileId equals profile.Id
                join source in dbContext.SourceSystemDefinitions.AsNoTracking()
                    on profile.SourceSystemDefinitionId equals source.Id
                select new SourceScheduleRow(
                    dataset.Id,
                    dataset.ConnectionProfileId,
                    profile.ConnectionProfileCode,
                    profile.ConnectionProfileName,
                    profile.ProviderType,
                    source.Id,
                    source.SourceSystemCode,
                    source.SourceSystemName,
                    dataset.DatasetCode,
                    dataset.DatasetName,
                    dataset.DatasetKind,
                    dataset.SourceSchemaName,
                    dataset.SourceObjectName,
                    dataset.PrimaryTimestampField,
                    dataset.IncrementalCursorField,
                    dataset.LastCursorValue,
                    dataset.RefreshIntervalSeconds,
                    dataset.NextRunAtUtc,
                    dataset.IsActive,
                    profile.IsActive,
                    dataset.NextRunAtUtc == null || dataset.NextRunAtUtc <= now,
                    dataset.Description,
                    dataset.CreatedAtUtc,
                    dataset.UpdatedAtUtc))
            .OrderBy(x => x.NextRunAtUtc ?? DateTime.MinValue)
            .ThenBy(x => x.ProviderType)
            .ThenBy(x => x.DatasetCode)
            .ToListAsync(cancellationToken);

        var response = new SourceScheduleBoardResponse(
            DateTime.UtcNow,
            rows.Count,
            rows.Count(x => x.IsDueNow && x.IsDatasetActive && x.IsConnectionActive),
            rows);

        return Results.Ok(response);
    }
}
