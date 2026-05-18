using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Integration.Services.Jobs;

public sealed class JobRegistrationService : IJobRegistrationService
{
    private readonly IPlantProcessDbContext _dbContext;

    public JobRegistrationService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationResult> RegisterSystemJobsAsync(
        CancellationToken cancellationToken)
    {
        var jobs = new[]
        {
            new UpsertJobDefinitionRequest(
                JobCode: "SYSTEM_TELEMETRY_INGESTION",
                JobName: "Telemetry Ingestion Worker",
                JobType: JobDefinitionType.Custom,
                TargetId: null,
                TargetType: "SystemWorker",
                ScheduleExpression: "Continuous",
                IsEnabled: true,
                Description: "Continuously flushes buffered high-frequency telemetry observations into the database.",
                IsSynthetic: false,
                SourceSystem: "PlantProcessIQ.System",
                SourceRecordId: "SYSTEM_TELEMETRY_INGESTION"),

            new UpsertJobDefinitionRequest(
                JobCode: "SYSTEM_IMPORT_QUEUE_PROCESSOR",
                JobName: "Import Batch Queue Processor",
                JobType: JobDefinitionType.DbLinkImport,
                TargetId: null,
                TargetType: "SystemWorker",
                ScheduleExpression: "Every 2 minutes",
                IsEnabled: true,
                Description: "Processes pending import batches and maps staging data into canonical records.",
                IsSynthetic: false,
                SourceSystem: "PlantProcessIQ.System",
                SourceRecordId: "SYSTEM_IMPORT_QUEUE_PROCESSOR"),

            new UpsertJobDefinitionRequest(
                JobCode: "SYSTEM_DATA_QUALITY_SCAN",
                JobName: "Scheduled Data Quality Scan",
                JobType: JobDefinitionType.DataQualityScan,
                TargetId: null,
                TargetType: "SystemWorker",
                ScheduleExpression: "Every 15 minutes",
                IsEnabled: true,
                Description: "Scans canonical and staging data for data-quality issues.",
                IsSynthetic: false,
                SourceSystem: "PlantProcessIQ.System",
                SourceRecordId: "SYSTEM_DATA_QUALITY_SCAN"),

            new UpsertJobDefinitionRequest(
                JobCode: "SYSTEM_RISK_SCORING",
                JobName: "Scheduled Risk Scoring",
                JobType: JobDefinitionType.RiskScoring,
                TargetId: null,
                TargetType: "SystemWorker",
                ScheduleExpression: "Every 15 minutes",
                IsEnabled: true,
                Description: "Calculates material/process risk scores for recent manufacturing records.",
                IsSynthetic: false,
                SourceSystem: "PlantProcessIQ.System",
                SourceRecordId: "SYSTEM_RISK_SCORING")
        };

        foreach (var job in jobs)
        {
            var result = await UpsertJobAsync(job, cancellationToken);

            if (result.IsFailure)
                return ApplicationResult.Failure(result.Error!);
        }

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<JobDefinitionDto>> UpsertJobAsync(
        UpsertJobDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JobCode))
            return ApplicationResult<JobDefinitionDto>.Failure(ApplicationError.Validation("Job code is required."));

        if (string.IsNullOrWhiteSpace(request.JobName))
            return ApplicationResult<JobDefinitionDto>.Failure(ApplicationError.Validation("Job name is required."));

        if (string.IsNullOrWhiteSpace(request.ScheduleExpression))
            return ApplicationResult<JobDefinitionDto>.Failure(ApplicationError.Validation("Schedule expression is required."));

        var jobCode = NormalizeCode(request.JobCode);
        var nowUtc = DateTime.UtcNow;

        var targetType = string.IsNullOrWhiteSpace(request.TargetType)
            ? null
            : request.TargetType.Trim();

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        /*
         * Important:
         * This uses ExecuteUpdateAsync for existing rows instead of:
         *   load tracked entity -> modify -> SaveChangesAsync
         *
         * Why:
         * API integration tests and future multi-instance deployments can start
         * more than one API process at the same time. Each startup registers the
         * same system jobs. Tracked updates with xmin optimistic concurrency can
         * throw DbUpdateConcurrencyException when two startup scopes touch the
         * same JobDefinition row.
         *
         * ExecuteUpdateAsync performs a direct database update and avoids the
         * stale tracked-entity concurrency problem for idempotent registration.
         */
        var updatedRows = await _dbContext.JobDefinitions
            .Where(x => !x.IsDeleted && x.JobCode == jobCode)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.JobName, request.JobName.Trim())
                    .SetProperty(x => x.JobType, request.JobType)
                    .SetProperty(x => x.TargetId, request.TargetId)
                    .SetProperty(x => x.TargetType, targetType)
                    .SetProperty(x => x.ScheduleExpression, request.ScheduleExpression.Trim())
                    .SetProperty(x => x.IsEnabled, request.IsEnabled)
                    .SetProperty(x => x.Description, description)
                    .SetProperty(x => x.UpdatedAtUtc, nowUtc),
                cancellationToken);

        if (updatedRows > 0)
        {
            var updated = await _dbContext.JobDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => !x.IsDeleted && x.JobCode == jobCode, cancellationToken);

            return updated is null
                ? ApplicationResult<JobDefinitionDto>.Failure(ApplicationError.NotFound($"Job definition '{jobCode}' was not found after update."))
                : ApplicationResult<JobDefinitionDto>.Success(ToDto(updated));
        }

        var job = new JobDefinition(
            jobCode: jobCode,
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

        _dbContext.JobDefinitions.Add(job);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ApplicationResult<JobDefinitionDto>.Success(ToDto(job));
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another API/test instance inserted or updated the same system job first.
        }
        catch (DbUpdateException)
        {
            // Most likely a unique-key race on JobCode during parallel startup.
        }

        var existing = await _dbContext.JobDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.JobCode == jobCode, cancellationToken);

        return existing is null
            ? ApplicationResult<JobDefinitionDto>.Failure(ApplicationError.Conflict($"Job definition '{jobCode}' could not be inserted or reloaded after concurrent upsert."))
            : ApplicationResult<JobDefinitionDto>.Success(ToDto(existing));
    }
    private static string NormalizeCode(string value)
    {
        return value.Trim().ToUpperInvariant().Replace(" ", "_");
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
}






