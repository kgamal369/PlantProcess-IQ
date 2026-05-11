using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlantProcess.Infrastructure.Bulk;

namespace PlantProcess.Infrastructure.Resilience;

public interface IFailedBatchWriter
{
    Task WriteFailedBatchAsync(IReadOnlyList<ParameterObservationInsertRow> batch, Exception exception, CancellationToken cancellationToken = default);
}

public class FailedBatchWriter : IFailedBatchWriter
{
    private readonly ILogger<FailedBatchWriter> _logger;
    private readonly string _dlqDirectory;

    public FailedBatchWriter(ILogger<FailedBatchWriter> logger)
    {
        _logger = logger;

        // Ensure the local DLQ directory exists (can be mapped to a Docker volume in production)
        _dlqDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dlq");
        if (!Directory.Exists(_dlqDirectory))
        {
            Directory.CreateDirectory(_dlqDirectory);
        }
    }

    public async Task WriteFailedBatchAsync(IReadOnlyList<ParameterObservationInsertRow> batch, Exception exception, CancellationToken cancellationToken = default)
    {
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            var filename = $"failed_telemetry_batch_{timestamp}.json";
            var filepath = Path.Combine(_dlqDirectory, filename);

            // Create a structured wrapper containing the context AND the raw data
            var payload = new
            {
                FailedAtUtc = DateTime.UtcNow,
                RecordCount = batch.Count,
                ExceptionMessage = exception.Message,
                // Do not serialize the entire stack trace if it's too large, but useful for debugging
                StackTrace = exception.StackTrace,
                RawData = batch
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filepath, json, cancellationToken);

            _logger.LogWarning("DLQ ACTIVATED: Saved {Count} orphaned telemetry records safely to disk -> {FilePath}", batch.Count, filepath);
        }
        catch (Exception ex)
        {
            // The absolute worst-case scenario: DB is down AND the disk is full/unwritable
            _logger.LogCritical(ex, "FATAL: Dead Letter Queue failed to write to disk. DATA LOSS OCCURRED for {Count} records.", batch.Count);
        }
    }
}