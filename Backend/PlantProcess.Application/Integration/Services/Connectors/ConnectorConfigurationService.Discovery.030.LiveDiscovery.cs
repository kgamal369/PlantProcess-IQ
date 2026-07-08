// ============================================================
// FILE: Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.Discovery.030.LiveDiscovery.cs
// M1-04: Live table + column discovery over a connected source.
// Reuses the EXISTING ISchemaReader.DiscoverDatasetsAsync / DiscoverFieldsForDatasetAsync
// resolved generically via IDataSourceConnectorFactory.GetSchemaReader(providerType).
// No provider-specific code, no raw SQL: pure application-layer wiring.
// Registration (raw registry function) lives in the endpoint, matching the two-stage import pattern.
// ============================================================
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.Connectors;

public sealed partial class ConnectorConfigurationService
{
    public async Task<ApplicationResult<IReadOnlyList<SourceTableDto>>> ListSourceTablesAsync(
        Guid connectionProfileId,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == connectionProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
            return ApplicationResult<IReadOnlyList<SourceTableDto>>.Failure(
                ApplicationError.NotFound("Connection profile not found."));

        ISchemaReader schemaReader;
        try
        {
            schemaReader = _connectorFactory.GetSchemaReader(profile.ProviderType);
        }
        catch (NotSupportedException ex)
        {
            return ApplicationResult<IReadOnlyList<SourceTableDto>>.Failure(
                ApplicationError.Validation($"Live table discovery is not supported for provider '{profile.ProviderType}'. {ex.Message}"));
        }

        try
        {
            var datasets = await schemaReader.DiscoverDatasetsAsync(profile, cancellationToken);
            var tables = datasets
                .Select(d => new SourceTableDto(
                    SchemaName: d.SourceSchemaName ?? string.Empty,
                    TableName: d.SourceObjectName,
                    Kind: d.DatasetKind))
                .OrderBy(x => x.SchemaName)
                .ThenBy(x => x.TableName)
                .ToList();

            return ApplicationResult<IReadOnlyList<SourceTableDto>>.Success(tables);
        }
        catch (Exception ex)
        {
            return ApplicationResult<IReadOnlyList<SourceTableDto>>.Failure(
                ApplicationError.Validation($"Live table discovery failed: {ex.Message}"));
        }
    }

    public async Task<ApplicationResult<IReadOnlyList<SourceColumnDto>>> ListSourceColumnsAsync(
        Guid connectionProfileId,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            return ApplicationResult<IReadOnlyList<SourceColumnDto>>.Failure(
                ApplicationError.Validation("Table name is required."));

        var profile = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == connectionProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
            return ApplicationResult<IReadOnlyList<SourceColumnDto>>.Failure(
                ApplicationError.NotFound("Connection profile not found."));

        ISchemaReader schemaReader;
        try
        {
            schemaReader = _connectorFactory.GetSchemaReader(profile.ProviderType);
        }
        catch (NotSupportedException ex)
        {
            return ApplicationResult<IReadOnlyList<SourceColumnDto>>.Failure(
                ApplicationError.Validation($"Column discovery is not supported for provider '{profile.ProviderType}'. {ex.Message}"));
        }

        var datasetDefinition = new SourceDatasetDefinition(
            connectionProfileId: profile.Id,
            datasetCode: "DISCOVERY_PROBE",
            datasetName: $"{schemaName}.{tableName}",
            datasetKind: "LiveProbe",
            sourceObjectName: tableName,
            isSynthetic: false,
            sourceSchemaName: string.IsNullOrWhiteSpace(schemaName) ? null : schemaName,
            primaryTimestampField: null,
            incrementalCursorField: null,
            refreshIntervalSeconds: 300,
            datasetOptionsJson: null,
            description: null,
            sourceSystem: null,
            sourceRecordId: null);

        try
        {
            var fields = await schemaReader.DiscoverFieldsForDatasetAsync(profile, datasetDefinition, cancellationToken);
            var columns = fields
                .OrderBy(f => f.Ordinal)
                .Select(f => new SourceColumnDto(
                    ColumnName: f.FieldName,
                    DataType: f.SourceDataType,
                    Ordinal: f.Ordinal,
                    IsNullable: f.IsNullable,
                    IsPrimaryKeyCandidate: f.IsPrimaryKeyCandidate,
                    IsTimestampCandidate: f.IsTimestampCandidate))
                .ToList();

            return ApplicationResult<IReadOnlyList<SourceColumnDto>>.Success(columns);
        }
        catch (Exception ex)
        {
            return ApplicationResult<IReadOnlyList<SourceColumnDto>>.Failure(
                ApplicationError.Validation($"Column discovery failed: {ex.Message}"));
        }
    }
}
