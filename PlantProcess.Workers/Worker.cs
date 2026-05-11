using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Application.Services.Integration;

namespace PlantProcess.Workers;

/// <summary>
/// Background worker host for PlantProcess IQ automated jobs.
///
/// Active jobs:
///   1. ImportQueueProcessorJob — processes Created/Running import batches with pending staging rows.
///   2. DataQualityScanJob     — runs the automated full data-quality scan and persists findings.
///
/// Design rule: Worker.cs only schedules jobs. Business logic stays in PlantProcess.Application services.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PlantProcess IQ Worker started. Jobs: ImportQueueProcessorJob, DataQualityScanJob.");

        var importEnabled = _configuration.GetValue("PlantProcess:Workers:EnableImportQueueProcessorJob", true);
        var importIntervalSeconds = Math.Max(30, _configuration.GetValue("PlantProcess:Workers:ImportQueueProcessorIntervalSeconds", 120));
        var importMaxBatches = Math.Clamp(_configuration.GetValue("PlantProcess:Workers:ImportQueueProcessorMaxBatches", 5), 1, 100);
        var importRowsPerBatch = Math.Clamp(_configuration.GetValue("PlantProcess:Workers:ImportQueueProcessorRowsPerBatch", 5000), 1, 50000);

        var scanEnabled = _configuration.GetValue("PlantProcess:Workers:EnableDataQualityScanJob", true);
        var scanIntervalSeconds = Math.Max(60, _configuration.GetValue("PlantProcess:Workers:DataQualityScanIntervalSeconds", 3600));
        var scanMaxCandidatesPerRule = Math.Clamp(_configuration.GetValue("PlantProcess:Workers:DataQualityMaxCandidatesPerRule", 500), 1, 5000);

        var nextImportRun = DateTimeOffset.UtcNow.AddSeconds(10);
        var nextScanRun = DateTimeOffset.UtcNow.AddSeconds(30);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            if (importEnabled && now >= nextImportRun)
            {
                await RunImportQueueProcessorJobAsync(importMaxBatches, importRowsPerBatch, stoppingToken);
                nextImportRun = DateTimeOffset.UtcNow.AddSeconds(importIntervalSeconds);
            }

            if (scanEnabled && now >= nextScanRun)
            {
                await RunDataQualityScanJobAsync(scanMaxCandidatesPerRule, stoppingToken);
                nextScanRun = DateTimeOffset.UtcNow.AddSeconds(scanIntervalSeconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("PlantProcess IQ Worker stopping.");
    }

    private async Task RunImportQueueProcessorJobAsync(
        int maxBatches,
        int rowsPerBatch,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "ImportQueueProcessorJob: starting. MaxBatches={MaxBatches}, RowsPerBatch={RowsPerBatch}",
            maxBatches,
            rowsPerBatch);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportBatchQueueProcessorService>();

            var result = await service.ProcessPendingBatchesAsync(
                maxBatches,
                rowsPerBatch,
                stopOnFirstError: false,
                runDataQualityScan: true,
                cancellationToken);

            if (result.IsSuccess && result.Value is not null)
            {
                _logger.LogInformation(
                    "ImportQueueProcessorJob: completed. Scanned={Scanned}, Completed={Completed}, Failed={Failed}, Skipped={Skipped}, DurationMs={DurationMs}",
                    result.Value.BatchesScanned,
                    result.Value.BatchesCompleted,
                    result.Value.BatchesFailed,
                    result.Value.BatchesSkipped,
                    (long)result.Value.Duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogError(
                    "ImportQueueProcessorJob: failed. Error={ErrorCode} — {ErrorMessage}",
                    result.Error?.Code,
                    result.Error?.Message);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ImportQueueProcessorJob: cancelled during shutdown.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportQueueProcessorJob: unhandled exception. Will retry on next interval.");
        }
    }

    private async Task RunDataQualityScanJobAsync(
        int maxCandidatesPerRule,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "DataQualityScanJob: starting. MaxCandidatesPerRule={MaxCandidatesPerRule}",
            maxCandidatesPerRule);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IDataQualityService>();

            var result = await service.RunFullScanAsync(maxCandidatesPerRule, cancellationToken);

            if (result.IsSuccess && result.Value is not null)
            {
                _logger.LogInformation(
                    "DataQualityScanJob: completed. CandidatesFound={CandidatesFound}, NewIssuesPersisted={NewIssuesPersisted}, ExistingIssuesSkipped={ExistingIssuesSkipped}, DurationMs={DurationMs}",
                    result.Value.CandidatesFound,
                    result.Value.NewIssuesPersisted,
                    result.Value.ExistingIssuesSkipped,
                    (long)result.Value.ScanDuration.TotalMilliseconds);

                if (result.Value.NewIssuesPersisted > 0)
                {
                    _logger.LogWarning(
                        "DataQualityScanJob: {NewIssuesPersisted} new data-quality issues were persisted. Review /data-quality/issues.",
                        result.Value.NewIssuesPersisted);
                }
            }
            else
            {
                _logger.LogError(
                    "DataQualityScanJob: failed. Error={ErrorCode} — {ErrorMessage}",
                    result.Error?.Code,
                    result.Error?.Message);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("DataQualityScanJob: cancelled during shutdown.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataQualityScanJob: unhandled exception. Will retry on next interval.");
        }
    }
}
