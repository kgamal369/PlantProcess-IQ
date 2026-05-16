using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Integration;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Services.Integration;

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

        var existing = await _dbContext.JobDefinitions
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.JobCode == jobCode, cancellationToken);

        if (existing is null)
        {
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
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ApplicationResult<JobDefinitionDto>.Success(ToDto(job));
        }

        existing.UpdateDefinition(
            jobName: request.JobName,
            jobType: request.JobType,
            scheduleExpression: request.ScheduleExpression,
            targetId: request.TargetId,
            targetType: request.TargetType,
            isEnabled: request.IsEnabled,
            description: request.Description);

        if (request.IsEnabled)
            existing.Enable();
        else
            existing.Disable();

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult<JobDefinitionDto>.Success(ToDto(existing));
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