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
private static async Task<IResult> UpdateDatasetCursorAsync(
        Guid sourceDatasetDefinitionId,
        [FromBody] UpdateDatasetCursorRequest request,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var dataset = await dbContext.SourceDatasetDefinitions
            .FirstOrDefaultAsync(x => x.Id == sourceDatasetDefinitionId, cancellationToken);

        if (dataset is null)
            return ApplicationProblems.NotFound("Source dataset definition not found.");

        dataset.UpdateLastCursorValue(request.LastCursorValue);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildSourceScheduleRowAsync(sourceDatasetDefinitionId, dbContext, cancellationToken);
    }
}
