using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Services.Jobs;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Integration.Services.Jobs;

public sealed class JobRuntimeService : IJobRuntimeService
{
    private readonly IPlantProcessDbContext _dbContext;

    public JobRuntimeService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>T-106 B2.3c. Manual start: no occurrence identity, exactly as before.</summary>
    public Task<ApplicationResult<JobRunHistoryDto>> StartAsync(
        string jobCode,
        string triggerSource,
        string? triggeredBy,
        string? correlationId,
        CancellationToken cancellationToken)
        => StartCoreAsync(jobCode, null, null, triggerSource, triggeredBy, correlationId, cancellationToken);

    /// <summary>
    /// T-106 B2.3c. Scheduled start: the same creation core, carrying the governed
    /// occurrence identity so the insert itself is the claim.
    /// </summary>
    public Task<ApplicationResult<JobRunHistoryDto>> StartScheduledAsync(
        string jobCode,
        string occurrenceKey,
        DateTime nominalAtUtc,
        string triggerSource,
        string? triggeredBy,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(occurrenceKey))
        {
            return Task.FromResult(ApplicationResult<JobRunHistoryDto>.Failure(
                ApplicationError.Validation("A scheduled run requires a governed occurrence identity.")));
        }

        return StartCoreAsync(jobCode, occurrenceKey, nominalAtUtc, triggerSource, triggeredBy, correlationId, cancellationToken);
    }

    /// <summary>
    /// The one place a JobRunHistory row is created. Manual and scheduled differ by the
    /// occurrence they carry and by nothing else.
    /// </summary>
    private async Task<ApplicationResult<JobRunHistoryDto>> StartCoreAsync(
        string jobCode,
        string? occurrenceKey,
        DateTime? nominalAtUtc,
        string triggerSource,
        string? triggeredBy,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jobCode))
            return ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.Validation("Job code is required."));

        var normalizedCode = NormalizeCode(jobCode);

        var job = await _dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.JobCode == normalizedCode, cancellationToken);

        if (job is null)
            return ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.NotFound($"Job '{normalizedCode}' was not found."));

        if (!job.IsEnabled)
            return ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.BusinessRule($"Job '{normalizedCode}' is paused/disabled."));

        job.MarkRunning(DateTime.UtcNow);

        var history = new JobRunHistory(
            jobDefinitionId: job.Id,
            jobCode: job.JobCode,
            jobName: job.JobName,
            jobType: job.JobType,
            triggerSource: triggerSource,
            triggeredBy: triggeredBy,
            correlationId: correlationId,
            isSynthetic: false,
            sourceSystem: "PlantProcessIQ.JobRuntime",
            sourceRecordId: Guid.NewGuid().ToString("N"),
            occurrenceKey: occurrenceKey,
            nominalAtUtc: nominalAtUtc);

        _dbContext.JobRunHistories.Add(history);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (occurrenceKey is not null && IsOccurrenceAlreadyClaimed(ex))
        {
            // Exactly one meaning: another dispatcher already claimed this occurrence. It is
            // not an infrastructure failure, not a failed run, and it writes no second row.
            // Only the known occurrence index is read this way; any other unique violation
            // keeps its own meaning and propagates.
            return ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.Conflict(
                JobOccurrenceAlreadyClaimed + " Occurrence '" + occurrenceKey + "' is already claimed."));
        }

        return ApplicationResult<JobRunHistoryDto>.Success(ToDto(history));
    }

    /// <summary>The claim message other layers match on, so nobody re-invents the wording.</summary>
    public const string JobOccurrenceAlreadyClaimed = "OCCURRENCE ALREADY CLAIMED.";

    private const string OccurrenceIndexName = "ux_job_run_histories_occurrence_key";

    private static bool IsOccurrenceAlreadyClaimed(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(OccurrenceIndexName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<ApplicationResult<JobRunHistoryDto>> RecordBlockedAsync(
        Guid jobDefinitionId,
        string triggerSource,
        string? triggeredBy,
        string? correlationId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (jobDefinitionId == Guid.Empty)
            return ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.Validation("Job definition ID is required."));

        var job = await _dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == jobDefinitionId, cancellationToken);

        if (job is null)
            return ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.NotFound("Job definition was not found."));

        var history = new JobRunHistory(
            jobDefinitionId: job.Id,
            jobCode: job.JobCode,
            jobName: job.JobName,
            jobType: job.JobType,
            triggerSource: triggerSource,
            triggeredBy: triggeredBy,
            correlationId: correlationId,
            isSynthetic: false,
            sourceSystem: "PlantProcessIQ.JobRuntime",
            sourceRecordId: null);

        // Born terminal. There is no SaveChanges between construction and this
        // call, so no observer ever sees the transient Running state.
        history.MarkBlocked(reason);
        job.MarkBlocked(reason, history.CompletedAtUtc);

        _dbContext.JobRunHistories.Add(history);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<JobRunHistoryDto>.Success(ToDto(history));
    }

    public async Task<ApplicationResult<JobRunHistoryDto>> CompleteAsync(
        Guid jobRunHistoryId,
        JobRunStatus finalStatus,
        string? message,
        string? failureReason,
        string? resultSummaryJson,
        CancellationToken cancellationToken)
    {
        if (jobRunHistoryId == Guid.Empty)
            return ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.Validation("Job run history ID is required."));

        var history = await _dbContext.JobRunHistories
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == jobRunHistoryId, cancellationToken);

        if (history is null)
            return ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.NotFound("Job run history record was not found."));

        var job = await _dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == history.JobDefinitionId, cancellationToken);

        if (job is null)
            return ApplicationResult<JobRunHistoryDto>.Failure(ApplicationError.NotFound("Job definition was not found."));

        switch (finalStatus)
        {
            case JobRunStatus.Ok:
                history.MarkSucceeded(message, resultSummaryJson);
                job.MarkSucceeded(history.DurationMs, history.CompletedAtUtc);
                break;

            case JobRunStatus.Failed:
                history.MarkFailed(failureReason ?? message ?? "Job failed.", resultSummaryJson);
                job.MarkFailed(history.FailureReason ?? "Job failed.", history.DurationMs, history.CompletedAtUtc);
                break;

            case JobRunStatus.Timeout:
                history.MarkTimedOut(failureReason ?? message ?? "Job timed out.", resultSummaryJson);
                job.MarkTimedOut(history.FailureReason ?? "Job timed out.", history.DurationMs, history.CompletedAtUtc);
                break;

            default:
                return ApplicationResult<JobRunHistoryDto>.Failure(
                    ApplicationError.Validation("Final job status must be Ok, Failed, or Timeout."));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<JobRunHistoryDto>.Success(ToDto(history));
    }

    public async Task<ApplicationResult<IReadOnlyList<JobRunHistoryDto>>> GetHistoryAsync(
        Guid jobDefinitionId,
        int take,
        CancellationToken cancellationToken)
    {
        if (jobDefinitionId == Guid.Empty)
            return ApplicationResult<IReadOnlyList<JobRunHistoryDto>>.Failure(ApplicationError.Validation("Job definition ID is required."));

        take = Math.Clamp(take, 1, 100);

        var rows = await _dbContext.JobRunHistories
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.JobDefinitionId == jobDefinitionId)
            .OrderByDescending(x => x.StartedAtUtc)
            .Take(take)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return ApplicationResult<IReadOnlyList<JobRunHistoryDto>>.Success(rows);
    }

    private static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant().Replace(" ", "_");
    }

    private static JobRunHistoryDto ToDto(JobRunHistory history)
    {
        return new JobRunHistoryDto(
            history.Id,
            history.JobDefinitionId,
            history.JobCode,
            history.JobName,
            history.JobType,
            history.Status,
            history.StartedAtUtc,
            history.CompletedAtUtc,
            history.DurationMs,
            history.TriggerSource,
            history.TriggeredBy,
            history.CorrelationId,
            history.FailureReason,
            history.RunMessage,
            history.ResultSummaryJson);
    }
}



