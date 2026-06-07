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
public async Task<ApplicationResult<IReadOnlyList<SourceDatasetDefinitionDto>>> DiscoverSchemaAsync(
        Guid connectionProfileId,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == connectionProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
            return ApplicationResult<IReadOnlyList<SourceDatasetDefinitionDto>>.Failure(
                ApplicationError.NotFound("Connection profile was not found."));

        try
        {
            var schemaReader = _connectorFactory.GetSchemaReader(profile.ProviderType);
            var discoveredDatasets = await schemaReader.DiscoverDatasetsAsync(profile, cancellationToken);

            var persistedDtos = new List<SourceDatasetDefinitionDto>();

            foreach (var discovered in discoveredDatasets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var datasetCode = NormalizeCode(discovered.DatasetCode);

                var dataset = await _dbContext.SourceDatasetDefinitions
                    .FirstOrDefaultAsync(
                        x => x.ConnectionProfileId == profile.Id &&
                            x.DatasetCode == datasetCode &&
                            !x.IsDeleted,
                        cancellationToken);

                if (dataset is null)
                {
                    dataset = new SourceDatasetDefinition(
                        connectionProfileId: profile.Id,
                        datasetCode: datasetCode,
                        datasetName: discovered.DatasetName,
                        datasetKind: discovered.DatasetKind,
                        sourceObjectName: discovered.SourceObjectName,
                        isSynthetic: false,
                        sourceSchemaName: discovered.SourceSchemaName,
                        datasetOptionsJson: discovered.DatasetOptionsJson,
                        description: "Discovered automatically by connector framework.",
                        sourceSystem: "ConnectorDiscovery",
                        sourceRecordId: discovered.SourceObjectName);

                    _dbContext.SourceDatasetDefinitions.Add(dataset);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    dataset.Update(
                        datasetName: discovered.DatasetName,
                        sourceObjectName: discovered.SourceObjectName,
                        sourceSchemaName: discovered.SourceSchemaName,
                        primaryTimestampField: dataset.PrimaryTimestampField,
                        incrementalCursorField: dataset.IncrementalCursorField,
                        refreshIntervalSeconds: dataset.RefreshIntervalSeconds,
                        datasetOptionsJson: discovered.DatasetOptionsJson,
                        description: dataset.Description);

                    dataset.Activate();
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                var fields = await schemaReader.DiscoverFieldsForDatasetAsync(profile, dataset, cancellationToken);

                foreach (var field in fields)
                {
                    var existingField = await _dbContext.SourceFieldDefinitions
                        .FirstOrDefaultAsync(
                            x => x.SourceDatasetDefinitionId == dataset.Id &&
                                x.FieldName == field.FieldName &&
                                !x.IsDeleted,
                            cancellationToken);

                    if (existingField is null)
                    {
                        _dbContext.SourceFieldDefinitions.Add(new SourceFieldDefinition(
                            sourceDatasetDefinitionId: dataset.Id,
                            fieldName: field.FieldName,
                            displayName: field.DisplayName,
                            sourceDataType: field.SourceDataType,
                            ordinal: field.Ordinal,
                            isNullable: field.IsNullable,
                            isSynthetic: false,
                            maxLength: field.MaxLength,
                            numericPrecision: field.NumericPrecision,
                            numericScale: field.NumericScale,
                            sampleValue: field.SampleValue,
                            isPrimaryKeyCandidate: field.IsPrimaryKeyCandidate,
                            isTimestampCandidate: field.IsTimestampCandidate,
                            sourceSystem: "ConnectorDiscovery",
                            sourceRecordId: $"{dataset.DatasetCode}.{field.FieldName}"));
                    }
                    else
                    {
                        existingField.UpdateProfile(
                            displayName: field.DisplayName,
                            sourceDataType: field.SourceDataType,
                            isNullable: field.IsNullable,
                            maxLength: field.MaxLength,
                            numericPrecision: field.NumericPrecision,
                            numericScale: field.NumericScale,
                            sampleValue: field.SampleValue,
                            isPrimaryKeyCandidate: field.IsPrimaryKeyCandidate,
                            isTimestampCandidate: field.IsTimestampCandidate);

                        existingField.Activate();
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                persistedDtos.Add(ToDatasetDto(dataset, profile));
            }

            return ApplicationResult<IReadOnlyList<SourceDatasetDefinitionDto>>.Success(persistedDtos);
        }
        catch (Exception ex)
        {
            return ApplicationResult<IReadOnlyList<SourceDatasetDefinitionDto>>.Failure(
                ApplicationError.Validation($"Schema discovery failed: {ex.Message}"));}
    }
}
