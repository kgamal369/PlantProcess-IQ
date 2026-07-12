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
private IQueryable<SourceDatasetDefinitionDto> GetDatasetDtoQuery(Guid? datasetId = null)
    {
        return
            from dataset in _dbContext.SourceDatasetDefinitions.AsNoTracking()
            join profile in _dbContext.ConnectionProfiles.AsNoTracking()
                on dataset.ConnectionProfileId equals profile.Id
            where !dataset.IsDeleted && !profile.IsDeleted && (datasetId == null || dataset.Id == datasetId.Value)
            select new SourceDatasetDefinitionDto(
                dataset.Id,
                dataset.ConnectionProfileId,
                profile.ConnectionProfileCode,
                profile.ProviderType,
                dataset.DatasetCode,
                dataset.DatasetName,
                dataset.DatasetKind,
                dataset.SourceObjectName,
                dataset.SourceSchemaName,
                dataset.PrimaryTimestampField,
                dataset.IncrementalCursorField,
                dataset.LastCursorValue,
                dataset.RefreshIntervalSeconds,
                dataset.DatasetOptionsJson,
                dataset.IsActive,
                dataset.Description,
                dataset.IsSynthetic,
                dataset.CreatedAtUtc,
                dataset.UpdatedAtUtc);
    }
}
