using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Analytics.Services;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Workers;

/// <summary>
/// Background worker host for PlantProcess IQ automated jobs.
/// 
/// Phase 2 upgrade:
/// - Every execution updates JobDefinition status.
/// - Every execution creates JobRunHistory.
/// - Jobs Monitor becomes a real operational monitor, not a static/synthesized table.
/// </summary>
public class Worker : BackgroundService
{
    private const string ImportQueueJobCode = "SYSTEM_IMPORT_QUEUE_PROCESSOR";
    private const string DataQualityJobCode = "SYSTEM_DATA_QUALITY_SCAN";
    private const string RiskScoringJobCode = "SYSTEM_RISK_SCORING";

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
        _logger.LogInformation(
            "PlantProcess IQ Worker started. Jobs: ImportQueueProcessorJob, DataQualityScanJob, RiskScoringJob.");

        var importEnabled = _configuration.GetValue("PlantProcess:Workers:EnableImportQueueProcessorJob", true);
        var importIntervalSeconds = Math.Max(30, _configuration.GetValue("PlantProcess:Workers:ImportQueueProcessorIntervalSeconds", 120));
        var importMaxBatches = Math.Clamp(_configuration.GetValue("PlantProcess:Workers:ImportQueueProcessorMaxBatches", 5), 1, 100);
        var importRowsPerBatch = Math.Clamp(_configuration.GetValue("PlantProcess:Workers:ImportQueueProcessorRowsPerBatch", 5000), 1, 50000);

        var scanEnabled = _configuration.GetValue("PlantProcess:Workers:EnableDataQualityScanJob", true);
        var scanIntervalSeconds = Math.Max(60, _configuration.GetValue("PlantProcess:Workers:DataQualityScanIntervalSeconds", 3600));
        var scanMaxCandidatesPerRule = Math.Clamp(_configuration.GetValue("PlantProcess:Workers:DataQualityMaxCandidatesPerRule", 500), 1, 5000);

        var riskEnabled = _configuration.GetValue("PlantProcess:Workers:EnableRiskScoringJob", true);
        var riskIntervalSeconds = Math.Max(300, _configuration.GetValue("PlantProcess:Workers:RiskScoringIntervalSeconds", 7200));
        var riskMaxMaterials = Math.Clamp(_configuration.GetValue("PlantProcess:Workers:RiskScoringMaxMaterials", 100), 1, 5000);
        var riskType = _configuration.GetValue("PlantProcess:Workers:RiskScoringRiskType", RiskScoreService.DefaultRiskType)
            ?? RiskScoreService.DefaultRiskType;

        var nextImportRun = DateTimeOffset.UtcNow.AddSeconds(10);
        var nextScanRun = DateTimeOffset.UtcNow.AddSeconds(30);
        var nextRiskRun = DateTimeOffset.UtcNow.AddSeconds(60);

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

