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

public sealed class ConnectorConfigurationService : IConnectorConfigurationService
{
    private readonly IDataSourceConnectorFactory _connectorFactory;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IPlantProcessDbContext _dbContext;

    public ConnectorConfigurationService(IPlantProcessDbContext dbContext,
    IDataSourceConnectorFactory connectorFactory)
    {
        _dbContext = dbContext;
        _connectorFactory = connectorFactory;

    }

    public IReadOnlyList<ProviderTypeDto> GetProviderTypes()
    {
        return new List<ProviderTypeDto>
        {
            new(
                "Csv",
                "CSV Snapshot",
                "Reads uploaded/exported CSV text into the PlantProcess IQ raw staging layer.",
                IsAvailableNow: true,
                RequiresSecretReference: false,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false),

            new(
                "Excel",
                "Excel Snapshot",
                "Planned next file connector for workbook/sheet snapshots.",
                IsAvailableNow: false,
                RequiresSecretReference: false,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: false),

            new(
                "PostgreSql",
                "PostgreSQL Read-only DB Link",
                "Planned read-only database connector for PostgreSQL sources.",
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new(
                "SqlServer",
                "Microsoft SQL Server Read-only DB Link",
                "Planned read-only database connector for SQL Server / MSSQL sources.",
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new(
                "Oracle",
                "Oracle Read-only DB Link",
                "Planned read-only database connector for Oracle MES/L2/QMS sources.",
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: true,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true),

            new(
                "RestApi",
                "REST API Snapshot",
                "Planned API snapshot connector.",
                IsAvailableNow: false,
                RequiresSecretReference: true,
                SupportsSchemaDiscovery: false,
                SupportsSnapshotImport: true,
                SupportsIncrementalImport: true)
        };
    }

    public async Task<ApplicationResult<IReadOnlyList<ConnectionProfileDto>>> GetConnectionProfilesAsync(
        Guid? sourceSystemDefinitionId,
        string? providerType,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query =
            from profile in _dbContext.ConnectionProfiles.AsNoTracking()
            join source in _dbContext.SourceSystemDefinitions.AsNoTracking()
                on profile.SourceSystemDefinitionId equals source.Id
            where !profile.IsDeleted && !source.IsDeleted
            select new { profile, source };

        if (sourceSystemDefinitionId.HasValue)
            query = query.Where(x => x.profile.SourceSystemDefinitionId == sourceSystemDefinitionId.Value);

        if (!string.IsNullOrWhiteSpace(providerType))
            query = query.Where(x => x.profile.ProviderType == NormalizeProviderType(providerType));

        if (!includeInactive)
            query = query.Where(x => x.profile.IsActive);

        var rows = await query
            .OrderBy(x => x.profile.ProviderType)
            .ThenBy(x => x.profile.ConnectionProfileCode)
            .Select(x => new ConnectionProfileDto(
                x.profile.Id,
                x.profile.SourceSystemDefinitionId,
                x.source.SourceSystemCode,
                x.source.SourceSystemName,
                x.profile.ConnectionProfileCode,
                x.profile.ConnectionProfileName,
                x.profile.ProviderType,
                x.profile.ConnectionMode,
                x.profile.HostName,
                x.profile.Port,
                x.profile.DatabaseName,
                x.profile.SchemaName,
                x.profile.FileRootPath,
                x.profile.ApiBaseUrl,
                x.profile.SecretReference,
                x.profile.ConnectionOptionsJson,
                x.profile.IsActive,
                x.profile.ReadOnlyEnforced,
                x.profile.Description,
                x.profile.LastTestedAtUtc,
                x.profile.LastTestStatus,
                x.profile.LastTestMessage,
                x.profile.IsSynthetic,
                x.profile.CreatedAtUtc,
                x.profile.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return ApplicationResult<IReadOnlyList<ConnectionProfileDto>>.Success(rows);
    }

    public async Task<ApplicationResult<ConnectionProfileDto>> GetConnectionProfileByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var profile = await GetConnectionProfileDtoQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return profile is null
            ? ApplicationResult<ConnectionProfileDto>.Failure(ApplicationError.NotFound("Connection profile not found."))
            : ApplicationResult<ConnectionProfileDto>.Success(profile);
    }

    public async Task<ApplicationResult<ConnectionProfileDto>> CreateConnectionProfileAsync(
        CreateConnectionProfileRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateConnectionProfileRequest(request);

        if (validation is not null)
            return ApplicationResult<ConnectionProfileDto>.Failure(validation);

        var providerType = NormalizeProviderType(request.ProviderType);

        var provider = GetProviderTypes().FirstOrDefault(x => x.ProviderType == providerType);

        if (provider is null)
        {
            return ApplicationResult<ConnectionProfileDto>.Failure(
                ApplicationError.Validation($"Unsupported provider type '{request.ProviderType}'."));
        }

        var sourceExists = await _dbContext.SourceSystemDefinitions
            .AnyAsync(x =>
                x.Id == request.SourceSystemDefinitionId &&
                !x.IsDeleted,
                cancellationToken);

        if (!sourceExists)
        {
            return ApplicationResult<ConnectionProfileDto>.Failure(
                ApplicationError.NotFound("Source system definition not found."));
        }

        var duplicate = await _dbContext.ConnectionProfiles
            .AnyAsync(x =>
                !x.IsDeleted &&
                x.ConnectionProfileCode == request.ConnectionProfileCode.Trim(),
                cancellationToken);

        if (duplicate)
        {
            return ApplicationResult<ConnectionProfileDto>.Failure(
                ApplicationError.Conflict("Connection profile code already exists."));
        }

        var entity = new ConnectionProfile(
            sourceSystemDefinitionId: request.SourceSystemDefinitionId,
            connectionProfileCode: request.ConnectionProfileCode,
            connectionProfileName: request.ConnectionProfileName,
            providerType: providerType,
            isSynthetic: request.IsSynthetic,
            connectionMode: request.ConnectionMode ?? "Snapshot",
            hostName: request.HostName,
            port: request.Port,
            databaseName: request.DatabaseName,
            schemaName: request.SchemaName,
            fileRootPath: request.FileRootPath,
            apiBaseUrl: request.ApiBaseUrl,
            secretReference: request.SecretReference,
            connectionOptionsJson: request.ConnectionOptionsJson,
            readOnlyEnforced: request.ReadOnlyEnforced ?? true,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        _dbContext.ConnectionProfiles.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetConnectionProfileByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ApplicationResult<ConnectionProfileDto>> UpdateConnectionProfileAsync(
        Guid id,
        UpdateConnectionProfileRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return ApplicationResult<ConnectionProfileDto>.Failure(
                ApplicationError.NotFound("Connection profile not found."));
        }

        entity.Update(
            connectionProfileName: request.ConnectionProfileName,
            connectionMode: request.ConnectionMode ?? "Snapshot",
            hostName: request.HostName,
            port: request.Port,
            databaseName: request.DatabaseName,
            schemaName: request.SchemaName,
            fileRootPath: request.FileRootPath,
            apiBaseUrl: request.ApiBaseUrl,
            secretReference: request.SecretReference,
            connectionOptionsJson: request.ConnectionOptionsJson,
            readOnlyEnforced: request.ReadOnlyEnforced ?? true,
            description: request.Description);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetConnectionProfileByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ApplicationResult<ConnectionProfileDto>> ActivateConnectionProfileAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (entity is null)
            return ApplicationResult<ConnectionProfileDto>.Failure(ApplicationError.NotFound("Connection profile not found."));

        entity.Activate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetConnectionProfileByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ApplicationResult<ConnectionProfileDto>> DeactivateConnectionProfileAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (entity is null)
            return ApplicationResult<ConnectionProfileDto>.Failure(ApplicationError.NotFound("Connection profile not found."));

        entity.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetConnectionProfileByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<ApplicationResult<DataSourceConnectionTestResult>> TestConnectionProfileAsync(
        Guid connectionProfileId,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == connectionProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
            return ApplicationResult<DataSourceConnectionTestResult>.Failure(
                ApplicationError.NotFound("Connection profile was not found."));

        try
        {
            var connector = _connectorFactory.GetConnector(profile.ProviderType);
            var result = await connector.TestConnectionAsync(profile, cancellationToken);

            profile.MarkTestResult(result.IsSuccess, result.Message);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return ApplicationResult<DataSourceConnectionTestResult>.Success(result);
        }
        catch (Exception ex)
        {
            profile.MarkTestResult(false, ex.Message);
            await _dbContext.SaveChangesAsync(cancellationToken);

           return ApplicationResult<DataSourceConnectionTestResult>.Failure(
                 ApplicationError.Validation($"Some error message: {ex.Message}"));     
        }
    }

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

    private static string NormalizeCode(string value)
    {
        var clean = new string(value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_')
            .ToArray());

        while (clean.Contains("__", StringComparison.Ordinal))
            clean = clean.Replace("__", "_", StringComparison.Ordinal);

        return clean.Trim('_');
    }


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

    public async Task<ApplicationResult<CsvSchemaDiscoveryResult>> DiscoverCsvSchemaAsync(
        Guid sourceDatasetDefinitionId,
        CsvSchemaDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var dataset = await _dbContext.SourceDatasetDefinitions
            .FirstOrDefaultAsync(x => x.Id == sourceDatasetDefinitionId && !x.IsDeleted, cancellationToken);

        if (dataset is null)
            return ApplicationResult<CsvSchemaDiscoveryResult>.Failure(ApplicationError.NotFound("Source dataset not found."));

        var profile = await _dbContext.ConnectionProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dataset.ConnectionProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
            return ApplicationResult<CsvSchemaDiscoveryResult>.Failure(ApplicationError.NotFound("Connection profile not found."));

        if (profile.ProviderType != "Csv")
            return ApplicationResult<CsvSchemaDiscoveryResult>.Failure(ApplicationError.Validation("CSV schema discovery is only available for Csv provider profiles in Phase 3."));

        var delimiter = ResolveDelimiter(request.Delimiter);
        var hasHeader = request.HasHeader ?? true;
        var maxRows = Math.Clamp(request.MaxRowsToAnalyze ?? 100, 1, 5000);

        var parsed = CsvTextParser.Parse(request.CsvText, delimiter, hasHeader, maxRows);

        if (parsed.Headers.Count == 0)
            return ApplicationResult<CsvSchemaDiscoveryResult>.Failure(ApplicationError.Validation("CSV contains no columns."));

        if (request.PersistFields)
        {
            var existingFields = await _dbContext.SourceFieldDefinitions
                .Where(x => x.SourceDatasetDefinitionId == sourceDatasetDefinitionId && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var existing in existingFields)
                existing.SoftDelete("Replaced by new CSV schema discovery.");

            var fieldEntities = BuildFieldDefinitions(
                sourceDatasetDefinitionId,
                parsed,
                dataset.IsSynthetic);

            _dbContext.SourceFieldDefinitions.AddRange(fieldEntities);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var fields = await GetFieldDtosAsync(sourceDatasetDefinitionId, cancellationToken);

        if (fields.Count == 0)
        {
            fields = BuildFieldDefinitionDtos(sourceDatasetDefinitionId, parsed);
        }

        return ApplicationResult<CsvSchemaDiscoveryResult>.Success(
            new CsvSchemaDiscoveryResult(
                sourceDatasetDefinitionId,
                dataset.DatasetCode,
                dataset.SourceObjectName,
                delimiter.ToString(),
                hasHeader,
                parsed.Rows.Count,
                fields));
    }

    public async Task<ApplicationResult<CsvPreviewResult>> PreviewCsvAsync(
        Guid sourceDatasetDefinitionId,
        CsvPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var dataset = await _dbContext.SourceDatasetDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sourceDatasetDefinitionId && !x.IsDeleted, cancellationToken);

        if (dataset is null)
            return ApplicationResult<CsvPreviewResult>.Failure(ApplicationError.NotFound("Source dataset not found."));

        var delimiter = ResolveDelimiter(request.Delimiter);
        var hasHeader = request.HasHeader ?? true;
        var maxRows = Math.Clamp(request.MaxRows ?? 25, 1, 500);

        var parsed = CsvTextParser.Parse(request.CsvText, delimiter, hasHeader, maxRows);

        return ApplicationResult<CsvPreviewResult>.Success(
            new CsvPreviewResult(
                delimiter.ToString(),
                hasHeader,
                parsed.Headers,
                parsed.Rows));
    }

    public async Task<ApplicationResult<CsvImportSnapshotResult>> ImportCsvSnapshotAsync(
        Guid sourceDatasetDefinitionId,
        CsvImportSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        var dataset = await _dbContext.SourceDatasetDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sourceDatasetDefinitionId && !x.IsDeleted, cancellationToken);

        if (dataset is null)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.NotFound("Source dataset not found."));

        var profile = await _dbContext.ConnectionProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == dataset.ConnectionProfileId && !x.IsDeleted, cancellationToken);

        if (profile is null)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.NotFound("Connection profile not found."));

        if (!profile.IsActive)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.BusinessRule("Connection profile is inactive."));

        if (profile.ProviderType != "Csv")
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.Validation("CSV snapshot import is only available for Csv provider profiles in Phase 3."));

