using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlantProcess.Infrastructure.Bulk;
using System.Diagnostics;

namespace PlantProcess.Workers;

public class TelemetryIngestionWorker : BackgroundService
{
    private readonly ITelemetryChannelBuffer _channelBuffer;
    private readonly ITelemetryBulkRepository _bulkRepository;
    private readonly ILogger<TelemetryIngestionWorker> _logger;

    private const int BatchSizeLimit = 5000;
    private const int FlushIntervalMilliseconds = 1000;

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
        _logger.LogInformation("High-Frequency Telemetry Ingestion Worker started.");

        var batch = new List<ParameterObservationInsertRow>(BatchSizeLimit);
        var stopwatch = new Stopwatch();

        try
        {
            await foreach (var observation in _channelBuffer.ConsumeAllAsync(stoppingToken))
            {
                if (batch.Count == 0) stopwatch.Restart();

                batch.Add(observation);

                // Flush condition: We hit 5,000 items OR it's been waiting for more than 1 second
                if (batch.Count >= BatchSizeLimit || stopwatch.ElapsedMilliseconds > FlushIntervalMilliseconds)
                {
                    await FlushBatchAsync(batch, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown, flush anything remaining
            if (batch.Any()) await FlushBatchAsync(batch, CancellationToken.None);
        }
    }

    private async Task FlushBatchAsync(List<ParameterObservationInsertRow> batch, CancellationToken ct)
    {
        if (batch.Count == 0) return;

        try
        {
            _logger.LogDebug("Flushing {BatchSize} telemetry records to Npgsql...", batch.Count);

            // Execute the bulk copy
            await _bulkRepository.BulkInsertAsync(batch, ct);

            _logger.LogInformation("Successfully bulk inserted {BatchSize} rows.", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FATAL ERROR during bulk import of {BatchSize} records.", batch.Count);
            // NOTE FOR TASK 3: This is exactly where we will implement the Dead Letter Queue (DLQ).
            // await _dlqService.WriteFailedBatchAsync(batch);
        }
        finally
        {
            batch.Clear(); // Empty the list for the next round
        }
    }
}