using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Common.Time;
using PlantProcess.Application.Contracts.Analytics;
using PlantProcess.Application.Services.Analytics.Interfaces;
using PlantProcess.Domain.Entities.Analytics;

namespace PlantProcess.Application.Services.Analytics.Services;

public sealed class RiskScoreService : IRiskScoreService
{
    public const string DefaultRiskType = "OverallQualityRisk";
    public const string DefaultRuleVersion = "rule-risk-v1.0";

    private readonly IPlantProcessDbContext _dbContext;
    private readonly IPlantTimeContextResolver _timeContextResolver;
    private readonly IFeatureEngineeringService _featureEngineeringService;
    private readonly ILogger<RiskScoreService> _logger;

    public RiskScoreService(
        IPlantProcessDbContext dbContext,
        IPlantTimeContextResolver timeContextResolver,
        IFeatureEngineeringService featureEngineeringService,
        ILogger<RiskScoreService> logger)
    {
        _dbContext = dbContext;
        _timeContextResolver = timeContextResolver;
        _featureEngineeringService = featureEngineeringService;
        _logger = logger;
    }

    public async Task<ApplicationResult<Guid>> StoreAsync(
        StoreRiskScoreCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MaterialUnitId == Guid.Empty)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Material unit ID is required."));

        if (string.IsNullOrWhiteSpace(command.RiskType))
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Risk type is required."));

        if (command.Score < 0 || command.Score > 1)
            return ApplicationResult<Guid>.Failure(ApplicationError.Validation("Risk score must be between 0 and 1."));

