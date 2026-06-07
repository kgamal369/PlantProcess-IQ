using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.Connectors;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.Connectors;

// PPIQ_REALIZATION_T028_CONNECTOR_CONFIGURATION_SERVICE_DATASETS_SPLIT
public sealed partial class ConnectorConfigurationService
{
public async Task<ApplicationResult<IReadOnlyList<SourceDatasetDefinitionDto>>> GetDatasetsAsync(
        Guid? connectionProfileId,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query =
            from dataset in _dbContext.SourceDatasetDefinitions.AsNoTracking()
            join profile in _dbContext.ConnectionProfiles.AsNoTracking()
                on dataset.ConnectionProfileId equals profile.Id
            where !dataset.IsDeleted && !profile.IsDeleted
            select new { dataset, profile };

        if (connectionProfileId.HasValue)
            query = query.Where(x => x.dataset.ConnectionProfileId == connectionProfileId.Value);

        if (!includeInactive)
            query = query.Where(x => x.dataset.IsActive);

        var rows = await query
            .OrderBy(x => x.profile.ConnectionProfileCode)
            .ThenBy(x => x.dataset.DatasetCode)
            .Select(x => new SourceDatasetDefinitionDto(
                x.dataset.Id,
                x.dataset.ConnectionProfileId,
                x.profile.ConnectionProfileCode,
                x.profile.ProviderType,
                x.dataset.DatasetCode,
                x.dataset.DatasetName,
                x.dataset.DatasetKind,
                x.dataset.SourceObjectName,
                x.dataset.SourceSchemaName,
                x.dataset.PrimaryTimestampField,
                x.dataset.IncrementalCursorField,
                x.dataset.LastCursorValue,
                x.dataset.RefreshIntervalSeconds,
                x.dataset.DatasetOptionsJson,
                x.dataset.IsActive,
                x.dataset.Description,
                x.dataset.IsSynthetic,
                x.dataset.CreatedAtUtc,
                x.dataset.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return ApplicationResult<IReadOnlyList<SourceDatasetDefinitionDto>>.Success(rows);
    }
}
