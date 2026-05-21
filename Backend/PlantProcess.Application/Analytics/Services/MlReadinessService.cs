using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Analytics.Services;

public sealed class MlReadinessService : IMlReadinessService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IQualityLabelBuilderService _labelBuilderService;

    public MlReadinessService(
        IPlantProcessDbContext dbContext,
        IQualityLabelBuilderService labelBuilderService)
    {
        _dbContext = dbContext;
        _labelBuilderService = labelBuilderService;
    }

    public async Task<MlReadinessScoreDto> GetReadinessAsync(
        CancellationToken cancellationToken = default)
    {
        var materialCount = await _dbContext.MaterialUnits
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var parameterObservationCount = await _dbContext.ParameterObservations
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var qualityEventCount = await _dbContext.QualityEvents
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var labeledOutcomeCount = await _labelBuilderService
            .CountLabeledMaterialsAsync(cancellationToken);

        var defectFamilies = await CountDefectFamiliesAsync(cancellationToken);

        var genealogyEdges = await _dbContext.GenealogyEdges
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var materialsWithGenealogy = await _dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .SelectMany(x => new[] { x.ParentMaterialUnitId, x.ChildMaterialUnitId })
            .Distinct()
            .CountAsync(cancellationToken);

        var genealogyCoveragePercent = materialCount == 0
            ? 0m
            : Math.Round(materialsWithGenealogy * 100m / materialCount, 2);

        var missingOrInvalidObservationCount = await _dbContext.ParameterObservations
            .AsNoTracking()
            .CountAsync(
                x => !x.IsDeleted
                     && (x.QualityFlag == "Missing"
                         || x.QualityFlag == "Invalid"
                         || x.QualityFlag == "Outlier"
                         || x.NumericValue == null),
                cancellationToken);

        var missingCriticalPercent = parameterObservationCount == 0
            ? 100m
            : Math.Round(missingOrInvalidObservationCount * 100m / parameterObservationCount, 2);

        var firstDataAtUtc = await GetFirstDataAtUtcAsync(cancellationToken);
        var lastDataAtUtc = await GetLastDataAtUtcAsync(cancellationToken);

        var timeRangeDays = firstDataAtUtc.HasValue && lastDataAtUtc.HasValue
            ? Math.Max(0m, Math.Round((decimal)(lastDataAtUtc.Value - firstDataAtUtc.Value).TotalDays, 2))
            : 0m;

        var metrics = new List<MlReadinessMetricDto>
        {
            Metric(
                "PARAMETER_OBSERVATIONS",
                "Parameter observations",
                parameterObservationCount,
                1000,
                "rows",
                "ML needs enough process signal rows before learning can start."),

            Metric(
                "QUALITY_EVENTS",
                "Quality events",
                qualityEventCount,
                200,
                "events",
                "ML needs enough quality outcomes to learn process-to-quality relationships."),

            Metric(
                "LABELED_OUTCOMES",
                "Labeled material outcomes",
                labeledOutcomeCount,
                100,
                "materials",
                "Quality labels must connect material, genealogy, and quality outcomes."),

            Metric(
                "DEFECT_FAMILIES",
                "Defect families",
                defectFamilies,
                2,
                "families",
                "At least two defect/outcome families are needed for useful classification."),

            Metric(
                "GENEALOGY_COVERAGE",
                "Genealogy coverage",
                genealogyCoveragePercent,
                70,
                "%",
                "ML needs upstream/downstream material relationships to explain suspected contributors."),

            MetricMax(
                "MISSING_CRITICAL_PARAMETERS",
                "Missing / invalid parameter rate",
                missingCriticalPercent,
                20,
                "%",
                "Too much missing or invalid process data will create unreliable training data."),

            Metric(
                "TIME_RANGE",
                "Historical time range",
                timeRangeDays,
                30,
                "days",
                "A wider time window reduces the risk of learning from a short abnormal period.")
        };

        var blockers = metrics
            .Where(x => !x.IsReady)
            .Select(x => $"{x.Name}: {x.Message}")
            .ToList();

        var scorePercent = Math.Round(metrics.Count(x => x.IsReady) * 100m / metrics.Count, 2);

        var canStartTraining = metrics.All(x => x.IsReady);

        var nextActions = canStartTraining
            ? new List<string>
            {
                "Review feature vector quality with a process engineer.",
                "Select ML execution architecture: ML.NET or Python microservice.",
                "Run an offline training experiment only after data-owner approval."
            }
            : new List<string>
            {
                "Complete source snapshot → staging → schema mapping → canonical import.",
                "Increase labeled quality outcomes and defect family coverage.",
                "Reduce missing/invalid critical process parameters.",
                "Keep ML jobs disabled until readiness score is green."
            };

        return new MlReadinessScoreDto(
            GeneratedAtUtc: DateTime.UtcNow,
            OverallStatus: canStartTraining ? "ReadyForOfflineExperiment" : "NotReadyForTraining",
            ScorePercent: scorePercent,
            CanStartTraining: canStartTraining,
            TrainingStatus: canStartTraining
                ? "Offline training experiment can be planned after technical review."
                : "Training disabled. Waiting for validated labeled historical data.",
            HonestPositioning:
                "No trained production ML model is active. Current intelligence is rule-based risk scoring, correlation analysis, and suspected contributor ranking.",
            Metrics: metrics,
            Blockers: blockers,
            NextActions: nextActions);
    }

    public async Task<IReadOnlyList<MlJobReadinessDto>> GetMlJobsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureMlJobDefinitionsAsync(cancellationToken);

        var jobTypes = MlJobTypes();

        var jobs = await _dbContext.JobDefinitions
            .AsNoTracking()
            .Where(x => !x.IsDeleted && jobTypes.Contains(x.JobType))
            .OrderBy(x => x.JobType)
            .ThenBy(x => x.JobCode)
            .ToListAsync(cancellationToken);

        return jobs
            .Select(job => new MlJobReadinessDto(
                JobId: job.Id,
                JobCode: job.JobCode,
                JobName: job.JobName,
                JobType: job.JobType.ToString(),
                IsEnabled: job.IsEnabled,
                LastRunStatus: job.LastRunStatus.ToString(),
                ScheduleExpression: job.ScheduleExpression,
                ReadinessStatus: job.IsEnabled ? "Enabled" : "PlannedDisabled",
                Reason: job.IsEnabled
                    ? "Enabled by operator. Confirm readiness before running."
                    : "Disabled by design until labeled canonical data is ready."))
            .ToList();
    }

    public async Task EnsureMlJobDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var requiredJobs = new[]
        {
            new
            {
                Code = "SYSTEM_ML_PARAMS_VS_DEFECTS",
                Name = "ML readiness — parameters vs defects",
                Type = JobDefinitionType.MlParamsVsDefects,
                Schedule = "Daily 02:00",
                Description = "Planned ML learning job. Disabled until readiness gates are green."
            },
            new
            {
                Code = "SYSTEM_ML_PARAMS_VS_DOWNTIME",
                Name = "ML readiness — parameters vs downtime",
                Type = JobDefinitionType.MlParamsVsDowntime,
                Schedule = "Daily 02:30",
                Description = "Planned ML learning job. Disabled until readiness gates are green."
            },
            new
            {
                Code = "SYSTEM_ML_PARAMS_VS_KPIS",
                Name = "ML readiness — parameters vs KPIs",
                Type = JobDefinitionType.MlParamsVsKpis,
                Schedule = "Daily 03:00",
                Description = "Planned ML learning job. Disabled until readiness gates are green."
            },
            new
            {
                Code = "SYSTEM_ML_WEEKLY_FULL",
                Name = "ML readiness — weekly full learning",
                Type = JobDefinitionType.MlWeeklyFull,
                Schedule = "Weekly Sunday 03:00",
                Description = "Planned weekly ML learning job. Disabled until readiness gates are green."
            }
        };

        foreach (var required in requiredJobs)
        {
            var exists = await _dbContext.JobDefinitions
                .AnyAsync(
                    x => !x.IsDeleted && x.JobCode == required.Code,
                    cancellationToken);

            if (exists)
                continue;

            var job = new JobDefinition(
                jobCode: required.Code,
                jobName: required.Name,
                jobType: required.Type,
                scheduleExpression: required.Schedule,
                isSynthetic: false,
                targetId: null,
                targetType: "ML_READINESS",
                isEnabled: false,
                description: required.Description,
                sourceSystem: "PlantProcessIQ.System",
                sourceRecordId: required.Code);

            _dbContext.JobDefinitions.Add(job);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MlWorkspaceReadinessDto> GetWorkspaceAsync(
        int labelPreviewLimit,
        CancellationToken cancellationToken = default)
    {
        var readiness = await GetReadinessAsync(cancellationToken);
        var labelPreview = await _labelBuilderService.BuildPreviewAsync(
            labelPreviewLimit,
            cancellationToken);
        var jobs = await GetMlJobsAsync(cancellationToken);

        var models = await _dbContext.ModelRegistries
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.RegisteredAtUtc)
            .Take(25)
            .Select(x => new ModelRegistryLifecycleDto(
                x.Id,
                x.ModelCode,
                x.ModelName,
                x.ModelType,
                x.ModelVersion,
                x.RiskType,
                x.IsActive,
                x.ModelType == "RuleBased"
                    ? "ApprovedForDemo"
                    : readiness.CanStartTraining
                        ? "ReadyForReview"
                        : "DataNotReady",
                x.ModelType == "RuleBased"
                    ? "Transparent rule-based model is allowed for current demo."
                    : "ML model lifecycle remains governed and cannot be presented as production ML until data readiness and validation are complete.",
                x.RegisteredAtUtc))
            .ToListAsync(cancellationToken);

        var correlations = await _dbContext.CorrelationResults
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.CalculatedAtUtc)
            .Take(25)
            .Select(x => new CorrelationLifecycleDto(
                x.Id,
                x.CorrelationType,
                x.SubjectCode,
                x.OutcomeCode,
                x.Score,
                "EvidenceForReview",
                "Correlation result is a suspected-contributor signal, not guaranteed root cause proof.",
                x.CalculatedAtUtc))
            .ToListAsync(cancellationToken);

        return new MlWorkspaceReadinessDto(
            GeneratedAtUtc: DateTime.UtcNow,
            Readiness: readiness,
            LabelPreview: labelPreview,
            MlJobs: jobs,
            ModelRegistry: models,
            Correlations: correlations,
            CurrentIntelligence:
                "Current intelligence uses rule-based risk scoring, data-quality scanning, statistical correlation, and investigation workflows.",
            FutureMlLifecycle:
                "Future ML lifecycle: schema mapping → canonical data → feature vectors → labels → readiness score → offline training → model registry → dashboard/report integration.",
            Disclaimer:
                "No trained production ML model is active. Do not claim AI prediction or guaranteed root cause.");
    }

    private static IReadOnlyList<JobDefinitionType> MlJobTypes()
    {
        return new[]
        {
            JobDefinitionType.MlParamsVsDefects,
            JobDefinitionType.MlParamsVsDowntime,
            JobDefinitionType.MlParamsVsKpis,
            JobDefinitionType.MlWeeklyFull
        };
    }

    private async Task<int> CountDefectFamiliesAsync(CancellationToken cancellationToken)
    {
        var defectCatalogIds = await _dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.DefectCatalogId.HasValue)
            .Select(x => x.DefectCatalogId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (defectCatalogIds.Count == 0)
            return 0;

        return await _dbContext.DefectCatalogs
            .AsNoTracking()
            .Where(x => !x.IsDeleted && defectCatalogIds.Contains(x.Id))
            .Select(x => x.DefectCategory ?? x.DefectCode)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private async Task<DateTime?> GetFirstDataAtUtcAsync(CancellationToken cancellationToken)
    {
        var firstObservation = await _dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => (DateTime?)x.ObservedAtUtc)
            .MinAsync(cancellationToken);

        var firstQuality = await _dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => (DateTime?)x.EventAtUtc)
            .MinAsync(cancellationToken);

        return Min(firstObservation, firstQuality);
    }

    private async Task<DateTime?> GetLastDataAtUtcAsync(CancellationToken cancellationToken)
    {
        var lastObservation = await _dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => (DateTime?)x.ObservedAtUtc)
            .MaxAsync(cancellationToken);

        var lastQuality = await _dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => (DateTime?)x.EventAtUtc)
            .MaxAsync(cancellationToken);

        return Max(lastObservation, lastQuality);
    }

    private static DateTime? Min(DateTime? left, DateTime? right)
    {
        if (!left.HasValue) return right;
        if (!right.HasValue) return left;
        return left.Value <= right.Value ? left : right;
    }

    private static DateTime? Max(DateTime? left, DateTime? right)
    {
        if (!left.HasValue) return right;
        if (!right.HasValue) return left;
        return left.Value >= right.Value ? left : right;
    }

    private static MlReadinessMetricDto Metric(
        string code,
        string name,
        decimal current,
        decimal required,
        string unit,
        string message)
    {
        var isReady = current >= required;

        return new MlReadinessMetricDto(
            Code: code,
            Name: name,
            CurrentValue: current,
            RequiredValue: required,
            Unit: unit,
            IsReady: isReady,
            Status: isReady ? "Ready" : "NotReady",
            Message: message);
    }

    private static MlReadinessMetricDto MetricMax(
        string code,
        string name,
        decimal current,
        decimal maximumAllowed,
        string unit,
        string message)
    {
        var isReady = current <= maximumAllowed;

        return new MlReadinessMetricDto(
            Code: code,
            Name: name,
            CurrentValue: current,
            RequiredValue: maximumAllowed,
            Unit: unit,
            IsReady: isReady,
            Status: isReady ? "Ready" : "NotReady",
            Message: message);
    }
}