            if (riskEnabled && now >= nextRiskRun)
            {
                await RunRiskScoringJobAsync(riskType, riskMaxMaterials, stoppingToken);
                nextRiskRun = DateTimeOffset.UtcNow.AddSeconds(riskIntervalSeconds);
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
        await using var scope = _scopeFactory.CreateAsyncScope();

        var runtime = scope.ServiceProvider.GetRequiredService<IJobRuntimeService>();
        var service = scope.ServiceProvider.GetRequiredService<IImportBatchQueueProcessorService>();

        var run = await runtime.StartAsync(
            ImportQueueJobCode,
            triggerSource: "WorkerSchedule",
            triggeredBy: "PlantProcess.Workers",
            correlationId: Guid.NewGuid().ToString("N"),
            cancellationToken);

        if (run.IsFailure || run.Value is null)
        {
            _logger.LogWarning(
                "ImportQueueProcessorJob: skipped. Reason={Reason}",
                run.Error?.Message);
            return;
        }

        try
        {
            _logger.LogInformation(
                "ImportQueueProcessorJob: starting. MaxBatches={MaxBatches}, RowsPerBatch={RowsPerBatch}",
                maxBatches,
                rowsPerBatch);

            var result = await service.ProcessPendingBatchesAsync(
                maxBatches,
                rowsPerBatch,
                stopOnFirstError: false,
                runDataQualityScan: true,
                cancellationToken);

            if (result.IsSuccess && result.Value is not null)
            {
                var message =
                    $"Import queue completed. Scanned={result.Value.BatchesScanned}, Completed={result.Value.BatchesCompleted}, Failed={result.Value.BatchesFailed}, Skipped={result.Value.BatchesSkipped}.";

                _logger.LogInformation("{Message}", message);

                await runtime.CompleteAsync(
                    run.Value.Id,
                    JobRunStatus.Ok,
                    message,
                    failureReason: null,
                    resultSummaryJson: System.Text.Json.JsonSerializer.Serialize(result.Value),
                    cancellationToken);
            }
            else
            {
                var message = result.Error?.Message ?? "Import queue processor failed.";

                _logger.LogError("ImportQueueProcessorJob: failed. Error={Message}", message);

                await runtime.CompleteAsync(
                    run.Value.Id,
                    JobRunStatus.Failed,
                    message,
                    message,
                    resultSummaryJson: null,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ImportQueueProcessorJob: cancelled during shutdown.");

            await runtime.CompleteAsync(
                run.Value.Id,
                JobRunStatus.Timeout,
                "Import queue job cancelled during shutdown.",
                "Operation cancelled.",
                null,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportQueueProcessorJob: unhandled exception.");

            await runtime.CompleteAsync(
                run.Value.Id,
                JobRunStatus.Failed,
                ex.Message,
                ex.Message,
                null,
                CancellationToken.None);
        }
    }

    private async Task RunDataQualityScanJobAsync(
        int maxCandidatesPerRule,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var runtime = scope.ServiceProvider.GetRequiredService<IJobRuntimeService>();
        var service = scope.ServiceProvider.GetRequiredService<IDataQualityService>();

        var run = await runtime.StartAsync(
            DataQualityJobCode,
            triggerSource: "WorkerSchedule",
            triggeredBy: "PlantProcess.Workers",
            correlationId: Guid.NewGuid().ToString("N"),
            cancellationToken);

        if (run.IsFailure || run.Value is null)
        {
            _logger.LogWarning(
                "DataQualityScanJob: skipped. Reason={Reason}",
                run.Error?.Message);
            return;
        }

        try
        {
            _logger.LogInformation(
                "DataQualityScanJob: starting. MaxCandidatesPerRule={MaxCandidatesPerRule}",
                maxCandidatesPerRule);

            var result = await service.RunFullScanAsync(maxCandidatesPerRule, cancellationToken);

            if (result.IsSuccess && result.Value is not null)
            {
                var message =
                    $"Data quality scan completed. Candidates={result.Value.CandidatesFound}, NewIssues={result.Value.NewIssuesPersisted}, ExistingSkipped={result.Value.ExistingIssuesSkipped}.";

                _logger.LogInformation("{Message}", message);

                await runtime.CompleteAsync(
                    run.Value.Id,
                    JobRunStatus.Ok,
                    message,
                    failureReason: null,
                    resultSummaryJson: System.Text.Json.JsonSerializer.Serialize(result.Value),
                    cancellationToken);
            }
            else
            {
                var message = result.Error?.Message ?? "Data quality scan failed.";

                _logger.LogError("DataQualityScanJob: failed. Error={Message}", message);

                await runtime.CompleteAsync(
                    run.Value.Id,
                    JobRunStatus.Failed,
                    message,
                    message,
                    null,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("DataQualityScanJob: cancelled during shutdown.");

            await runtime.CompleteAsync(
                run.Value.Id,
                JobRunStatus.Timeout,
                "Data quality job cancelled during shutdown.",
                "Operation cancelled.",
                null,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DataQualityScanJob: unhandled exception.");

            await runtime.CompleteAsync(
                run.Value.Id,
                JobRunStatus.Failed,
                ex.Message,
                ex.Message,
                null,
                CancellationToken.None);
        }
    }

    private async Task RunRiskScoringJobAsync(
        string riskType,
        int maxMaterials,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var runtime = scope.ServiceProvider.GetRequiredService<IJobRuntimeService>();
        var service = scope.ServiceProvider.GetRequiredService<IRiskScoreService>();

        var run = await runtime.StartAsync(
            RiskScoringJobCode,
            triggerSource: "WorkerSchedule",
            triggeredBy: "PlantProcess.Workers",
            correlationId: Guid.NewGuid().ToString("N"),
            cancellationToken);

        if (run.IsFailure || run.Value is null)
        {
            _logger.LogWarning(
                "RiskScoringJob: skipped. Reason={Reason}",
                run.Error?.Message);
            return;
        }

        try
        {
            _logger.LogInformation(
                "RiskScoringJob: starting. RiskType={RiskType}, MaxMaterials={MaxMaterials}",
                riskType,
                maxMaterials);

            var result = await service.CalculateBatchAsync(
                new CalculateRiskScoresBatchCommand(
                    SiteId: null,
                    RiskType: riskType,
                    MaxMaterials: maxMaterials,
                    StoreResult: true,
                    RequestedBy: "PlantProcess.Worker",
                    CorrelationId: Guid.NewGuid().ToString("N")),
                cancellationToken);

            if (result.IsSuccess && result.Value is not null)
            {
                var message =
                    $"Risk scoring completed. Candidates={result.Value.CandidatesScanned}, Calculated={result.Value.ScoresCalculated}, Stored={result.Value.ScoresStored}, Skipped={result.Value.Skipped}.";

                _logger.LogInformation("{Message}", message);

                await runtime.CompleteAsync(
                    run.Value.Id,
                    JobRunStatus.Ok,
                    message,
                    failureReason: null,
                    resultSummaryJson: System.Text.Json.JsonSerializer.Serialize(result.Value),
                    cancellationToken);
            }
            else
            {
                var message = result.Error?.Message ?? "Risk scoring failed.";

                _logger.LogError("RiskScoringJob: failed. Error={Message}", message);

                await runtime.CompleteAsync(
                    run.Value.Id,
                    JobRunStatus.Failed,
                    message,
                    message,
                    null,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("RiskScoringJob: cancelled during shutdown.");

            await runtime.CompleteAsync(
                run.Value.Id,
                JobRunStatus.Timeout,
                "Risk scoring job cancelled during shutdown.",
                "Operation cancelled.",
                null,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RiskScoringJob: unhandled exception.");

            await runtime.CompleteAsync(
                run.Value.Id,
                JobRunStatus.Failed,
                ex.Message,
                ex.Message,
                null,
                CancellationToken.None);
        }
    }
}