        var material = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.Id == command.MaterialUnitId && !x.IsDeleted)
            .Select(x => new { x.Id, x.SiteId })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
            return ApplicationResult<Guid>.Failure(ApplicationError.NotFound("Material unit does not exist."));

        var siteTimeZoneId = await _dbContext.Sites
            .AsNoTracking()
            .Where(x => x.Id == material.SiteId)
            .Select(x => x.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        var timeContext = _timeContextResolver.Resolve(command.PlantTimeZoneId ?? siteTimeZoneId, DateTime.UtcNow);
        var riskClass = string.IsNullOrWhiteSpace(command.RiskClass) ? CalculateRiskClass(command.Score) : command.RiskClass.Trim();

        var riskScore = new RiskScore(
            materialUnitId: command.MaterialUnitId,
            riskType: command.RiskType,
            score: command.Score,
            isSynthetic: command.Metadata.IsSynthetic,
            riskClass: riskClass,
            mainContributorsJson: command.MainContributorsJson,
            modelVersion: command.ModelVersion,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId,
            plantTimeZoneId: command.PlantTimeZoneId ?? timeContext.TimeZoneId,
            plantUtcOffsetMinutes: command.PlantUtcOffsetMinutes ?? timeContext.UtcOffsetMinutes);

        _dbContext.RiskScores.Add(riskScore);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Stored risk score. RiskScoreId={RiskScoreId}, MaterialUnitId={MaterialUnitId}, RiskType={RiskType}, Score={Score}, RiskClass={RiskClass}, CorrelationId={CorrelationId}",
            riskScore.Id,
            riskScore.MaterialUnitId,
            riskScore.RiskType,
            riskScore.Score,
            riskScore.RiskClass,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(riskScore.Id);
    }

    public async Task<ApplicationResult<CalculateRiskScoreResult>> CalculateAsync(
        CalculateRiskScoreCommand command,
        CancellationToken cancellationToken)
    {
        if (command.MaterialUnitId == Guid.Empty)
            return ApplicationResult<CalculateRiskScoreResult>.Failure(ApplicationError.Validation("Material unit ID is required."));

        var riskType = string.IsNullOrWhiteSpace(command.RiskType) ? DefaultRiskType : command.RiskType.Trim();
        var modelVersion = string.IsNullOrWhiteSpace(command.ModelVersion) ? DefaultRuleVersion : command.ModelVersion.Trim();

        await EnsureDefaultModelRegistryAsync(riskType, modelVersion, cancellationToken);

        var featureResult = await _featureEngineeringService.BuildMaterialFeatureVectorAsync(command.MaterialUnitId, cancellationToken);
        if (featureResult.IsFailure || featureResult.Value is null)
            return ApplicationResult<CalculateRiskScoreResult>.Failure(featureResult.Error ?? ApplicationError.Unexpected("Feature vector could not be built."));

        var vector = featureResult.Value;
        var contributors = new List<RiskContributorDto>();
        decimal score = 0.05m;

        AddContributorIfPositive(contributors, "DataQuality", "DQ_ISSUES", "Open data-quality issues", 0.12m, Normalize(vector.DataQualityIssueCount, 0, 10), "increase", $"{vector.DataQualityIssueCount} data-quality issue(s) are linked to this material.");
        AddContributorIfPositive(contributors, "Process", "ABORTED_STEPS", "Aborted or abnormal process steps", 0.16m, Normalize(vector.AbortedProcessStepCount, 0, 3), "increase", $"{vector.AbortedProcessStepCount} process step(s) are aborted.");
        AddContributorIfPositive(contributors, "Downtime", "DOWNTIME_MINUTES", "Downtime and delay exposure", 0.14m, Normalize(vector.TotalDowntimeMinutes, 0, 180), "increase", $"Total downtime/delay exposure is {vector.TotalDowntimeMinutes:N1} minutes.");
        AddContributorIfPositive(contributors, "Parameter", "PARAMETER_ANOMALY_RATIO", "Parameter values outside expected range", 0.28m, Clamp01(vector.ParameterAnomalyRatio), "increase", $"Parameter anomaly ratio is {vector.ParameterAnomalyRatio:P1}.");
        AddContributorIfPositive(contributors, "Quality", "EXISTING_QUALITY_SIGNALS", "Existing inspection/quality signals", 0.18m, Normalize(vector.DefectEventCount, 0, 5), "increase", $"{vector.DefectEventCount} defect/quality signal(s) exist for this material.");
        AddContributorIfPositive(contributors, "Correlation", "CORRELATION_HINTS", "Matched historical correlation hints", 0.12m, Normalize(vector.CorrelationHints.Count, 0, 5), "increase", $"{vector.CorrelationHints.Count} correlation hint(s) match the material's feature/outcome context.");

        foreach (var parameter in vector.ParameterAggregates.OrderByDescending(x => x.AnomalyRatio).Take(5))
        {
            if (parameter.AnomalyRatio <= 0) continue;

            AddContributorIfPositive(
                contributors,
                "Parameter",
                parameter.ParameterCode,
                parameter.ParameterName,
                0.05m,
                Clamp01(parameter.AnomalyRatio),
                "increase",
                $"Parameter {parameter.ParameterCode} has anomaly ratio {parameter.AnomalyRatio:P1} based on expected limits / missing values / quality flags.");
        }

        foreach (var contributor in contributors)
            score += contributor.Contribution;

        score = Math.Round(Clamp01(score), 6);
        var riskClass = CalculateRiskClass(score);
        var contributorsJson = JsonSerializer.Serialize(contributors, JsonOptions);

        Guid? riskScoreId = null;
        if (command.StoreResult)
        {
            var material = await _dbContext.MaterialUnits
                .AsNoTracking()
                .Where(x => x.Id == command.MaterialUnitId)
                .Select(x => new { x.SiteId, x.IsSynthetic })
                .FirstAsync(cancellationToken);

            var siteTimeZoneId = await _dbContext.Sites
                .AsNoTracking()
                .Where(x => x.Id == material.SiteId)
                .Select(x => x.TimeZoneId)
                .FirstOrDefaultAsync(cancellationToken);

            var timeContext = _timeContextResolver.Resolve(command.PlantTimeZoneId ?? siteTimeZoneId, DateTime.UtcNow);

            var riskScore = new RiskScore(
                materialUnitId: command.MaterialUnitId,
                riskType: riskType,
                score: score,
                isSynthetic: material.IsSynthetic,
                riskClass: riskClass,
                mainContributorsJson: contributorsJson,
                modelVersion: modelVersion,
                sourceSystem: "PlantProcessIQ.RuleScorer",
                sourceRecordId: command.CorrelationId,
                plantTimeZoneId: command.PlantTimeZoneId ?? timeContext.TimeZoneId,
                plantUtcOffsetMinutes: command.PlantUtcOffsetMinutes ?? timeContext.UtcOffsetMinutes);

            _dbContext.RiskScores.Add(riskScore);
            await _dbContext.SaveChangesAsync(cancellationToken);
            riskScoreId = riskScore.Id;
        }

        var result = new CalculateRiskScoreResult(
            riskScoreId,
            command.MaterialUnitId,
            riskType,
            score,
            riskClass,
            modelVersion,
            "TransparentRuleBasedScorer",
            contributorsJson,
            DateTime.UtcNow,
            command.StoreResult,
            vector,
            contributors.OrderByDescending(x => x.Contribution).ToList());

        _logger.LogInformation(
            "Calculated quality risk. MaterialUnitId={MaterialUnitId}, RiskType={RiskType}, Score={Score}, RiskClass={RiskClass}, Stored={Stored}, RiskScoreId={RiskScoreId}",
            command.MaterialUnitId,
            riskType,
            score,
            riskClass,
            command.StoreResult,
            riskScoreId);

        return ApplicationResult<CalculateRiskScoreResult>.Success(result);
    }

    public async Task<ApplicationResult<CalculateRiskScoresBatchResult>> CalculateBatchAsync(
        CalculateRiskScoresBatchCommand command,
        CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        var riskType = string.IsNullOrWhiteSpace(command.RiskType) ? DefaultRiskType : command.RiskType.Trim();
        var maxMaterials = Math.Clamp(command.MaxMaterials, 1, 5000);
        var warnings = new List<string>();
        var results = new List<CalculateRiskScoreResult>();

        var query = _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (command.SiteId.HasValue)
            query = query.Where(x => x.SiteId == command.SiteId.Value);

        var materialIds = await query
            .OrderByDescending(x => x.ProductionStartUtc ?? x.CreatedAtUtc)
            .Take(maxMaterials)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var materialId in materialIds)
        {
            var result = await CalculateAsync(
                new CalculateRiskScoreCommand(
                    materialId,
                    riskType,
                    DefaultRuleVersion,
                    null,
                    null,
                    command.StoreResult,
                    command.RequestedBy,
                    command.CorrelationId),
                cancellationToken);

            if (result.IsSuccess && result.Value is not null)
            {
                results.Add(result.Value);
            }
            else
            {
                warnings.Add($"Material {materialId}: {result.Error?.Message ?? "Unknown risk calculation error"}");
            }
        }

        var completed = DateTime.UtcNow;
        var batchResult = new CalculateRiskScoresBatchResult(
            materialIds.Count,
            results.Count,
            results.Count(x => x.Stored),
            warnings.Count,
            started,
            completed,
            completed - started,
            results,
            warnings);

        _logger.LogInformation(
            "Batch risk scoring completed. Candidates={Candidates}, Calculated={Calculated}, Stored={Stored}, Skipped={Skipped}, DurationMs={DurationMs}",
            batchResult.CandidatesScanned,
            batchResult.ScoresCalculated,
            batchResult.ScoresStored,
            batchResult.Skipped,
            (long)batchResult.Duration.TotalMilliseconds);

        return ApplicationResult<CalculateRiskScoresBatchResult>.Success(batchResult);
    }

    private async Task EnsureDefaultModelRegistryAsync(string riskType, string modelVersion, CancellationToken cancellationToken)
    {
        var exists = await _dbContext.ModelRegistries
            .AnyAsync(x => x.RiskType == riskType && x.ModelVersion == modelVersion, cancellationToken);

        if (exists) return;

        _dbContext.ModelRegistries.Add(new ModelRegistry(
            modelCode: $"RULE-{riskType.ToUpperInvariant()}",
            modelName: "Transparent rule-based quality risk scorer",
            modelType: "RuleBased",
            modelVersion: modelVersion,
            riskType: riskType,
            isSynthetic: true,
            description: "Initial explainable heuristic scorer used before ML/ONNX integration.",
            artifactUri: null,
            trainingDataSummaryJson: "{\"stage\":\"rule-based-baseline\",\"trainingRequired\":false}",
            metricsJson: "{\"baseline\":true,\"explainable\":true}",
            sourceSystem: "PlantProcessIQ",
            sourceRecordId: "phase-h-default-model"));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void AddContributorIfPositive(
        List<RiskContributorDto> contributors,
        string contributorType,
        string contributorCode,
        string contributorName,
        decimal weight,
        decimal normalizedSignal,
        string direction,
        string explanation)
    {
        var contribution = Math.Round(weight * Clamp01(normalizedSignal), 6);
        if (contribution <= 0) return;

        contributors.Add(new RiskContributorDto(
            contributorType,
            contributorCode,
            contributorName,
            weight,
            contribution,
            direction,
            explanation));
    }

    private static decimal Normalize(decimal value, decimal min, decimal max)
    {
        if (max <= min) return 0m;
        return Clamp01((value - min) / (max - min));
    }

    private static decimal Clamp01(decimal value)
    {
        if (value < 0m) return 0m;
        if (value > 1m) return 1m;
        return value;
    }

    private static string CalculateRiskClass(decimal score)
    {
        if (score >= 0.70m) return "High";
        if (score >= 0.40m) return "Medium";
        return "Low";
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}
