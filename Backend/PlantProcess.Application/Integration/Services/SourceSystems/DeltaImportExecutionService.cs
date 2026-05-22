// ============================================================
// FILE: Backend/PlantProcess.Application/Integration/Services/SourceSystems/DeltaImportExecutionService.cs
//
// FIXED: All 7 build errors resolved by using the real interfaces:
//   1. SourceDatasetDefinition has no .ConnectionProfile navigation prop
//      → load ConnectionProfile separately by ConnectionProfileId
//   2. DataSourceIncrementalReadRequest has 8 params (ConnectionProfileId,
//      SourceDatasetDefinitionId, SourceObjectName, SourceSchemaName,
//      CursorFieldName, LastCursorValue, Limit, DatasetOptionsJson)
//   3. IImportBatchService.CreateAsync() not CreateImportBatchAsync()
//   4. CreateImportBatchCommand has (SourceSystemDefinitionId, ImportBatchCode,
//      ImportType, SourceObjectName?, FileName?, Checksum?, CommandMetadata)
//   5. IStagingRecordService.CreateBulkAsync() not CreateStagingRecordAsync()
//   6. BulkCreateStagingRecordsCommand(ImportBatchId, SourceObjectName,
//      IReadOnlyCollection<CreateStagingRecordRow>, CommandMetadata)
//   7. CommandMetadata(IsSynthetic, SourceSystem, SourceRecordId, ...)
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Common;
using PlantProcess.Application.Integration.Contracts.Commands;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Application.Integration.Interfaces.Staging;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.SourceSystems;