        var sourceSystem = await _dbContext.SourceSystemDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == profile.SourceSystemDefinitionId && !x.IsDeleted, cancellationToken);

        if (sourceSystem is null)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.NotFound("Source system definition not found."));

        if (!sourceSystem.IsActive)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.BusinessRule("Source system is inactive."));

        var delimiter = ResolveDelimiter(request.Delimiter);
        var hasHeader = request.HasHeader ?? true;

        var parsed = CsvTextParser.Parse(
            request.CsvText,
            delimiter,
            hasHeader,
            maxRows: 200_000);

        if (parsed.Rows.Count == 0)
            return ApplicationResult<CsvImportSnapshotResult>.Failure(ApplicationError.Validation("CSV contains no data rows."));

        var importBatchCode = string.IsNullOrWhiteSpace(request.ImportBatchCode)
            ? $"CSV-{dataset.DatasetCode}-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : request.ImportBatchCode.Trim();

        var batch = new ImportBatch(
            sourceSystemDefinitionId: profile.SourceSystemDefinitionId,
            importBatchCode: importBatchCode,
            importType: "CsvSnapshot",
            isSynthetic: request.IsSynthetic,
            sourceObjectName: dataset.SourceObjectName,
            fileName: request.FileName,
            checksum: request.Checksum,
            sourceSystem: request.SourceSystem ?? "PlantProcessIQ.AdminCsvConnector",
            sourceRecordId: request.SourceRecordId);

        _dbContext.ImportBatches.Add(batch);
        await _dbContext.SaveChangesAsync(cancellationToken);

        batch.MarkRunning();

        var rowNumber = 1;
        var stagingRecords = parsed.Rows.Select(row =>
        {
            var rawJson = JsonSerializer.Serialize(row, JsonOptions);

            return new StagingRecord(
                importBatchId: batch.Id,
                sourceObjectName: dataset.SourceObjectName,
                rowNumber: rowNumber++,
                rawJson: rawJson,
                isSynthetic: request.IsSynthetic,
                sourceSystem: request.SourceSystem ?? "PlantProcessIQ.AdminCsvConnector",
                sourceRecordId: row.TryGetValue("SourceRecordId", out var sourceRecordId)
                    ? sourceRecordId
                    : null);
        }).ToList();

        _dbContext.StagingRecords.AddRange(stagingRecords);

        batch.MarkCompleted(stagingRecords.Count);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<CsvImportSnapshotResult>.Success(
            new CsvImportSnapshotResult(
                batch.Id,
                batch.ImportBatchCode,
                dataset.Id,
                profile.Id,
                profile.SourceSystemDefinitionId,
                dataset.SourceObjectName,
                stagingRecords.Count,
                batch.Status,
                batch.StartedAtUtc,
                batch.CompletedAtUtc));
    }

    private IQueryable<ConnectionProfileDto> GetConnectionProfileDtoQuery()
    {
        return
            from profile in _dbContext.ConnectionProfiles.AsNoTracking()
            join source in _dbContext.SourceSystemDefinitions.AsNoTracking()
                on profile.SourceSystemDefinitionId equals source.Id
            where !profile.IsDeleted && !source.IsDeleted
            select new ConnectionProfileDto(
                profile.Id,
                profile.SourceSystemDefinitionId,
                source.SourceSystemCode,
                source.SourceSystemName,
                profile.ConnectionProfileCode,
                profile.ConnectionProfileName,
                profile.ProviderType,
                profile.ConnectionMode,
                profile.HostName,
                profile.Port,
                profile.DatabaseName,
                profile.SchemaName,
                profile.FileRootPath,
                profile.ApiBaseUrl,
                profile.SecretReference,
                profile.ConnectionOptionsJson,
                profile.IsActive,
                profile.ReadOnlyEnforced,
                profile.Description,
                profile.LastTestedAtUtc,
                profile.LastTestStatus,
                profile.LastTestMessage,
                profile.IsSynthetic,
                profile.CreatedAtUtc,
                profile.UpdatedAtUtc);
    }

    private IQueryable<SourceDatasetDefinitionDto> GetDatasetDtoQuery()
    {
        return
            from dataset in _dbContext.SourceDatasetDefinitions.AsNoTracking()
            join profile in _dbContext.ConnectionProfiles.AsNoTracking()
                on dataset.ConnectionProfileId equals profile.Id
            where !dataset.IsDeleted && !profile.IsDeleted
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

    private async Task<IReadOnlyList<SourceFieldDefinitionDto>> GetFieldDtosAsync(
        Guid sourceDatasetDefinitionId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.SourceFieldDefinitions
            .AsNoTracking()
            .Where(x => x.SourceDatasetDefinitionId == sourceDatasetDefinitionId && !x.IsDeleted)
            .OrderBy(x => x.Ordinal)
            .Select(x => new SourceFieldDefinitionDto(
                x.Id,
                x.SourceDatasetDefinitionId,
                x.FieldName,
                x.DisplayName,
                x.SourceDataType,
                x.Ordinal,
                x.IsNullable,
                x.MaxLength,
                x.NumericPrecision,
                x.NumericScale,
                x.SampleValue,
                x.IsPrimaryKeyCandidate,
                x.IsTimestampCandidate,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    private static IReadOnlyList<SourceFieldDefinition> BuildFieldDefinitions(
        Guid sourceDatasetDefinitionId,
        CsvParseResult parsed,
        bool isSynthetic)
    {
        var result = new List<SourceFieldDefinition>();

        for (var index = 0; index < parsed.Headers.Count; index++)
        {
            var header = parsed.Headers[index];
            var values = parsed.Rows
                .Select(x => x.TryGetValue(header, out var value) ? value : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(100)
                .ToList();

            var inferredType = InferDataType(values);
            var sampleValue = values.FirstOrDefault();

            result.Add(new SourceFieldDefinition(
                sourceDatasetDefinitionId,
                fieldName: header,
                displayName: header,
                sourceDataType: inferredType,
                ordinal: index + 1,
                isNullable: parsed.Rows.Any(x => !x.TryGetValue(header, out var value) || string.IsNullOrWhiteSpace(value)),
                isSynthetic: isSynthetic,
                maxLength: values.Count == 0 ? null : values.Max(x => x!.Length),
                numericPrecision: inferredType == "Decimal" ? 18 : null,
                numericScale: inferredType == "Decimal" ? 6 : null,
                sampleValue: sampleValue,
                isPrimaryKeyCandidate: LooksLikeKey(header),
                isTimestampCandidate: inferredType == "DateTime" || LooksLikeTimestamp(header),
                sourceSystem: "PlantProcessIQ.CsvSchemaDiscovery"));
        }

        return result;
    }

    private static IReadOnlyList<SourceFieldDefinitionDto> BuildFieldDefinitionDtos(
        Guid sourceDatasetDefinitionId,
        CsvParseResult parsed)
    {
        var result = new List<SourceFieldDefinitionDto>();

        for (var index = 0; index < parsed.Headers.Count; index++)
        {
            var header = parsed.Headers[index];
            var values = parsed.Rows
                .Select(x => x.TryGetValue(header, out var value) ? value : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Take(100)
                .ToList();

            var inferredType = InferDataType(values);

            result.Add(new SourceFieldDefinitionDto(
                Guid.Empty,
                sourceDatasetDefinitionId,
                header,
                header,
                inferredType,
                index + 1,
                parsed.Rows.Any(x => !x.TryGetValue(header, out var value) || string.IsNullOrWhiteSpace(value)),
                values.Count == 0 ? null : values.Max(x => x!.Length),
                inferredType == "Decimal" ? 18 : null,
                inferredType == "Decimal" ? 6 : null,
                values.FirstOrDefault(),
                LooksLikeKey(header),
                inferredType == "DateTime" || LooksLikeTimestamp(header),
                true));
        }

        return result;
    }

    private static ApplicationError? ValidateConnectionProfileRequest(CreateConnectionProfileRequest request)
    {
        if (request.SourceSystemDefinitionId == Guid.Empty)
            return ApplicationError.Validation("SourceSystemDefinitionId is required.");

        if (string.IsNullOrWhiteSpace(request.ConnectionProfileCode))
            return ApplicationError.Validation("ConnectionProfileCode is required.");

        if (string.IsNullOrWhiteSpace(request.ConnectionProfileName))
            return ApplicationError.Validation("ConnectionProfileName is required.");

        if (string.IsNullOrWhiteSpace(request.ProviderType))
            return ApplicationError.Validation("ProviderType is required.");

        return null;
    }

    private static string NormalizeProviderType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().ToLowerInvariant() switch
        {
            "csv" => "Csv",
            "excel" => "Excel",
            "xlsx" => "Excel",
            "postgres" => "PostgreSql",
            "postgresql" => "PostgreSql",
            "sqlserver" => "SqlServer",
            "mssql" => "SqlServer",
            "oracle" => "Oracle",
            "api" => "RestApi",
            "restapi" => "RestApi",
            _ => value.Trim()
        };
    }

    private static char ResolveDelimiter(string? delimiter)
    {
        if (string.IsNullOrWhiteSpace(delimiter))
            return ',';

        var value = delimiter.Trim();

        return value.ToLowerInvariant() switch
        {
            "\\t" => '\t',
            "tab" => '\t',
            "semicolon" => ';',
            ";" => ';',
            "pipe" => '|',
            "|" => '|',
            "," => ',',
            "comma" => ',',
            _ => value[0]
        };
    }

    private static string InferDataType(IReadOnlyList<string?> values)
    {
        if (values.Count == 0)
            return "String";

        var nonEmpty = values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();

        if (nonEmpty.Count == 0)
            return "String";

        if (nonEmpty.All(x => bool.TryParse(x, out _)))
            return "Boolean";

        if (nonEmpty.All(x => long.TryParse(x, out _)))
            return "Integer";

        if (nonEmpty.All(x => decimal.TryParse(x, out _)))
            return "Decimal";

        if (nonEmpty.All(x => DateTime.TryParse(x, out _)))
            return "DateTime";

        return "String";
    }

    private static bool LooksLikeKey(string fieldName)
    {
        var name = fieldName.ToLowerInvariant();
        return name is "id" or "key"
            || name.EndsWith("_id")
            || name.EndsWith("id")
            || name.Contains("code")
            || name.Contains("number");
    }

    private static bool LooksLikeTimestamp(string fieldName)
    {
        var name = fieldName.ToLowerInvariant();
        return name.Contains("time")
            || name.Contains("date")
            || name.Contains("timestamp")
            || name.EndsWith("_at")
            || name.EndsWith("utc");
    }

    private sealed record CsvParseResult(
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows);

    private static class CsvTextParser
    {
        public static CsvParseResult Parse(
            string? csvText,
            char delimiter,
            bool hasHeader,
            int maxRows)
        {
            if (string.IsNullOrWhiteSpace(csvText))
                return new CsvParseResult(Array.Empty<string>(), Array.Empty<IReadOnlyDictionary<string, string?>>());

            var records = ParseRecords(csvText, delimiter)
                .Where(x => x.Count > 0 && x.Any(v => !string.IsNullOrWhiteSpace(v)))
                .Take(maxRows + 1)
                .ToList();

            if (records.Count == 0)
                return new CsvParseResult(Array.Empty<string>(), Array.Empty<IReadOnlyDictionary<string, string?>>());

            var headers = hasHeader
                ? records[0].Select(NormalizeHeader).ToList()
                : Enumerable.Range(1, records[0].Count).Select(x => $"Column{x}").ToList();

            headers = EnsureUniqueHeaders(headers);

            var dataRecords = hasHeader
                ? records.Skip(1).Take(maxRows).ToList()
                : records.Take(maxRows).ToList();

            var rows = new List<IReadOnlyDictionary<string, string?>>();

            foreach (var record in dataRecords)
            {
                var dictionary = new Dictionary<string, string?>();

                for (var i = 0; i < headers.Count; i++)
                {
                    var value = i < record.Count ? record[i] : null;
                    dictionary[headers[i]] = string.IsNullOrWhiteSpace(value)
                        ? null
                        : value;
                }

                rows.Add(dictionary);
            }

            return new CsvParseResult(headers, rows);
        }

        private static IReadOnlyList<IReadOnlyList<string>> ParseRecords(string text, char delimiter)
        {
            var records = new List<IReadOnlyList<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];

                if (current == '"')
                {
                    if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (current == delimiter && !inQuotes)
                {
                    row.Add(field.ToString().Trim());
                    field.Clear();
                    continue;
                }

                if ((current == '\r' || current == '\n') && !inQuotes)
                {
                    if (current == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    row.Add(field.ToString().Trim());
                    field.Clear();

                    records.Add(row);
                    row = new List<string>();
                    continue;
                }

                field.Append(current);
            }

            row.Add(field.ToString().Trim());

            if (row.Count > 1 || row.Any(x => !string.IsNullOrWhiteSpace(x)))
                records.Add(row);

            return records;
        }

        private static string NormalizeHeader(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Column";

            var cleaned = value.Trim();

            cleaned = string.Concat(cleaned.Select(ch =>
                char.IsLetterOrDigit(ch) || ch == '_'
                    ? ch
                    : '_'));

            while (cleaned.Contains("__", StringComparison.Ordinal))
                cleaned = cleaned.Replace("__", "_", StringComparison.Ordinal);

            cleaned = cleaned.Trim('_');

            return string.IsNullOrWhiteSpace(cleaned)
                ? "Column"
                : cleaned;
        }

        private static List<string> EnsureUniqueHeaders(IReadOnlyList<string> headers)
        {
            var result = new List<string>();
            var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var header in headers)
            {
                if (!used.TryAdd(header, 1))
                {
                    used[header]++;
                    result.Add($"{header}_{used[header]}");
                }
                else
                {
                    result.Add(header);
                }
            }

            return result;
        }
    }
}



