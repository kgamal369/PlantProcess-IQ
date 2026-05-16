using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Application.Services.Analytics;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Services.Integration;

public sealed class JobRunOrchestratorService : IJobRunOrchestratorService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IJobRuntimeService _jobRuntimeService;
    private readonly IImportBatchQueueProcessorService _importBatchQueueProcessorService;
    private readonly IDataQualityService _dataQualityService;
    private readonly IRiskScoreService _riskScoreService;

    public JobRunOrchestratorService(
        IPlantProcessDbContext dbContext,
        IJobRuntimeService jobRuntimeService,
        IImportBatchQueueProcessorService importBatchQueueProcessorService,
        IDataQualityService dataQualityService,
        IRiskScoreService riskScoreService)
    {
        _dbContext = dbContext;
        _jobRuntimeService = jobRuntimeService;
        _importBatchQueueProcessorService = importBatchQueueProcessorService;
        _dataQualityService = dataQualityService;
        _riskScoreService = riskScoreService;
    }

    public async Task<ApplicationResult<JobActionResponseDto>> RunNowAsync(
        Guid jobDefinitionId,
        string? requestedBy,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.JobDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == jobDefinitionId, cancellationToken);

        if (job is null)
            return ApplicationResult<JobActionResponseDto>.Failure(ApplicationError.NotFound("Job definition was not found."));

        if (!job.IsEnabled)
            return ApplicationResult<JobActionResponseDto>.Failure(ApplicationError.BusinessRule("Paused jobs cannot be executed. Resume the job first."));

        var run = await _jobRuntimeService.StartAsync(
            job.JobCode,
            triggerSource: "ManualRunNow",
            triggeredBy: requestedBy ?? "Admin",
            correlationId: correlationId ?? Guid.NewGuid().ToString("N"),
            cancellationToken);

        if (run.IsFailure || run.Value is null)
            return ApplicationResult<JobActionResponseDto>.Failure(run.Error!);

        try
        {
            var executionResult = await ExecuteJobAsync(job.JobType, cancellationToken);

            var finalStatus = executionResult.IsSuccess
                ? JobRunStatus.Ok
                : JobRunStatus.Failed;

            var completed = await _jobRuntimeService.CompleteAsync(
                run.Value.Id,
                finalStatus,
                executionResult.Message,
                executionResult.IsSuccess ? null : executionResult.Message,
                executionResult.ResultSummaryJson,
                cancellationToken);

            if (completed.IsFailure)
                return ApplicationResult<JobActionResponseDto>.Failure(completed.Error!);

            return ApplicationResult<JobActionResponseDto>.Success(
                new JobActionResponseDto(
                    job.Id,
                    job.JobCode,
                    job.JobName,
                    job.JobType,
                    finalStatus,
                    executionResult.Message,
                    run.Value.Id,
                    DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            await _jobRuntimeService.CompleteAsync(
                run.Value.Id,
                JobRunStatus.Failed,
                ex.Message,
                ex.Message,
                null,
                CancellationToken.None);

            return ApplicationResult<JobActionResponseDto>.Failure(
                ApplicationError.Unexpected($"Run Now failed: {ex.Message}"));
        }
    }

    private async Task<RunNowExecutionResult> ExecuteJobAsync(
        JobDefinitionType jobType,
        CancellationToken cancellationToken)
    {
        switch (jobType)
        {
            case JobDefinitionType.DbLinkImport:
            case JobDefinitionType.CanonicalRefresh:
            {
                var result = await _importBatchQueueProcessorService.ProcessPendingBatchesAsync(
                    maxBatches: 10,
                    rowsPerBatch: 5000,
                    stopOnFirstError: false,
                    runDataQualityScan: false,
                    cancellationToken);

                if (result.IsFailure || result.Value is null)
                {
                    return RunNowExecutionResult.Failed(
                        result.Error?.Message ?? "Import queue processing failed.");
                }

                return RunNowExecutionResult.Ok(
                    $"Import queue completed. Scanned={result.Value.BatchesScanned}, Completed={result.Value.BatchesCompleted}, Failed={result.Value.BatchesFailed}.",
                    JsonSerializer.Serialize(result.Value));
            }

            case JobDefinitionType.DataQualityScan:
            {
                var result = await _dataQualityService.RunFullScanAsync(
                    maxCandidatesPerRule: 500,
                    cancellationToken);

                if (result.IsFailure || result.Value is null)
                {
                    return RunNowExecutionResult.Failed(
                        result.Error?.Message ?? "Data quality scan failed.");
                }

                return RunNowExecutionResult.Ok(
                    $"Data quality scan completed. Candidates={result.Value.CandidatesFound}, NewIssues={result.Value.NewIssuesPersisted}.",
                    JsonSerializer.Serialize(result.Value));
            }

            case JobDefinitionType.RiskScoring:
            {
                var result = await _riskScoreService.CalculateBatchAsync(
                    new CalculateRiskScoresBatchCommand(
                        SiteId: null,
                        RiskType: "QualityRisk",
                        MaxMaterials: 100,
                        StoreResult: true,
                        RequestedBy: "AdminRunNow",
                        CorrelationId: Guid.NewGuid().ToString("N")),
                    cancellationToken);

                if (result.IsFailure || result.Value is null)
                {
                    return RunNowExecutionResult.Failed(
                        result.Error?.Message ?? "Risk scoring failed.");
                }

                return RunNowExecutionResult.Ok(
                    $"Risk scoring completed. Calculated={result.Value.ScoresCalculated}, Stored={result.Value.ScoresStored}.",
                    JsonSerializer.Serialize(result.Value));
            }

            case JobDefinitionType.Custom:
                return RunNowExecutionResult.Failed(
                    "This custom/continuous job has no manual executor. Continuous jobs are monitored by the Worker runtime.");

            default:
                return RunNowExecutionResult.Failed(
                    $"Run Now executor is not implemented yet for job type '{jobType}'.");
        }
    }

    private sealed record RunNowExecutionResult(
        bool IsSuccess,
        string Message,
        string? ResultSummaryJson)
    {
        public static RunNowExecutionResult Ok(string message, string? resultSummaryJson)
        {
            return new RunNowExecutionResult(true, message, resultSummaryJson);
        }

        public static RunNowExecutionResult Failed(string message)
        {
            return new RunNowExecutionResult(false, message, null);
        }
    }
}