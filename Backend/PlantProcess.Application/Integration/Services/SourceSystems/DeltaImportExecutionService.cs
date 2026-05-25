using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Services.SourceSystems;

public sealed class DeltaImportExecutionService : IDeltaImportExecutionService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IDataSourceConnectorFactory _connectorFactory;
    private readonly ILogger<DeltaImportExecutionService> _logger;

    public DeltaImportExecutionService(
        IPlantProcessDbContext dbContext,
        IDataSourceConnectorFactory connectorFactory,
        ILogger<DeltaImportExecutionService> logger)
    {
        _dbContext = dbContext;
        _connectorFactory = connectorFactory;
        _logger = logger;
    }

    public async Task<DeltaImportSummary> ExecuteAllAsync(
        int maxDatasetsPerRun,
        int maxRowsPerDataset,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        maxDatasetsPerRun = Math.Clamp(maxDatasetsPerRun, 1, 200);
        maxRowsPerDataset = Math.Clamp(maxRowsPerDataset, 1, 50_000);

        var datasets = await _dbContext.SourceDatasetDefinitions
            .Where(x =>
                x.IsActive &&
                (x.NextRunAtUtc == null || x.NextRunAtUtc <= now))
            .OrderBy(x => x.NextRunAtUtc ?? DateTime.MinValue)
            .ThenBy(x => x.DatasetCode)
            .Take(maxDatasetsPerRun)
            .ToListAsync(cancellationToken);

        var summary = new DeltaImportSummary();

        foreach (var dataset in datasets)
        {
            var result = await ExecuteSingleDatasetAsync(
                dataset,
                maxRowsPerDataset,
                cancellationToken);

            summary.DatasetsProcessed++;
            summary.TotalRowsImported += result.RowsImported;

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                summary.DatasetsFailedCount++;

            summary.DatasetResults.Add(result);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return summary;
    }

    private async Task<DeltaDatasetResult> ExecuteSingleDatasetAsync(
        SourceDatasetDefinition dataset,
        int maxRows,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _dbContext.ConnectionProfiles
                .FirstOrDefaultAsync(x => x.Id == dataset.ConnectionProfileId, cancellationToken);

            if (profile is null)
            {
                dataset.ScheduleNextRunAfterFailure();
                return new DeltaDatasetResult(
                    dataset.Id.ToString(),
                    dataset.DatasetCode,
                    0,
                    "Connection profile not found.");
            }

            if (!profile.IsActive)
            {
                dataset.ScheduleNextRunAfterFailure();
                return new DeltaDatasetResult(
                    dataset.Id.ToString(),
                    dataset.DatasetCode,
                    0,
                    "Connection profile is inactive.");
            }

            var sourceSystem = await _dbContext.SourceSystemDefinitions
                .FirstOrDefaultAsync(x => x.Id == profile.SourceSystemDefinitionId, cancellationToken);

            if (sourceSystem is null)
            {
                dataset.ScheduleNextRunAfterFailure();
                return new DeltaDatasetResult(
                    dataset.Id.ToString(),
                    dataset.DatasetCode,
                    0,
                    "Source system definition not found.");
            }

            var reader = _connectorFactory.GetDataSourceReader(profile.ProviderType);

            var effectiveCursorFieldName =
                !string.IsNullOrWhiteSpace(dataset.IncrementalCursorField)
                    ? dataset.IncrementalCursorField
                    : dataset.PrimaryTimestampField;

            IReadOnlyList<DataSourceRow> rows;

            if (!string.IsNullOrWhiteSpace(effectiveCursorFieldName))
            {
                var request = new DataSourceIncrementalReadRequest(
                    dataset.ConnectionProfileId,
                    dataset.Id,
                    dataset.SourceObjectName,
                    dataset.SourceSchemaName,
                    effectiveCursorFieldName,
                    dataset.LastCursorValue,
                    maxRows,
                    dataset.DatasetOptionsJson);

                rows = await reader.ReadRowsSinceKeyAsync(
                    profile,
                    dataset,
                    request,
                    cancellationToken);
            }
            else
            {
                rows = await reader.ReadRowsAsync(
                    profile,
                    dataset,
                    new DataSourceReadRequest(
                        dataset.ConnectionProfileId,
                        dataset.Id,
                        dataset.SourceObjectName,
                        dataset.SourceSchemaName,
                        maxRows,
                        dataset.DatasetOptionsJson),
                    cancellationToken);
            }

            var batchCode =
                $"DELTA_{dataset.DatasetCode}_{DateTime.UtcNow:yyyyMMddHHmmssfff}"
                    .ToUpperInvariant();

            var batch = new ImportBatch(
                sourceSystemDefinitionId: sourceSystem.Id,
                importBatchCode: batchCode,
                importType: profile.ProviderType,
                isSynthetic: false,
                sourceObjectName: dataset.SourceObjectName,
                fileName: null,
                checksum: null,
                sourceSystem: "DeltaImportExecutionService",
                sourceRecordId: dataset.Id.ToString());

            batch.MarkRunning();

            _dbContext.ImportBatches.Add(batch);

            var rowNumber = 1;
            string? maxCursorValue = dataset.LastCursorValue;

            foreach (var row in rows)
            {
                var rawJson = JsonSerializer.Serialize(row.Values);

                var sourceRecordId = TryGetSourceRecordId(
                    row,
                    effectiveCursorFieldName,
                    rowNumber);

                _dbContext.StagingRecords.Add(new StagingRecord(
                    importBatchId: batch.Id,
                    sourceObjectName: dataset.SourceObjectName,
                    rowNumber: rowNumber,
                    rawJson: rawJson,
                    isSynthetic: false,
                    sourceSystem: profile.ProviderType,
                    sourceRecordId: sourceRecordId));

                var candidateCursor = TryGetCursorValue(row, effectiveCursorFieldName);
                if (!string.IsNullOrWhiteSpace(candidateCursor))
                    maxCursorValue = MaxCursor(maxCursorValue, candidateCursor);

                rowNumber++;
            }

            batch.MarkCompleted(rows.Count);

            if (rows.Count > 0)
                dataset.UpdateLastCursorValue(maxCursorValue);

            dataset.ScheduleNextRunAfterSuccess();

            _logger.LogInformation(
                "Delta import completed for dataset {DatasetCode}. Rows={Rows}, NextRunAtUtc={NextRunAtUtc}",
                dataset.DatasetCode,
                rows.Count,
                dataset.NextRunAtUtc);

            return new DeltaDatasetResult(
                dataset.Id.ToString(),
                dataset.DatasetCode,
                rows.Count,
                null);
        }
        catch (Exception ex)
        {
            dataset.ScheduleNextRunAfterFailure();

            _logger.LogError(
                ex,
                "Delta import failed for dataset {DatasetCode}",
                dataset.DatasetCode);

            return new DeltaDatasetResult(
                dataset.Id.ToString(),
                dataset.DatasetCode,
                0,
                ex.Message);
        }
    }

    private static string TryGetSourceRecordId(
        DataSourceRow row,
        string? cursorFieldName,
        int rowNumber)
    {
        var cursor = TryGetCursorValue(row, cursorFieldName);

        return !string.IsNullOrWhiteSpace(cursor)
            ? cursor
            : rowNumber.ToString();
    }

    private static string? TryGetCursorValue(
        DataSourceRow row,
        string? cursorFieldName)
    {
        if (string.IsNullOrWhiteSpace(cursorFieldName))
            return null;

        if (!row.Values.TryGetValue(cursorFieldName, out var value))
            return null;

        return value?.ToString();
    }

    private static string? MaxCursor(string? current, string candidate)
    {
        if (string.IsNullOrWhiteSpace(current))
            return candidate;

        if (decimal.TryParse(current, out var currentNumber) &&
            decimal.TryParse(candidate, out var candidateNumber))
        {
            return candidateNumber > currentNumber ? candidate : current;
        }

        if (DateTime.TryParse(current, out var currentDate) &&
            DateTime.TryParse(candidate, out var candidateDate))
        {
            return candidateDate > currentDate ? candidate : current;
        }

        return string.Compare(candidate, current, StringComparison.OrdinalIgnoreCase) > 0
            ? candidate
            : current;
    }
}