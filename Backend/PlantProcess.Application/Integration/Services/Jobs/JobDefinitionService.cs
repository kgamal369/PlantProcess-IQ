using PlantProcess.Application.Jobs.Admission;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Services.Jobs;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Application.Jobs.Canvas;
using PlantProcess.Application.Jobs.Scheduling;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Integration.Services.Jobs;

public sealed class JobDefinitionService : IJobDefinitionService
{
    private readonly IPlantProcessDbContext _dbContext;

    public JobDefinitionService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationResult<IReadOnlyList<JobDefinitionDto>>> GetJobsAsync(
        JobDefinitionType? jobType,
        bool includeDisabled,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.JobDefinitions
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (jobType.HasValue)
            query = query.Where(x => x.JobType == jobType.Value);

        if (!includeDisabled)
            query = query.Where(x => x.IsEnabled);

        var jobs = await query
            .OrderBy(x => x.JobType)
            .ThenBy(x => x.JobCode)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return ApplicationResult<IReadOnlyList<JobDefinitionDto>>.Success(jobs);
    }

    public async Task<ApplicationResult<JobDefinitionDto>> GetJobByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.JobDefinitions
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Id == id)
            .Select(x => ToDto(x))
            .FirstOrDefaultAsync(cancellationToken);

        return job is null
            ? ApplicationResult<JobDefinitionDto>.Failure(ApplicationError.NotFound("Job definition not found."))
            : ApplicationResult<JobDefinitionDto>.Success(job);
    }

    public async Task<ApplicationResult<JobDefinitionDto>> CreateJobAsync(
        CreateJobDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateCreateRequest(request);

        if (validation is not null)
            return ApplicationResult<JobDefinitionDto>.Failure(validation);

        var normalizedCode = NormalizeCode(request.JobCode);

        var duplicateExists = await _dbContext.JobDefinitions
            .AnyAsync(x => !x.IsDeleted && x.JobCode == normalizedCode, cancellationToken);

        if (duplicateExists)
        {
            return ApplicationResult<JobDefinitionDto>.Failure(
                ApplicationError.Conflict($"Job code '{normalizedCode}' already exists."));
        }

        var job = new JobDefinition(
            jobCode: normalizedCode,
            jobName: request.JobName,
            jobType: request.JobType,
            scheduleExpression: request.ScheduleExpression,
            isSynthetic: request.IsSynthetic,
            targetId: request.TargetId,
            targetType: request.TargetType,
            isEnabled: request.IsEnabled,
            description: request.Description,
            sourceSystem: request.SourceSystem,
            sourceRecordId: request.SourceRecordId);

        JobLaneAssignment.Initialize(job);
        _dbContext.JobDefinitions.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<JobDefinitionDto>.Success(ToDto(job));
    }

    public async Task<ApplicationResult<JobDefinitionDto>> UpdateJobAsync(
        Guid id,
        UpdateJobDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateUpdateRequest(request);

        if (validation is not null)
            return ApplicationResult<JobDefinitionDto>.Failure(validation);

        var job = await _dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id, cancellationToken);
        if (job is null)
            return ApplicationResult<JobDefinitionDto>.Failure(ApplicationError.NotFound("Job definition not found."));

        var poolError = JobLaneAssignment.ValidateChange(job, request.JobType);
        if (poolError is not null) return ApplicationResult<JobDefinitionDto>.Failure(poolError);

        job.UpdateDefinition(
            jobName: request.JobName,
            jobType: request.JobType,
            scheduleExpression: request.ScheduleExpression,
            targetId: request.TargetId,
            targetType: request.TargetType,
            isEnabled: request.IsEnabled,
            description: request.Description);
        JobLaneAssignment.CompleteFamilyChange(job);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<JobDefinitionDto>.Success(ToDto(job));
    }

    public async Task<ApplicationResult<GovernedJobBindingResult>> BindGovernedTargetAsync(
        GovernedJobBindingRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ApplicationError? invalid = ValidateBinding(request);

        if (invalid is not null)
            return ApplicationResult<GovernedJobBindingResult>.Failure(invalid);

        JobDefinition? job = await _dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.JobCode == request.JobCode, cancellationToken);

        if (job is not null)
            return await RebindAsync(job, request, created: false, cancellationToken);

        var created = new JobDefinition(
            request.JobCode,
            request.JobName,
            request.JobType,
            scheduleExpression: "Manual",
            isSynthetic: false);

        JobLaneAssignment.Initialize(created);
        created.AssignTargetDefinition(
            request.TargetDefinitionKind,
            request.TargetDefinitionId,
            request.VersionPolicy,
            request.PinnedVersion,
            request.ParametersJson);

        _dbContext.JobDefinitions.Add(created);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // ANOTHER REQUEST WON THE RACE. The unique job code is the arbiter, so the
            // loser adopts the row that exists instead of creating a rival job.
            _dbContext.JobDefinitions.Entry(created).State = EntityState.Detached;

            JobDefinition? winner = await _dbContext.JobDefinitions
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.JobCode == request.JobCode, cancellationToken);

            if (winner is null)
                return ApplicationResult<GovernedJobBindingResult>.Failure(ApplicationError.Unexpected(
                    "The job binding was refused by the database and no job with that code exists."));

            return await RebindAsync(winner, request, created: false, cancellationToken);
        }

        return ApplicationResult<GovernedJobBindingResult>.Success(
            new GovernedJobBindingResult(ToDto(created), true));
    }

    private async Task<ApplicationResult<GovernedJobBindingResult>> RebindAsync(
        JobDefinition job,
        GovernedJobBindingRequest request,
        bool created,
        CancellationToken cancellationToken)
    {
        // A JOB OF ANOTHER FAMILY IS NOT THIS BINDING'S TO RETARGET. Silently converting
        // one would point an unrelated schedule at a definition its author never chose.
        if (job.JobType != request.JobType)
        {
            return ApplicationResult<GovernedJobBindingResult>.Failure(ApplicationError.Conflict(
                "Job " + job.JobCode + " is a " + job.JobType + " job and cannot be retargeted by a "
                + request.JobType + " binding."));
        }

        job.AssignTargetDefinition(
            request.TargetDefinitionKind,
            request.TargetDefinitionId,
            request.VersionPolicy,
            request.PinnedVersion,
            request.ParametersJson);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<GovernedJobBindingResult>.Success(
            new GovernedJobBindingResult(ToDto(job), created));
    }

    private static ApplicationError? ValidateBinding(GovernedJobBindingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.JobCode))
            return ApplicationError.Validation("A governed binding requires a job code.");

        if (string.IsNullOrWhiteSpace(request.JobName))
            return ApplicationError.Validation("A governed binding requires a job name.");

        if (string.IsNullOrWhiteSpace(request.TargetDefinitionKind))
            return ApplicationError.Validation("A governed binding requires the kind of definition it targets.");

        if (request.TargetDefinitionId == Guid.Empty)
            return ApplicationError.Validation("A governed binding requires the definition it targets.");

        // THE POLICY AND THE VERSION MUST AGREE. Pinned without a version would silently
        // become "latest", which is a different promise from the one an author made.
        if (request.VersionPolicy == JobTargetVersionPolicy.Pinned
            && (!request.PinnedVersion.HasValue || request.PinnedVersion.Value <= 0))
        {
            return ApplicationError.Validation("A pinned binding requires the exact published version number.");
        }

        if (request.VersionPolicy != JobTargetVersionPolicy.Pinned && request.PinnedVersion.HasValue)
            return ApplicationError.Validation("Only a pinned binding carries a version number.");

        return null;
    }

    public async Task<ApplicationResult<JobDefinitionDto>> EnableJobAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id, cancellationToken);

        if (job is null)
            return ApplicationResult<JobDefinitionDto>.Failure(ApplicationError.NotFound("Job definition not found."));

        job.Enable();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<JobDefinitionDto>.Success(ToDto(job));
    }

    public async Task<ApplicationResult<JobDefinitionDto>> DisableJobAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id, cancellationToken);

        if (job is null)
            return ApplicationResult<JobDefinitionDto>.Failure(ApplicationError.NotFound("Job definition not found."));

        job.Disable();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<JobDefinitionDto>.Success(ToDto(job));
    }

    public async Task<ApplicationResult<JobDefinitionDto>> UpdateRunStatusAsync(
        Guid id,
        UpdateJobRunStatusRequest request,
        CancellationToken cancellationToken)
    {
        var job = await _dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == id, cancellationToken);

        if (job is null)
            return ApplicationResult<JobDefinitionDto>.Failure(ApplicationError.NotFound("Job definition not found."));

        switch (request.Status)
        {
            case JobRunStatus.Running:
                job.MarkRunning(request.StartedAtUtc);
                break;

            case JobRunStatus.Ok:
                job.MarkSucceeded(
                    durationMs: request.DurationMs,
                    completedAtUtc: request.CompletedAtUtc);
                break;

            case JobRunStatus.Failed:
                job.MarkFailed(
                    failureReason: request.FailureReason ?? "Job failed.",
                    durationMs: request.DurationMs,
                    completedAtUtc: request.CompletedAtUtc);
                break;

            case JobRunStatus.Timeout:
                job.MarkTimedOut(
                    failureReason: request.FailureReason ?? "Job timed out.",
                    durationMs: request.DurationMs,
                    completedAtUtc: request.CompletedAtUtc);
                break;

            case JobRunStatus.NeverRun:
                return ApplicationResult<JobDefinitionDto>.Failure(
                    ApplicationError.Validation("Cannot manually update a job back to NeverRun."));

            default:
                return ApplicationResult<JobDefinitionDto>.Failure(
                    ApplicationError.Validation($"Unsupported job status '{request.Status}'."));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<JobDefinitionDto>.Success(ToDto(job));
    }

    public async Task<ApplicationResult> EnsureSystemJobsSeededAsync(
        CancellationToken cancellationToken)
    {
        var requiredJobs = new[]
        {
            new SeedJobDefinition(
                JobCode: "SYSTEM_IMPORT_QUEUE_PROCESSOR",
                JobName: "Import Queue Processor",
                JobType: JobDefinitionType.CanonicalRefresh,
                ScheduleExpression: "Every 2 minutes",
                Description: "Processes pending import batches and maps staging data into canonical records."),

            new SeedJobDefinition(
                JobCode: "SYSTEM_TELEMETRY_INGESTION",
                JobName: "Telemetry Ingestion Worker",
                JobType: JobDefinitionType.DbLinkImport,
                ScheduleExpression: "Every 2 minutes",
                Description: "Reads available source records into the raw staging layer."),

            new SeedJobDefinition(
                JobCode: "SYSTEM_DATA_QUALITY_SCAN",
                JobName: "Scheduled Data Quality Scan",
                JobType: JobDefinitionType.DataQualityScan,
                ScheduleExpression: "Every 15 minutes",
                Description: "Scans canonical data and staging data for data-quality issues."),

            new SeedJobDefinition(
                JobCode: "SYSTEM_RISK_SCORING",
                JobName: "Scheduled Risk Scoring",
                JobType: JobDefinitionType.RiskScoring,
                ScheduleExpression: "Every 15 minutes",
                Description: "Calculates material/batch quality risk scores from configured rules and models.")
        };

        foreach (var seed in requiredJobs)
        {
            var exists = await _dbContext.JobDefinitions
                .AnyAsync(x => !x.IsDeleted && x.JobCode == seed.JobCode, cancellationToken);

            if (exists)
                continue;

            var job = new JobDefinition(
                jobCode: seed.JobCode,
                jobName: seed.JobName,
                jobType: seed.JobType,
                scheduleExpression: seed.ScheduleExpression,
                isSynthetic: false,
                targetId: null,
                targetType: null,
                isEnabled: true,
                description: seed.Description,
                sourceSystem: "PlantProcessIQ.System",
                sourceRecordId: seed.JobCode);

            JobLaneAssignment.Initialize(job);
        _dbContext.JobDefinitions.Add(job);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult.Success();
    }

    private static ApplicationError? ValidateCreateRequest(CreateJobDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.JobCode))
            return ApplicationError.Validation("Job code is required.");

        if (string.IsNullOrWhiteSpace(request.JobName))
            return ApplicationError.Validation("Job name is required.");

        // T-106 B2.3b. One schedule authority for every write path: a string that nothing
        // can execute produces a job that can never become due, so it is refused at save.
        return JobScheduleWriteValidation.Validate(request.ScheduleExpression);
    }

    private static ApplicationError? ValidateUpdateRequest(UpdateJobDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.JobName))
            return ApplicationError.Validation("Job name is required.");

        return JobScheduleWriteValidation.Validate(request.ScheduleExpression);
    }

    private static JobDefinitionDto ToDto(JobDefinition job)
    {
        return new JobDefinitionDto(
            job.Id,
            job.JobCode,
            job.JobName,
            job.JobType,
            job.TargetId,
            job.TargetType,
            job.ScheduleExpression,
            job.IsEnabled,
            job.LastRunStartedAtUtc,
            job.LastRunCompletedAtUtc,
            job.LastRunDurationMs,
            job.LastRunStatus,
            job.LastFailureReason,
            job.NextRunAtUtc,
            job.Description,
            job.IsSynthetic,
            job.CreatedAtUtc,
            job.UpdatedAtUtc);
    }

    private static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant().Replace(" ", "_");
    }

    private sealed record SeedJobDefinition(
        string JobCode,
        string JobName,
        JobDefinitionType JobType,
        string ScheduleExpression,
        string Description);
}



