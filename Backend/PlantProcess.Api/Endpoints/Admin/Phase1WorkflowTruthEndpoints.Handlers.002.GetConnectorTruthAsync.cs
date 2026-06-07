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
private static async Task<IResult> GetConnectorTruthAsync(
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var providerRows = BuildProviderTruthRows();

        var activeProfilesByProvider = await dbContext.ConnectionProfiles
            .AsNoTracking()
            .GroupBy(x => x.ProviderType)
            .Select(g => new
            {
                ProviderType = g.Key,
                ActiveProfiles = g.Count(x => x.IsActive),
                TotalProfiles = g.Count()
            })
            .ToListAsync(cancellationToken);

        var datasetsByProvider = await (
                from dataset in dbContext.SourceDatasetDefinitions.AsNoTracking()
                join profile in dbContext.ConnectionProfiles.AsNoTracking()
                    on dataset.ConnectionProfileId equals profile.Id
                group dataset by profile.ProviderType
                into g
                select new
                {
                    ProviderType = g.Key,
                    ActiveDatasets = g.Count(x => x.IsActive),
                    TotalDatasets = g.Count()
                })
            .ToListAsync(cancellationToken);

        var enriched = providerRows
            .Select(row =>
            {
                var profileCount = activeProfilesByProvider
                    .FirstOrDefault(x => SameProvider(x.ProviderType, row.ProviderType));

                var datasetCount = datasetsByProvider
                    .FirstOrDefault(x => SameProvider(x.ProviderType, row.ProviderType));

                return row with
                {
                    ActiveConnectionProfiles = profileCount?.ActiveProfiles ?? 0,
                    TotalConnectionProfiles = profileCount?.TotalProfiles ?? 0,
                    ActiveSourceDatasets = datasetCount?.ActiveDatasets ?? 0,
                    TotalSourceDatasets = datasetCount?.TotalDatasets ?? 0
                };
            })
            .OrderBy(x => x.SortOrder)
            .ToList();

        var response = new ConnectorTruthMatrixResponse(
            GeneratedAtUtc: DateTime.UtcNow,
            OperatingRule:
            "Frontend must use this API as the single connector truth source. Do not hardcode connector availability in React.",
            Providers: enriched);

        return Results.Ok(response);
    }
}
