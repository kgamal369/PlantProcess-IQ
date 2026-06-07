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
private static async Task<IResult> GetStagingRecordsAsync(
        Guid? importBatchId,
        string? sourceObjectName,
        string? processingStatus,
        int? take,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var safeTake = take is > 0 and <= 1000 ? take.Value : 200;

        var query = dbContext.StagingRecords
            .AsNoTracking();

        if (importBatchId.HasValue)
            query = query.Where(x => x.ImportBatchId == importBatchId.Value);

        if (!string.IsNullOrWhiteSpace(sourceObjectName))
        {
            var normalized = sourceObjectName.Trim();
            query = query.Where(x => x.SourceObjectName == normalized);
        }

        if (!string.IsNullOrWhiteSpace(processingStatus))
        {
            var normalized = processingStatus.Trim();
            query = query.Where(x => x.ProcessingStatus == normalized);
        }

        var rows = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.RowNumber)
            .Take(safeTake)
            .Select(x => new StagingRecordRow(
                x.Id,
                x.ImportBatchId,
                x.SourceObjectName,
                x.RowNumber,
                x.RawJson,
                x.IsProcessed,
                x.ProcessedAtUtc,
                x.ProcessingStatus,
                x.ProcessingError,
                x.CanonicalEntityId,
                x.CanonicalEntityName,
                x.SourceSystem,
                x.SourceRecordId,
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(new StagingRecordsResponse(DateTime.UtcNow, rows.Count, rows));
    }
}
