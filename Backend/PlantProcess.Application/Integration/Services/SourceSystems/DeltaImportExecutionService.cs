// ============================================================
// FILE: Backend/PlantProcess.Application/Integration/Services/SourceSystems/DeltaImportExecutionService.cs
//
// CORRECTED v2 — matches the real types in the codebase:
//   - DeltaImportSummary has: DatasetsProcessed, TotalRowsImported,
//     DatasetsFailedCount, DatasetResults  (NO StartedAtUtc/CompletedAtUtc/
//     DatasetsSkippedCount/DatasetsSucceededCount)
//   - DeltaDatasetResult is a sealed record (NOT DeltaDatasetRunResult)
//     with positional ctor (DatasetId, DatasetCode, RowsImported, ErrorMessage)
//   - SourceDatasetDefinition has NO ConnectionProfile navigation property —
//     load ConnectionProfiles separately by ConnectionProfileId
//
// BE-FIX-001 changes (the only new behaviour vs the original):
//   1. WHERE clause filters on NextRunAtUtc <= now OR NULL
//   2. ORDER BY adds NextRunAtUtc first
//   3. After each dataset, call ScheduleNextRunAfterSuccess() or
//      ScheduleNextRunAfterFailure() on the TRACKED entity
//   4. SaveChangesAsync at the end to persist NextRunAtUtc updates
//
// IMPORTANT: removed .AsNoTracking() because we now mutate datasets.
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
        var nowUtc = DateTime.UtcNow;

        // ─────────────────────────────────────────────────────
        // BE-FIX-001: select only datasets that are DUE.
        // Tracked query (NO AsNoTracking) because we mutate NextRunAtUtc
        // on each dataset and SaveChangesAsync at the end.
        // ─────────────────────────────────────────────────────
        var datasets = await _dbContext.SourceDatasetDefinitions
            .Where(d =>
                !d.IsDeleted &&
                d.IsActive &&
                d.IncrementalCursorField != null &&
                d.IncrementalCursorField != "" &&
                (d.NextRunAtUtc == null || d.NextRunAtUtc <= nowUtc))
            .OrderBy(d => d.NextRunAtUtc ?? DateTime.MinValue)
            .ThenBy(d => d.UpdatedAtUtc ?? d.CreatedAtUtc)
            .Take(Math.Clamp(maxDatasetsPerRun, 1, 100))
            .ToListAsync(cancellationToken);

        if (datasets.Count == 0)
        {
            _logger.LogDebug(
                "DeltaImportExecutionService: no datasets due for import at {NowUtc:O}.",
                nowUtc);
            return summary;
        }

        // SourceDatasetDefinition has no navigation property to ConnectionProfile,
        // so join manually via ConnectionProfileId. AsNoTracking here is fine
        // because we never mutate the profile.
        var profileIds = datasets.Select(d => d.ConnectionProfileId).Distinct().ToList();

        var profiles = await _dbContext.ConnectionProfiles
            .AsNoTracking()
            .Where(p => profileIds.Contains(p.Id) && !p.IsDeleted && p.IsActive)
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        _logger.LogInformation(
            "DeltaImportExecutionService: {Count} due datasets, {Profiles} active profiles, at {NowUtc:O}.",
            datasets.Count, profiles.Count, nowUtc);

        // ─────────────────────────────────────────────────────
        // Process each dataset sequentially.
        // ─────────────────────────────────────────────────────
        foreach (var dataset in datasets)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (!profiles.TryGetValue(dataset.ConnectionProfileId, out var connectionProfile))
            {
                _logger.LogWarning(
                    "DeltaImportExecutionService: Dataset {DatasetCode} skipped — connection profile {ProfileId} not found or inactive.",
                    dataset.DatasetCode,
                    dataset.ConnectionProfileId);

                // Defer this dataset by the normal interval so we don't keep
                // re-evaluating it every tick while its profile is inactive.
                dataset.ScheduleNextRunAfterSuccess();
                continue;
            }

            try
            {
                var rowsImported = await ExecuteDatasetDeltaAsync(
                    dataset,
                    connectionProfile,
                    maxRowsPerDataset,
                    cancellationToken);

                // BE-FIX-001: success → advance by configured interval
                dataset.ScheduleNextRunAfterSuccess();

                summary.DatasetsProcessed++;
                summary.TotalRowsImported += rowsImported;
                summary.DatasetResults.Add(new DeltaDatasetResult(
                    dataset.Id.ToString(),
                    dataset.DatasetCode,
                    rowsImported,
                    null));

                _logger.LogInformation(
                    "DeltaImportExecutionService: dataset {DatasetCode} ok, {Rows} rows, next run at {NextRun:O}.",
                    dataset.DatasetCode, rowsImported, dataset.NextRunAtUtc);
            }
            catch (OperationCanceledException)
            {
                // App is shutting down — do not advance the schedule.
                throw;
            }
            catch (Exception ex)
            {
                // BE-FIX-001: failure → 2× back-off
                dataset.ScheduleNextRunAfterFailure();

                _logger.LogError(ex,
                    "DeltaImportExecutionService: dataset {DatasetCode} failed; next attempt at {NextRun:O}.",
                    dataset.DatasetCode, dataset.NextRunAtUtc);

                summary.DatasetsFailedCount++;
                summary.DatasetResults.Add(new DeltaDatasetResult(
                    dataset.Id.ToString(),
                    dataset.DatasetCode,
                    0,
                    ex.Message));
            }
        }

        // BE-FIX-001: persist all NextRunAtUtc updates in one round-trip
        await _dbContext.SaveChangesAsync(cancellationToken);

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

        var readRequest = new DataSourceIncrementalReadRequest(
            ConnectionProfileId:       connectionProfile.Id,
            SourceDatasetDefinitionId: dataset.Id,
            SourceObjectName:          dataset.SourceObjectName,
            SourceSchemaName:          dataset.SourceSchemaName,
            CursorFieldName:           dataset.IncrementalCursorField!,
            LastCursorValue:           dataset.LastCursorValue,
            Limit:                     Math.Clamp(maxRows, 1, 5000),
            DatasetOptionsJson:        dataset.DatasetOptionsJson);

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
            IsSynthetic:    false,
            SourceSystem:   "PlantProcessIQ.DeltaImport",
            SourceRecordId: dataset.DatasetCode,
            RequestedBy:    "DeltaImportExecutionService",
            CorrelationId:  Guid.NewGuid().ToString("N"));

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
        }

        var importBatchId = batchResult.IsSuccess ? batchResult.Value : Guid.Empty;

        // Track highest cursor value seen in this batch
        string? highestCursorValue = dataset.LastCursorValue;

        var stagingRows = new List<CreateStagingRecordRow>(rows.Count);

        foreach (var row in rows)
        {
            var rawJson = System.Text.Json.JsonSerializer.Serialize(row.Values);

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
                    ImportBatchId:    importBatchId,
                    SourceObjectName: dataset.SourceObjectName,
                    Rows:             stagingRows,
                    Metadata:         metadata),
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