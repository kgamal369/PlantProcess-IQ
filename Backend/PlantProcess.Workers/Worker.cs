using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Analytics.Services;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Application.Jobs.Scheduling;
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
///
/// Phase 3 addition (HIGH item 10):
/// - DeltaImportJob: reads only new rows per dataset using IncrementalCursorField.
/// </summary>
public class Worker : BackgroundService
{
    private const string ImportQueueJobCode = "SYSTEM_IMPORT_QUEUE_PROCESSOR";
    private const string DataQualityJobCode  = "SYSTEM_DATA_QUALITY_SCAN";
    private const string RiskScoringJobCode  = "SYSTEM_RISK_SCORING";
    private const string DeltaImportJobCode  = "SYSTEM_DELTA_IMPORT_JOB";   // ← NEW

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
            "PlantProcess IQ Worker started. It polls the governed schedule dispatcher; it owns no schedule semantics.");

        // T-106 B2.3c. The four "UtcNow + configured interval" timers that used to BE the
        // schedule are gone. A job runs because its saved governed schedule says it is due,
        // and the poll instant never becomes the occurrence identity.
        var pollSeconds = Math.Clamp(
            _configuration.GetValue("PlantProcess:Workers:GovernedSchedulePollSeconds", 5), 1, 60);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchGovernedSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host shutdown. This is not an operator cancelling a run and is never
                // recorded as one: run-level cancellation is the T-106 request path.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Governed schedule dispatch failed on this poll; the next poll retries.");
            }

            await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken);
        }

        _logger.LogInformation("PlantProcess IQ Worker stopping.");
    }

    // ── Delta Import Job (NEW — HIGH item 10) ─────────────────────────────────

    private async Task RunDeltaImportJobAsync(
        int maxDatasets,
        int maxRowsPerDataset,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var runtime = scope.ServiceProvider.GetRequiredService<IJobRuntimeService>();
        var service = scope.ServiceProvider.GetRequiredService<IDeltaImportExecutionService>();

        var run = await runtime.StartAsync(
            DeltaImportJobCode,
            triggerSource: "WorkerSchedule",
            triggeredBy: "PlantProcess.Workers",
            correlationId: Guid.NewGuid().ToString("N"),
            cancellationToken);

        if (run.IsFailure || run.Value is null)
        {
            _logger.LogWarning(
                "DeltaImportJob: skipped. Reason={Reason}",
                run.Error?.Message);
            return;
        }

        try
        {
            _logger.LogInformation(
                "DeltaImportJob: starting. MaxDatasets={MaxDatasets}, MaxRowsPerDataset={MaxRows}",
                maxDatasets,
                maxRowsPerDataset);

            var summary = await service.ExecuteAllAsync(maxDatasets, maxRowsPerDataset, cancellationToken);

            var message =
                $"Delta import completed. Datasets={summary.DatasetsProcessed}, " +
                $"Rows={summary.TotalRowsImported}, Failures={summary.DatasetsFailedCount}.";

            _logger.LogInformation("{Message}", message);

            await runtime.CompleteAsync(
                run.Value.Id,
                summary.DatasetsFailedCount == 0 ? JobRunStatus.Ok : JobRunStatus.Failed,
                message,
                failureReason: summary.DatasetsFailedCount > 0
                    ? $"{summary.DatasetsFailedCount} dataset(s) failed — see logs."
                    : null,
                resultSummaryJson: System.Text.Json.JsonSerializer.Serialize(summary),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("DeltaImportJob: cancelled during shutdown.");

            await runtime.CompleteAsync(
                run.Value.Id,
                JobRunStatus.Timeout,
                "Delta import job cancelled during shutdown.",
                "Operation cancelled.",
                null,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeltaImportJob: unhandled exception.");

            await runtime.CompleteAsync(
                run.Value.Id,
                JobRunStatus.Failed,
                ex.Message,
                ex.Message,
                null,
                CancellationToken.None);
        }
    }

    // ── Import Queue Processor ────────────────────────────────────────────────

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
                    $"Import queue completed. Scanned={result.Value.BatchesScanned}, " +
                    $"Completed={result.Value.BatchesCompleted}, Failed={result.Value.BatchesFailed}, " +
                    $"Skipped={result.Value.BatchesSkipped}.";

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

    // ── Data Quality Scan ─────────────────────────────────────────────────────

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
                    $"Data quality scan completed. Candidates={result.Value.CandidatesFound}, " +
                    $"NewIssues={result.Value.NewIssuesPersisted}, ExistingSkipped={result.Value.ExistingIssuesSkipped}.";

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

    // ── Risk Scoring ──────────────────────────────────────────────────────────

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
                    $"Risk scoring completed. Candidates={result.Value.CandidatesScanned}, " +
                    $"Calculated={result.Value.ScoresCalculated}, Stored={result.Value.ScoresStored}, " +
                    $"Skipped={result.Value.Skipped}.";

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

    /// <summary>
    /// T-106 B2.3c. One poll: ask the governed dispatcher what is due and let it execute
    /// through the registered dispatcher. Each execution gets its own scope; overlapping
    /// polls share the same process admission and dispatch bounds.
    /// </summary>
    private async Task DispatchGovernedSchedulesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<GovernedScheduleDispatcher>();

        DispatchReport report = await dispatcher.DispatchDueAsync(DateTime.UtcNow, cancellationToken);

        if (report.OccurrencesDue == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Governed schedule poll: considered={Considered}, due={Due}, dispatched={Dispatched}, claimed={Claimed}, refused={Refused}",
            report.JobsConsidered, report.OccurrencesDue, report.Dispatched, report.AlreadyClaimed, report.Refused);

        foreach (string note in report.Notes)
        {
            _logger.LogWarning("Governed schedule refusal: {Note}", note);
        }
    }
}
