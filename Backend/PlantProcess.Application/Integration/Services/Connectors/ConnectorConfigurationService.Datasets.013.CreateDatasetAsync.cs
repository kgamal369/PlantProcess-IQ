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
public async Task<ApplicationResult<SourceDatasetDefinitionDto>> CreateDatasetAsync(
        CreateSourceDatasetDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ConnectionProfileId == Guid.Empty)
            return ApplicationResult<SourceDatasetDefinitionDto>.Failure(ApplicationError.Validation("ConnectionProfileId is required."));

        if (string.IsNullOrWhiteSpace(request.DatasetCode))
            return ApplicationResult<SourceDatasetDefinitionDto>.Failure(ApplicationError.Validation("DatasetCode is required."));

        if (string.IsNullOrWhiteSpace(request.DatasetName))
            return ApplicationResult<SourceDatasetDefinitionDto>.Failure(ApplicationError.Validation("DatasetName is required."));

        if (string.IsNullOrWhiteSpace(request.DatasetKind))
            return ApplicationResult<SourceDatasetDefinitionDto>.Failure(ApplicationError.Validation("DatasetKind is required."));

        if (string.IsNullOrWhiteSpace(request.SourceObjectName))
            return ApplicationResult<SourceDatasetDefinitionDto>.Failure(ApplicationError.Validation("SourceObjectName is required."));

        var profileExists = await _dbContext.ConnectionProfiles
            .AnyAsync(x => x.Id == request.ConnectionProfileId && !x.IsDeleted, cancellationToken);

        if (!profileExists)
            return ApplicationResult<SourceDatasetDefinitionDto>.Failure(ApplicationError.NotFound("Connection profile not found."));

        var duplicate = await _dbContext.SourceDatasetDefinitions
            .AnyAsync(x =>
                !x.IsDeleted &&
                x.ConnectionProfileId == request.ConnectionProfileId &&
                x.DatasetCode == request.DatasetCode.Trim(),
                cancellationToken);

        if (duplicate)
            return ApplicationResult<SourceDatasetDefinitionDto>.Failure(ApplicationError.Conflict("Dataset code already exists for this connection profile."));

        var entity = new SourceDatasetDefinition(
            connectionProfileId: request.ConnectionProfileId,
            datasetCode: request.DatasetCode,
            datasetName: request.DatasetName,
            datasetKind: request.DatasetKind,
            sourceObjectName: request.SourceObjectName,
            isSynthetic: request.IsSynthetic,
            sourceSchemaName: request.SourceSchemaName,
            primaryTimestampField: request.PrimaryTimestampField,
            incrementalCursorField: request.IncrementalCursorField,
            refreshIntervalSeconds: request.RefreshIntervalSeconds ?? 300,
            datasetOptionsJson: request.DatasetOptionsJson,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        _dbContext.SourceDatasetDefinitions.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var created = await GetDatasetDtoQuery()
            .FirstAsync(x => x.Id == entity.Id, cancellationToken);

        return ApplicationResult<SourceDatasetDefinitionDto>.Success(created);
    }
}
