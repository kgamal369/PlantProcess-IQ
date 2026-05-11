using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using PlantProcess.Application.Services.DataQuality;

namespace PlantProcess.Workers;

/// <summary>
/// Background worker host for PlantProcess IQ automated jobs.
///
/// Currently active jobs:
///   • DataQualityScanJob — runs RunFullScanAsync on a configurable interval.
///
/// Future Sprint 2+ jobs to add here:
///   • ImportOrchestrationJob  — monitor import batch queue, run CSV/SQL connectors
///   • RiskScoringJob          — score newly completed materials automatically
///   • SyntheticDataGenerator  — generate/refresh synthetic seed data
/// </summary>
public class Worker : BackgroundService
{
    // ─── Dependencies ─────────────────────────────────────────────────────────
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    // ─── Constructor ──────────────────────────────────────────────────────────
    public Worker(
        ILogger<Worker> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    // ─── Background loop ──────────────────────────────────────────────────────
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "PlantProcess IQ Worker started. Jobs: DataQualityScanJob.");

        // Read configuration values — both default to safe values if absent
        var scanEnabled = _configuration.GetValue("PlantProcess:Workers:EnableDataQualityScanJob", true);
        var scanIntervalSecs = _configuration.GetValue("PlantProcess:Workers:DataQualityScanIntervalSeconds", 3600);
        var scanInterval = TimeSpan.FromSeconds(Math.Max(60, scanIntervalSecs)); // minimum 60 s

        _logger.LogInformation(
            "Worker configuration loaded. " +
            "EnableDataQualityScanJob={Enabled}, ScanInterval={ScanInterval}",
            scanEnabled,
            scanInterval);

        // ── Initial delay: give the API time to start before first scan ───
        _logger.LogDebug("Worker waiting 30 s before first data-quality scan run.");
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        // ── Main loop ─────────────────────────────────────────────────────
        while (!stoppingToken.IsCancellationRequested)
        {
            if (scanEnabled)
            {
                await RunDataQualityScanJobAsync(stoppingToken);
            }
            else
            {
                _logger.LogDebug(
                    "DataQualityScanJob is disabled via configuration. " +
                    "Set PlantProcess:Workers:EnableDataQualityScanJob=true to enable.");
            }

            // Wait for the configured interval before the next run
            _logger.LogDebug(
                "Worker sleeping for {Interval} before next scan cycle.",
                scanInterval);

            await Task.Delay(scanInterval, stoppingToken);
        }

        _logger.LogInformation("PlantProcess IQ Worker stopping.");
    }

    // ─── Job: Data Quality Full Scan ──────────────────────────────────────────

    private async Task RunDataQualityScanJobAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "DataQualityScanJob: starting scheduled run. UtcNow={UtcNow}",
            DateTime.UtcNow);

        try
        {
            // Each job run gets its own DI scope so DbContext is fresh
            // (DbContext is scoped — cannot use singleton scope)
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IDataQualityService>();

            // maxCandidatesPerRule = 500 per run — prevents DB overload
            var result = await service.RunFullScanAsync(
                maxCandidatesPerRule: 500,
                cancellationToken: cancellationToken);

            if (result.IsSuccess && result.Value is not null)
            {
                var summary = result.Value;

                _logger.LogInformation(
                    "DataQualityScanJob: completed successfully. " +
                    "CandidatesFound={CandidatesFound}, " +
                    "NewIssuesPersisted={NewIssuesPersisted}, " +
                    "ExistingIssuesSkipped={ExistingIssuesSkipped}, " +
                    "DurationMs={DurationMs}",
                    summary.CandidatesFound,
                    summary.NewIssuesPersisted,
                    summary.ExistingIssuesSkipped,
                    (long)summary.ScanDuration.TotalMilliseconds);

                if (summary.NewIssuesPersisted > 0)
                {
                    _logger.LogWarning(
                        "DataQualityScanJob: {NewIssuesPersisted} new data-quality issues were found and persisted. " +
                        "Review GET /data-quality/issues for details.",
                        summary.NewIssuesPersisted);
                }
            }
            else
            {
                _logger.LogError(
                    "DataQualityScanJob: scan returned a failure result. " +
                    "Error={ErrorCode} — {ErrorMessage}",
                    result.Error?.Code,
                    result.Error?.Message);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("DataQualityScanJob: cancelled during execution (host is shutting down).");
        }
        catch (Exception ex)
        {
            // Log and swallow — do not crash the worker host over a single scan failure.
            // The next scheduled run will retry.
            _logger.LogError(
                ex,
                "DataQualityScanJob: unhandled exception during scan run. " +
                "Will retry on next scheduled interval. Exception={ExceptionMessage}",
                ex.Message);
        }
    }
}