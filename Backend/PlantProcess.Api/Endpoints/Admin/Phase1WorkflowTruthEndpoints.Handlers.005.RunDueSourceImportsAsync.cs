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
private static async Task<IResult> RunDueSourceImportsAsync(
        [FromBody] RunDueSourceImportsRequest request,
        [FromServices] IDeltaImportExecutionService deltaImportExecutionService,
        CancellationToken cancellationToken)
    {
        var maxDatasets = request.MaxDatasetsPerRun is > 0 and <= 200
            ? request.MaxDatasetsPerRun.Value
            : 25;

        var maxRows = request.MaxRowsPerDataset is > 0 and <= 50_000
            ? request.MaxRowsPerDataset.Value
            : 5_000;

        var sw = Stopwatch.StartNew();

        var summary = await deltaImportExecutionService.ExecuteAllAsync(
            maxDatasets,
            maxRows,
            cancellationToken);

        sw.Stop();

        var response = new RunDueSourceImportsResponse(
            DateTime.UtcNow,
            maxDatasets,
            maxRows,
            sw.ElapsedMilliseconds,
            summary.DatasetsProcessed,
            summary.TotalRowsImported,
            summary.DatasetsFailedCount,
            summary.DatasetResults.Select(x => new RunDueSourceDatasetResult(
                    x.DatasetId,
                    x.DatasetCode,
                    x.RowsImported,
                    x.ErrorMessage))
                .ToList());

        return Results.Ok(response);
    }
}
