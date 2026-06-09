using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Integration.Services.SourceSystems;

/// <summary>
/// T-024: throttled, resumable, checkpointed historical backfill for one source
/// dataset. Drives the connector's bounded incremental read in a loop, stages each
/// batch, advances and persists the watermark after every batch (so an interrupted
/// run resumes from the checkpoint with no duplicate rows), and paces itself to a
/// configured source-impact budget (max rows/sec). Progress is recorded in
/// JobRunHistory for the Jobs Monitor.
/// </summary>
public interface IBackfillExecutionService
{
    Task<BackfillSummary> BackfillDatasetAsync(BackfillRequest request, CancellationToken cancellationToken);
}

public sealed record BackfillRequest(
    Guid DatasetId,
    Guid JobDefinitionId,
    int BatchSize,
    int MaxRowsPerSecond,
    long MaxTotalRows,
    string TriggerSource,
    string? TriggeredBy);

public sealed record BackfillSummary
{
    public string DatasetCode { get; set; } = string.Empty;
    public int BatchesProcessed { get; set; }
    public long RowsImported { get; set; }
    public bool Exhausted { get; set; }
    public bool Cancelled { get; set; }
    public string? LastCursorValue { get; set; }
    public double EffectiveRowsPerSecond { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class BackfillExecutionService : IBackfillExecutionService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IDataSourceConnectorFactory _connectorFactory;
    private readonly ILogger<BackfillExecutionService> _logger;

    public BackfillExecutionService(
        IPlantProcessDbContext dbContext,
        IDataSourceConnectorFactory connectorFactory,
        ILogger<BackfillExecutionService> logger)
    {
        _dbContext = dbContext;
        _connectorFactory = connectorFactory;
        _logger = logger;
    }

    public async Task<BackfillSummary> BackfillDatasetAsync(
        BackfillRequest request,
        CancellationToken cancellationToken)
    {
        var batchSize = Math.Clamp(request.BatchSize <= 0 ? 500 : request.BatchSize, 1, 50_000);
        var maxRowsPerSecond = Math.Clamp(request.MaxRowsPerSecond <= 0 ? 1000 : request.MaxRowsPerSecond, 1, 5_000_000);
        var maxTotalRows = request.MaxTotalRows <= 0 ? long.MaxValue : request.MaxTotalRows;

        var summary = new BackfillSummary();

        var dataset = await _dbContext.SourceDatasetDefinitions
            .FirstOrDefaultAsync(x => x.Id == request.DatasetId, cancellationToken);
        if (dataset is null)
        {
            summary.ErrorMessage = "Dataset not found.";
            return summary;
        }

        summary.DatasetCode = dataset.DatasetCode;

        var profile = await _dbContext.ConnectionProfiles
            .FirstOrDefaultAsync(x => x.Id == dataset.ConnectionProfileId, cancellationToken);
        if (profile is null || !profile.IsActive)
        {
            summary.ErrorMessage = "Connection profile not found or inactive.";
            return summary;
        }

        var sourceSystem = await _dbContext.SourceSystemDefinitions
            .FirstOrDefaultAsync(x => x.Id == profile.SourceSystemDefinitionId, cancellationToken);
        if (sourceSystem is null)
        {
            summary.ErrorMessage = "Source system definition not found.";
            return summary;
        }

        var cursorFieldName =
            !string.IsNullOrWhiteSpace(dataset.IncrementalCursorField)
                ? dataset.IncrementalCursorField
                : dataset.PrimaryTimestampField;

        if (string.IsNullOrWhiteSpace(cursorFieldName))
        {
            summary.ErrorMessage = "Backfill requires an incremental cursor or primary timestamp field on the dataset.";
            return summary;
        }

        // Record the run for the Jobs Monitor (only when a job definition is supplied).
        JobRunHistory? run = null;
        if (request.JobDefinitionId != Guid.Empty)
        {
            run = new JobRunHistory(
                jobDefinitionId: request.JobDefinitionId,
                jobCode: ("BACKFILL_" + dataset.DatasetCode).ToUpperInvariant(),
                jobName: "Historical backfill: " + dataset.DatasetCode,
                jobType: JobDefinitionType.DbLinkImport,
                triggerSource: string.IsNullOrWhiteSpace(request.TriggerSource) ? "BackfillExecutionService" : request.TriggerSource,
                triggeredBy: request.TriggeredBy,
                correlationId: null,
                isSynthetic: false,
                sourceSystem: profile.ProviderType,
                sourceRecordId: dataset.Id.ToString());

            _dbContext.JobRunHistories.Add(run);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var reader = _connectorFactory.GetDataSourceReader(profile.ProviderType);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    summary.Cancelled = true;
                    break;
                }

                var remaining = maxTotalRows - summary.RowsImported;
                if (remaining <= 0)
                {
                    break;
                }

                var batchLimit = (int)Math.Min(batchSize, remaining);

                var readRequest = new DataSourceIncrementalReadRequest(
                    dataset.ConnectionProfileId,
                    dataset.Id,
                    dataset.SourceObjectName,
                    dataset.SourceSchemaName,
                    cursorFieldName,
                    dataset.LastCursorValue,
                    batchLimit,
                    dataset.DatasetOptionsJson);

                var rows = await reader.ReadRowsSinceKeyAsync(profile, dataset, readRequest, cancellationToken);

                if (rows.Count == 0)
                {
                    summary.Exhausted = true;
                    break;
                }

                var batchCode =
                    ("BACKFILL_" + dataset.DatasetCode + "_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture))
                        .ToUpperInvariant();

                var batch = new ImportBatch(
                    sourceSystemDefinitionId: sourceSystem.Id,
                    importBatchCode: batchCode,
                    importType: profile.ProviderType,
                    isSynthetic: false,
                    sourceObjectName: dataset.SourceObjectName,
                    fileName: null,
                    checksum: null,
                    sourceSystem: "BackfillExecutionService",
                    sourceRecordId: dataset.Id.ToString());

                batch.MarkRunning();
                _dbContext.ImportBatches.Add(batch);

                var rowNumber = 1;
                var maxCursorValue = dataset.LastCursorValue;

                foreach (var row in rows)
                {
                    var rawJson = JsonSerializer.Serialize(row.Values);
                    var sourceRecordId = TryGetSourceRecordId(row, cursorFieldName, rowNumber);

                    _dbContext.StagingRecords.Add(new StagingRecord(
                        importBatchId: batch.Id,
                        sourceObjectName: dataset.SourceObjectName,
                        rowNumber: rowNumber,
                        rawJson: rawJson,
                        isSynthetic: false,
                        sourceSystem: profile.ProviderType,
                        sourceRecordId: sourceRecordId));

                    var candidateCursor = TryGetCursorValue(row, cursorFieldName);
                    if (!string.IsNullOrWhiteSpace(candidateCursor))
                    {
                        maxCursorValue = MaxCursor(maxCursorValue, candidateCursor);
                    }

                    rowNumber++;
                }

                batch.MarkCompleted(rows.Count);

                // Checkpoint: advance + persist the watermark so an interruption resumes here.
                if (!string.IsNullOrWhiteSpace(maxCursorValue) &&
                    !string.Equals(maxCursorValue, dataset.LastCursorValue, StringComparison.Ordinal))
                {
                    dataset.UpdateLastCursorValue(maxCursorValue);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);

                summary.BatchesProcessed++;
                summary.RowsImported += rows.Count;
                summary.LastCursorValue = dataset.LastCursorValue;

                // Fewer rows than asked => history is drained.
                if (rows.Count < batchLimit)
                {
                    summary.Exhausted = true;
                    break;
                }

                // Throttle to the configured source-impact budget (cumulative rows/sec).
                var elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);
                if (summary.RowsImported > maxRowsPerSecond * elapsedSeconds)
                {
                    var targetSeconds = (double)summary.RowsImported / maxRowsPerSecond;
                    var delayMs = (int)Math.Clamp((targetSeconds - elapsedSeconds) * 1000, 0, 60_000);
                    if (delayMs > 0)
                    {
                        try
                        {
                            await Task.Delay(delayMs, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            summary.Cancelled = true;
                            break;
                        }
                    }
                }
            }

            stopwatch.Stop();
            summary.EffectiveRowsPerSecond = summary.RowsImported / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);

            var state = summary.Exhausted ? "completed" : (summary.Cancelled ? "paused" : "stopped");
            run?.MarkSucceeded(
                "Backfill " + state + ": " + summary.RowsImported + " rows in " + summary.BatchesProcessed + " batches.",
                JsonSerializer.Serialize(summary));

            if (run is not null)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "Backfill {State} for dataset {DatasetCode}. Rows={Rows}, Batches={Batches}, Rate={Rate:0.0}/s",
                state,
                dataset.DatasetCode,
                summary.RowsImported,
                summary.BatchesProcessed,
                summary.EffectiveRowsPerSecond);

