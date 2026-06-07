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
private static SourceDatasetDefinitionDto ToDatasetDto(
        SourceDatasetDefinition dataset,
        ConnectionProfile profile)
    {
        return new SourceDatasetDefinitionDto(
            Id: dataset.Id,
            ConnectionProfileId: dataset.ConnectionProfileId,
            ConnectionProfileCode: profile.ConnectionProfileCode,
            ProviderType: profile.ProviderType,
            DatasetCode: dataset.DatasetCode,
            DatasetName: dataset.DatasetName,
            DatasetKind: dataset.DatasetKind,
            SourceObjectName: dataset.SourceObjectName,
            SourceSchemaName: dataset.SourceSchemaName,
            PrimaryTimestampField: dataset.PrimaryTimestampField,
            IncrementalCursorField: dataset.IncrementalCursorField,
            LastCursorValue: dataset.LastCursorValue,
            RefreshIntervalSeconds: dataset.RefreshIntervalSeconds,
            DatasetOptionsJson: dataset.DatasetOptionsJson,
            IsActive: dataset.IsActive,
            Description: dataset.Description,
            IsSynthetic: dataset.IsSynthetic,
            CreatedAtUtc: dataset.CreatedAtUtc,
            UpdatedAtUtc: dataset.UpdatedAtUtc);
    }
}