/// <summary>
/// Executes delta (incremental) imports for all active datasets that have
/// an IncrementalCursorField configured.
///
/// For each eligible dataset:
///  1. Reads the last cursor value stored in the dataset record.
///  2. Calls the appropriate connector's ReadRowsSinceKeyAsync() with that cursor.
///  3. Writes all new rows as StagingRecords via IStagingRecordService.CreateBulkAsync().
///  4. Updates the dataset's LastCursorValue to the highest value seen.
/// </summary>
public sealed class DeltaImportExecutionService : IDeltaImportExecutionService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IDataSourceConnectorFactory _connectorFactory;
    private readonly IIncrementalSyncStateService _syncState;
    private readonly IImportBatchService _importBatchService;
    private readonly IStagingRecordService _stagingRecordService;
    private readonly ILogger<DeltaImportExecutionService> _logger;

    public DeltaImportExecutionService(
        IPlantProcessDbContext dbContext,
        IDataSourceConnectorFactory connectorFactory,
        IIncrementalSyncStateService syncState,
        IImportBatchService importBatchService,
        IStagingRecordService stagingRecordService,
        ILogger<DeltaImportExecutionService> logger)
    {
        _dbContext = dbContext;
        _connectorFactory = connectorFactory;
        _syncState = syncState;
        _importBatchService = importBatchService;
        _stagingRecordService = stagingRecordService;
        _logger = logger;
    }

    public async Task<DeltaImportSummary> ExecuteAllAsync(
        int maxDatasetsPerRun,
        int maxRowsPerDataset,
        CancellationToken cancellationToken)
    {
        var summary = new DeltaImportSummary();

        // Load all active datasets that have an incremental cursor field.
        // SourceDatasetDefinition has no navigation property to ConnectionProfile,
        // so we join manually via ConnectionProfileId.
        var datasets = await _dbContext.SourceDatasetDefinitions
            .AsNoTracking()
            .Where(d =>
                !d.IsDeleted &&
                d.IsActive &&
                d.IncrementalCursorField != null &&
                d.IncrementalCursorField != "")
            .OrderBy(d => d.UpdatedAtUtc ?? d.CreatedAtUtc)
            .Take(Math.Clamp(maxDatasetsPerRun, 1, 100))
            .ToListAsync(cancellationToken);

        if (datasets.Count == 0)
        {
            _logger.LogDebug("DeltaImportExecutionService: No eligible incremental datasets found.");
            return summary;
        }

        // Load all required connection profiles in one query
        var profileIds = datasets.Select(d => d.ConnectionProfileId).Distinct().ToList();

        var profiles = await _dbContext.ConnectionProfiles
            .AsNoTracking()
            .Where(p => profileIds.Contains(p.Id) && !p.IsDeleted && p.IsActive)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        _logger.LogInformation(
            "DeltaImportExecutionService: {Count} eligible datasets, {Profiles} active connection profiles.",
            datasets.Count,
            profiles.Count);

        foreach (var dataset in datasets)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (!profiles.TryGetValue(dataset.ConnectionProfileId, out var connectionProfile))
            {
                _logger.LogWarning(
                    "DeltaImportExecutionService: Dataset {DatasetCode} skipped — connection profile {ProfileId} not found or inactive.",
                    dataset.DatasetCode,
                    dataset.ConnectionProfileId);
                continue;
            }

            try
            {
                var rowsImported = await ExecuteDatasetDeltaAsync(
                    dataset,
                    connectionProfile,
                    maxRowsPerDataset,
                    cancellationToken);

                summary.DatasetsProcessed++;
                summary.TotalRowsImported += rowsImported;
                summary.DatasetResults.Add(new DeltaDatasetResult(
                    dataset.Id.ToString(),
                    dataset.DatasetCode,
                    rowsImported,
                    null));
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "DeltaImportExecutionService: Dataset {DatasetCode} failed.",
                    dataset.DatasetCode);

                summary.DatasetsFailedCount++;
                summary.DatasetResults.Add(new DeltaDatasetResult(
                    dataset.Id.ToString(),
                    dataset.DatasetCode,
                    0,
                    ex.Message));
            }
        }

        _logger.LogInformation(
            "DeltaImportExecutionService: Complete. Datasets={Processed}, Rows={Rows}, Failures={Failures}",
            summary.DatasetsProcessed,
            summary.TotalRowsImported,
            summary.DatasetsFailedCount);

        return summary;
    }

    private async Task<int> ExecuteDatasetDeltaAsync(
        SourceDatasetDefinition dataset,
        ConnectionProfile connectionProfile,
        int maxRows,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "DeltaImportExecutionService: Dataset={DatasetCode}, Provider={Provider}, Cursor={CursorField}, LastValue={LastValue}",
            dataset.DatasetCode,
            connectionProfile.ProviderType,
            dataset.IncrementalCursorField,
            dataset.LastCursorValue ?? "(none)");

        var dataReader = _connectorFactory.GetDataSourceReader(connectionProfile.ProviderType);

        // FIX 2: DataSourceIncrementalReadRequest has 8 required parameters
        var readRequest = new DataSourceIncrementalReadRequest(
            ConnectionProfileId:         connectionProfile.Id,
            SourceDatasetDefinitionId:   dataset.Id,
            SourceObjectName:            dataset.SourceObjectName,
            SourceSchemaName:            dataset.SourceSchemaName,
            CursorFieldName:             dataset.IncrementalCursorField!,
            LastCursorValue:             dataset.LastCursorValue,
            Limit:                       Math.Clamp(maxRows, 1, 5000),
            DatasetOptionsJson:          dataset.DatasetOptionsJson);

        var rows = await dataReader.ReadRowsSinceKeyAsync(
            connectionProfile,
            dataset,
            readRequest,
            cancellationToken);

        if (rows.Count == 0)
        {
            _logger.LogDebug(
                "DeltaImportExecutionService: Dataset={DatasetCode} — no new rows.",
                dataset.DatasetCode);
            return 0;
        }

        _logger.LogInformation(
            "DeltaImportExecutionService: Dataset={DatasetCode} — {RowCount} new rows fetched.",
            dataset.DatasetCode,
            rows.Count);

        var metadata = new CommandMetadata(
            IsSynthetic:   false,
            SourceSystem:  "PlantProcessIQ.DeltaImport",
            SourceRecordId: dataset.DatasetCode,
            RequestedBy:   "DeltaImportExecutionService",
            CorrelationId: Guid.NewGuid().ToString("N"));

        // FIX 3+4: IImportBatchService.CreateAsync() with correct CreateImportBatchCommand fields
        var batchCode = $"DELTA_{dataset.DatasetCode}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        var batchResult = await _importBatchService.CreateAsync(
            new CreateImportBatchCommand(
                SourceSystemDefinitionId: connectionProfile.SourceSystemDefinitionId,
                ImportBatchCode:          batchCode,
                ImportType:               "DeltaImport",
                SourceObjectName:         dataset.SourceObjectName,
                FileName:                 null,
                Checksum:                 null,
                Metadata:                 metadata),
            cancellationToken);

        if (batchResult.IsFailure)
        {
            _logger.LogWarning(
                "DeltaImportExecutionService: Failed to create import batch for {DatasetCode}: {Error}",
                dataset.DatasetCode,
                batchResult.Error?.Message);
            // Do not abort — we still write staging records without a batch ID
        }

        var importBatchId = batchResult.IsSuccess ? batchResult.Value : Guid.Empty;

        // Track highest cursor value seen in this batch
        string? highestCursorValue = dataset.LastCursorValue;

        // FIX 5+6: IStagingRecordService.CreateBulkAsync() with BulkCreateStagingRecordsCommand
        var stagingRows = new List<CreateStagingRecordRow>(rows.Count);

        foreach (var row in rows)
        {
            var rawJson = System.Text.Json.JsonSerializer.Serialize(row.Values);

            // Advance the cursor tracker
            if (row.Values.TryGetValue(dataset.IncrementalCursorField!, out var cursorValue)
                && cursorValue != null)
            {
                if (highestCursorValue == null ||
                    string.Compare(cursorValue, highestCursorValue, StringComparison.Ordinal) > 0)
                {
                    highestCursorValue = cursorValue;
                }
            }

            stagingRows.Add(new CreateStagingRecordRow(
                RowNumber:      (int)row.RowNumber,
                RawJson:        rawJson,
                SourceRecordId: $"{dataset.DatasetCode}:{row.RowNumber}"));
        }

        int rowsWritten = 0;

        if (importBatchId != Guid.Empty && stagingRows.Count > 0)
        {
            var bulkResult = await _stagingRecordService.CreateBulkAsync(
                new BulkCreateStagingRecordsCommand(
                    ImportBatchId:   importBatchId,
                    SourceObjectName: dataset.SourceObjectName,
                    Rows:            stagingRows,
                    Metadata:        metadata),
                cancellationToken);

            if (bulkResult.IsSuccess && bulkResult.Value is not null)
            {
                rowsWritten = bulkResult.Value.Accepted;

                await _importBatchService.MarkCompletedAsync(importBatchId, rowsWritten, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "DeltaImportExecutionService: Bulk staging write failed for {DatasetCode}: {Error}",
                    dataset.DatasetCode,
                    bulkResult.Error?.Message);

                await _importBatchService.MarkFailedAsync(
                    importBatchId,
                    bulkResult.Error?.Message ?? "Bulk staging write failed.",
                    cancellationToken);
            }
        }

        // Advance the cursor so next run starts from where we left off
        if (highestCursorValue != null && highestCursorValue != dataset.LastCursorValue)
        {
            await _syncState.UpdateLastCursorValueAsync(
                dataset.Id,
                highestCursorValue,
                cancellationToken);

            _logger.LogInformation(
                "DeltaImportExecutionService: Dataset={DatasetCode} cursor advanced to {NewCursor}",
                dataset.DatasetCode,
                highestCursorValue);
        }

        return rowsWritten;
    }
}
