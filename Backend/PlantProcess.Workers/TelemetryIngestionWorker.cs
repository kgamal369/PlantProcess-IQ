using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlantProcess.Infrastructure.Bulk;

namespace PlantProcess.Workers;

/// <summary>
/// Background worker for high-frequency parameter observation ingestion.
///
/// Important:
/// - This worker does not define ITelemetryBulkRepository or TelemetryBulkRepository locally.
/// - The real bulk repository belongs to PlantProcess.Infrastructure.Bulk.
/// - This worker only consumes buffered telemetry rows and flushes them using the Infrastructure repository.
/// </summary>
public sealed class TelemetryIngestionWorker : BackgroundService
{
    private readonly ITelemetryChannelBuffer _channelBuffer;
    private readonly ITelemetryBulkRepository _bulkRepository;
    private readonly ILogger<TelemetryIngestionWorker> _logger;

    private const int BatchSizeLimit = 5_000;
    private const int FlushIntervalMilliseconds = 1_000;

    public TelemetryIngestionWorker(
        ITelemetryChannelBuffer channelBuffer,
        ITelemetryBulkRepository bulkRepository,
        ILogger<TelemetryIngestionWorker> logger)
    {
        _channelBuffer = channelBuffer;
        _bulkRepository = bulkRepository;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "High-frequency telemetry ingestion worker started. BatchSizeLimit={BatchSizeLimit}, FlushIntervalMilliseconds={FlushIntervalMilliseconds}",
            BatchSizeLimit,
            FlushIntervalMilliseconds);

        var batch = new List<ParameterObservationInsertRow>(BatchSizeLimit);
        var stopwatch = new Stopwatch();

        try
        {
            await foreach (var observation in _channelBuffer.ConsumeAllAsync(stoppingToken))
            {
                if (batch.Count == 0)
                {
                    stopwatch.Restart();
                }

                batch.Add(observation);

                if (batch.Count >= BatchSizeLimit ||
                    stopwatch.ElapsedMilliseconds >= FlushIntervalMilliseconds)
                {
                    await FlushBatchAsync(batch, stopwatch, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Telemetry ingestion worker cancellation requested.");

            if (batch.Count > 0)
            {
                await FlushBatchAsync(batch, stopwatch, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telemetry ingestion worker failed unexpectedly.");
            throw;
        }
        finally
        {
            _logger.LogInformation("High-frequency telemetry ingestion worker stopped.");
        }
    }

    private async Task FlushBatchAsync(
        List<ParameterObservationInsertRow> batch,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;

        var rowsToFlush = batch.Count;

        try
        {
            _logger.LogDebug(
                "Flushing telemetry batch. Rows={Rows}",
                rowsToFlush);

            await _bulkRepository.BulkInsertAsync(batch, cancellationToken);

            _logger.LogInformation(
                "Telemetry batch flushed successfully. Rows={Rows}",
                rowsToFlush);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Telemetry batch flush failed. Rows={Rows}. Future improvement: write failed batch to DLQ.",
                rowsToFlush);

            // Future Phase:
            // Add IFailedBatchWriter / DLQ persistence here.
            // For now, we log the failure clearly and clear the in-memory batch
            // to prevent blocking the worker forever.
        }
        finally
        {
            batch.Clear();
            stopwatch.Restart();
        }
    }
}