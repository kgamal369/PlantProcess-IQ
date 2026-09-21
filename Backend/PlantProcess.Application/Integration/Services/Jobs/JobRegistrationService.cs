using PlantProcess.Application.Jobs.Admission;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Application.Jobs.Scheduling;
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
        // T-106 B2.3b census convergence. The Worker dispatches four job codes; two of them
        // had no JobDefinition authority at all, so nothing could schedule, disable, monitor
        // or audit them. They are registered here, in the one canonical catalogue, with the
        // schedules the Worker already runs them on by default.
        // JobType for both is DbLinkImport, the executable import family the orchestrator
        // already commissions for this work. Confirm that classification.
        new UpsertJobDefinitionRequest(
            JobCode: "SYSTEM_IMPORT_QUEUE_PROCESSOR",
            JobName: "Import Queue Processor Worker",
            // CENTRAL ruling: this job maps staged batches into canonical records. It is not a
            // raw source reader, so it belongs to the CanonicalRefresh family, matching the
            // historical canonical seed. Corrected forward from b212406b, not rewritten.
            JobType: JobDefinitionType.CanonicalRefresh,
            TargetId: null,
            TargetType: "SystemWorker",
            ScheduleExpression: "Every 2 minutes",
            IsEnabled: true,
            Description: "Processes pending import batches from the staging queue. Dispatched by the Worker host.",
            IsSynthetic: false,
            SourceSystem: "PlantProcessIQ.System",
            SourceRecordId: "SYSTEM_IMPORT_QUEUE_PROCESSOR"),

        new UpsertJobDefinitionRequest(
            JobCode: "SYSTEM_DELTA_IMPORT_JOB",
            JobName: "Delta Import Worker",
            JobType: JobDefinitionType.DbLinkImport,
            TargetId: null,
            TargetType: "SystemWorker",
            ScheduleExpression: "Every 5 minutes",
            IsEnabled: true,
            Description: "Reads incremental source rows since the recorded cursor into staging. Dispatched by the Worker host.",
            IsSynthetic: false,
            SourceSystem: "PlantProcessIQ.System",
            SourceRecordId: "SYSTEM_DELTA_IMPORT_JOB"),

        new UpsertJobDefinitionRequest(
            JobCode: "SYSTEM_SOURCE_SNAPSHOT",
            JobName: "Source Snapshot Worker",
            JobType: JobDefinitionType.DbLinkImport,
            TargetId: null,
            TargetType: "SystemWorker",
            ScheduleExpression: "Every 2 minutes",
            IsEnabled: true,
            Description: "Reads configured source systems/files into the raw staging layer. This is source -> staging only.",
            IsSynthetic: false,
            SourceSystem: "PlantProcessIQ.System",
            SourceRecordId: "SYSTEM_SOURCE_SNAPSHOT"),

        new UpsertJobDefinitionRequest(
            JobCode: "SYSTEM_CANONICAL_MAPPING",
            JobName: "Canonical Mapping Worker",
            JobType: JobDefinitionType.CanonicalRefresh,
            TargetId: null,
            TargetType: "SystemWorker",
            ScheduleExpression: "Every 2 minutes",
            IsEnabled: true,
            Description: "Maps staged records into canonical MaterialUnit, ProcessStepExecution, ParameterObservation, QualityEvent and Genealogy records.",
            IsSynthetic: false,
            SourceSystem: "PlantProcessIQ.System",
            SourceRecordId: "SYSTEM_CANONICAL_MAPPING"),

        new UpsertJobDefinitionRequest(
            JobCode: "SYSTEM_DATA_QUALITY_SCAN",
            JobName: "Scheduled Data Quality Scan",
            JobType: JobDefinitionType.DataQualityScan,
            TargetId: null,
            TargetType: "SystemWorker",
            ScheduleExpression: "Every 15 minutes",
            IsEnabled: true,
            Description: "Scans canonical and staging data for missing IDs, invalid timestamps, broken genealogy, duplicates and inconsistent quality records.",
            IsSynthetic: false,
            SourceSystem: "PlantProcessIQ.System",
            SourceRecordId: "SYSTEM_DATA_QUALITY_SCAN"),

        new UpsertJobDefinitionRequest(
            JobCode: "SYSTEM_RISK_SCORING",
            JobName: "Scheduled Rule-Based Risk Scoring",
            JobType: JobDefinitionType.RiskScoring,
            TargetId: null,
            TargetType: "SystemWorker",
            ScheduleExpression: "Every 15 minutes",
            IsEnabled: true,
            Description: "Calculates transparent rule-based quality risk scores for recent material/batch records.",
            IsSynthetic: false,
            SourceSystem: "PlantProcessIQ.System",
            SourceRecordId: "SYSTEM_RISK_SCORING"),

        new UpsertJobDefinitionRequest(
            JobCode: "SYSTEM_ML_PARAMS_VS_DEFECTS",
            JobName: "Correlation Learning - Parameters vs Defects",
            JobType: JobDefinitionType.MlParamsVsDefects,
            TargetId: null,
            TargetType: "SystemWorker",
            ScheduleExpression: "Daily 02:00",
            IsEnabled: false,
            Description: "Future learning job: analyzes process parameters against configured defect outcomes. Disabled until ML phase.",
            IsSynthetic: false,
            SourceSystem: "PlantProcessIQ.System",
            SourceRecordId: "SYSTEM_ML_PARAMS_VS_DEFECTS"),

        new UpsertJobDefinitionRequest(
            JobCode: "SYSTEM_ML_PARAMS_VS_DOWNTIME",
            JobName: "Correlation Learning - Parameters vs Downtime",
            JobType: JobDefinitionType.MlParamsVsDowntime,
            TargetId: null,
            TargetType: "SystemWorker",
            ScheduleExpression: "Daily 02:30",
            IsEnabled: false,
            Description: "Future learning job: analyzes process parameters against downtime outcomes. Disabled until ML phase.",
            IsSynthetic: false,
            SourceSystem: "PlantProcessIQ.System",
            SourceRecordId: "SYSTEM_ML_PARAMS_VS_DOWNTIME"),

        new UpsertJobDefinitionRequest(
            JobCode: "SYSTEM_ML_PARAMS_VS_KPIS",
            JobName: "Correlation Learning - Parameters vs KPIs",
            JobType: JobDefinitionType.MlParamsVsKpis,
            TargetId: null,
            TargetType: "SystemWorker",
            ScheduleExpression: "Daily 03:00",
            IsEnabled: false,
            Description: "Future learning job: analyzes process parameters against configured KPIs. Disabled until ML phase.",
            IsSynthetic: false,
            SourceSystem: "PlantProcessIQ.System",
            SourceRecordId: "SYSTEM_ML_PARAMS_VS_KPIS"),

        new UpsertJobDefinitionRequest(
            JobCode: "SYSTEM_ML_WEEKLY_FULL",
            JobName: "Weekly Full Learning Refresh",
            JobType: JobDefinitionType.MlWeeklyFull,
            TargetId: null,
            TargetType: "SystemWorker",
            ScheduleExpression: "Weekly Sunday 03:00",
            IsEnabled: false,
            Description: "Future full learning refresh. Disabled until ML phase.",
            IsSynthetic: false,
            SourceSystem: "PlantProcessIQ.System",
            SourceRecordId: "SYSTEM_ML_WEEKLY_FULL"),

        new UpsertJobDefinitionRequest(
            JobCode: "SYSTEM_DASHBOARD_READ_MODEL_REFRESH",
            JobName: "Dashboard Read Model Refresh",
            JobType: JobDefinitionType.Custom,
            TargetId: null,
            TargetType: "SystemWorker",
            ScheduleExpression: "Every 15 minutes",
            IsEnabled: true,
            Description: "Refreshes dashboard-facing read models/materialized views after import and canonical mapping.",
            IsSynthetic: false,
            SourceSystem: "PlantProcessIQ.System",
            SourceRecordId: "SYSTEM_DASHBOARD_READ_MODEL_REFRESH")
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

        // T-106 B2.3b. System registration is a write path like any other: it cannot
        // write a schedule the scheduler could never execute.
        ApplicationError? scheduleError = JobScheduleWriteValidation.Validate(request.ScheduleExpression);
        if (scheduleError is not null)
            return ApplicationResult<JobDefinitionDto>.Failure(scheduleError);

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
        var expectedPool = JobLaneAssignment.DefaultFor(request.JobType);
        var updatedRows = await _dbContext.JobDefinitions
            .Where(x => !x.IsDeleted && x.JobCode == jobCode
                && (expectedPool == null || x.PoolCode == null || x.PoolCode == expectedPool))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.JobName, request.JobName.Trim())
                    .SetProperty(x => x.JobType, request.JobType)
                    .SetProperty(x => x.PoolCode, x => x.PoolCode ?? expectedPool)
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

        if (await _dbContext.JobDefinitions.AnyAsync(x => !x.IsDeleted && x.JobCode == jobCode, cancellationToken))
            return ApplicationResult<JobDefinitionDto>.Failure(ApplicationError.Validation(
                "Registration cannot change an explicitly assigned incompatible execution pool."));

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

        JobLaneAssignment.Initialize(job);
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