            return summary;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            summary.ErrorMessage = ex.Message;
            summary.EffectiveRowsPerSecond = summary.RowsImported / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.001);

            _logger.LogError(ex, "Backfill failed for dataset {DatasetCode}", dataset.DatasetCode);

            run?.MarkFailed(ex.Message, JsonSerializer.Serialize(summary));
            if (run is not null)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return summary;
        }
    }

    private static string TryGetSourceRecordId(DataSourceRow row, string? cursorFieldName, int rowNumber)
    {
        var cursor = TryGetCursorValue(row, cursorFieldName);
        return !string.IsNullOrWhiteSpace(cursor)
            ? cursor
            : rowNumber.ToString(CultureInfo.InvariantCulture);
    }

    private static string? TryGetCursorValue(DataSourceRow row, string? cursorFieldName)
    {
        if (string.IsNullOrWhiteSpace(cursorFieldName))
        {
            return null;
        }

        return row.Values.TryGetValue(cursorFieldName, out var value) ? value : null;
    }

    private static string? MaxCursor(string? current, string candidate)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return candidate;
        }

        if (decimal.TryParse(current, NumberStyles.Any, CultureInfo.InvariantCulture, out var currentNumber) &&
            decimal.TryParse(candidate, NumberStyles.Any, CultureInfo.InvariantCulture, out var candidateNumber))
        {
            return candidateNumber > currentNumber ? candidate : current;
        }

        if (DateTime.TryParse(current, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var currentDate) &&
            DateTime.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var candidateDate))
        {
            return candidateDate > currentDate ? candidate : current;
        }

        return string.CompareOrdinal(candidate, current) > 0 ? candidate : current;
    }
}