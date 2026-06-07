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

// PPIQ_REALIZATION_T027_PHASE1_WORKFLOW_TRUTH_CONTRACTS_SPLIT
public static partial class Phase1WorkflowTruthEndpoints
{
private static async Task<IResult> GetStagingSummaryAsync(
        string? sourceObjectName,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query =
            from batch in dbContext.ImportBatches.AsNoTracking()
            join record in dbContext.StagingRecords.AsNoTracking()
                on batch.Id equals record.ImportBatchId into recordGroup
            select new
            {
                batch.Id,
                batch.SourceSystemDefinitionId,
                batch.ImportBatchCode,
                batch.ImportType,
                batch.Status,
                batch.StartedAtUtc,
                batch.CompletedAtUtc,
                batch.SourceObjectName,
                batch.FileName,
                batch.RowCount,
                batch.ErrorMessage,
                Records = recordGroup
            };

        if (!string.IsNullOrWhiteSpace(sourceObjectName))
        {
            var normalized = sourceObjectName.Trim();
            query = query.Where(x => x.SourceObjectName == normalized);
        }

        var rows = await query
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(200)
            .Select(x => new StagingSummaryRow(
                x.Id,
                x.SourceSystemDefinitionId,
                x.ImportBatchCode,
                x.ImportType,
                x.Status,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.SourceObjectName,
                x.FileName,
                x.RowCount,
                x.ErrorMessage,
                x.Records.Count(),
                x.Records.Count(r => r.ProcessingStatus == "Pending"),
                x.Records.Count(r => r.ProcessingStatus == "Mapped"),
                x.Records.Count(r => r.ProcessingStatus == "Failed"),
                x.Records.Count(r => r.ProcessingStatus == "Skipped")))
            .ToListAsync(cancellationToken);

        var response = new StagingSummaryResponse(
            DateTime.UtcNow,
            "This is the raw latest-copy/staging layer before canonical mapping. It proves PlantProcess IQ copies source-shaped data first, then maps it into the generic model.",
            rows);

        return Results.Ok(response);
    }
}
