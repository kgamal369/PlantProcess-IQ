using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;

namespace PlantProcess.Application.Services.Analytics;

public sealed class DashboardQueryService : IDashboardQueryService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<DashboardQueryService> _logger;

    public DashboardQueryService(
        IPlantProcessDbContext dbContext,
        ILogger<DashboardQueryService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApplicationResult<DashboardOverviewDto>> GetOverviewAsync(Guid? siteId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var range = NormalizeRange(fromUtc, toUtc);
        var materialIds = await GetMaterialIdsAsync(siteId, range.FromUtc, range.ToUtc, cancellationToken);
        var materialIdSet = materialIds.ToHashSet();

        var qualityEvents = await _dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => !x.IsDeleted && materialIdSet.Contains(x.MaterialUnitId) && x.EventAtUtc >= range.FromUtc && x.EventAtUtc <= range.ToUtc)
            .Select(x => new { x.MaterialUnitId, x.EventType, x.DefectCatalogId, x.EventAtUtc })
            .ToListAsync(cancellationToken);

        var defectEvents = qualityEvents.Count(x => x.EventType.Equals("Defect", StringComparison.OrdinalIgnoreCase) || x.DefectCatalogId.HasValue);
        var riskScores = await _dbContext.RiskScores
            .AsNoTracking()
            .Where(x => !x.IsDeleted && materialIdSet.Contains(x.MaterialUnitId))
            .Select(x => new { x.MaterialUnitId, x.Score, x.RiskClass, x.MainContributorsJson })
            .ToListAsync(cancellationToken);

        var highRiskMaterials = riskScores.Where(x => x.Score >= 0.70m || (x.RiskClass ?? "").Equals("High", StringComparison.OrdinalIgnoreCase)).Select(x => x.MaterialUnitId).Distinct().Count();
        var processSteps = await _dbContext.ProcessStepExecutions.AsNoTracking().CountAsync(x => !x.IsDeleted && materialIdSet.Contains(x.MaterialUnitId), cancellationToken);
        var parameterObservations = await _dbContext.ParameterObservations.AsNoTracking().CountAsync(x => !x.IsDeleted && materialIdSet.Contains(x.MaterialUnitId), cancellationToken);
        var dataQualityIssues = await _dbContext.DataQualityIssues.AsNoTracking().CountAsync(x => !x.IsDeleted && x.MaterialUnitId.HasValue && materialIdSet.Contains(x.MaterialUnitId.Value), cancellationToken);
        var correlationResults = await _dbContext.CorrelationResults.AsNoTracking().CountAsync(x => !x.IsDeleted, cancellationToken);
        var sites = siteId.HasValue ? 1 : await _dbContext.Sites.AsNoTracking().CountAsync(x => !x.IsDeleted, cancellationToken);

        var materialsCount = Math.Max(materialIds.Count, 1);
        var defectRate = Math.Round((decimal)defectEvents / materialsCount * 100m, 2);
        var highRiskRate = Math.Round((decimal)highRiskMaterials / materialsCount * 100m, 2);

        var trend = qualityEvents
            .GroupBy(x => x.EventAtUtc.Date)
            .OrderBy(x => x.Key)
            .Select(x => new DashboardTrendPointDto(
                DateTime.SpecifyKind(x.Key, DateTimeKind.Utc),
                materialIds.Count,
                x.Count(),
                x.Count(e => e.EventType.Equals("Defect", StringComparison.OrdinalIgnoreCase) || e.DefectCatalogId.HasValue),
                Math.Round((decimal)x.Count(e => e.EventType.Equals("Defect", StringComparison.OrdinalIgnoreCase) || e.DefectCatalogId.HasValue) / materialsCount * 100m, 2)))
            .ToList();

        var metrics = new List<DashboardMetricDto>
        {
            new("MATERIALS", "Materials in scope", materialIds.Count, null, "Traceable material units included in the selected period."),
            new("DEFECT_RATE", "Defect rate", defectRate, "%", "Share of materials/events with defect signals."),
            new("HIGH_RISK_RATE", "High-risk material rate", highRiskRate, "%", "Share of materials currently classified as high risk."),
            new("DQ_ISSUES", "Data-quality issues", dataQualityIssues, null, "Open/current data-quality signals requiring review."),
            new("CORRELATIONS", "Saved correlation results", correlationResults, null, "Persisted analytics evidence available for investigation.")
        };

        var topContributors = riskScores
            .Where(x => !string.IsNullOrWhiteSpace(x.MainContributorsJson))
            .GroupBy(x => x.RiskClass ?? "Unknown")
            .Select(x => new DashboardRiskContributorDto("RiskClass", x.Key, x.Count(), Math.Round(x.Average(r => r.Score), 6)))
            .OrderByDescending(x => x.AverageRiskScore)
            .Take(10)
            .ToList();

        var dto = new DashboardOverviewDto(
            DateTime.UtcNow,
            siteId,
            sites,
            materialIds.Count,
            processSteps,
            parameterObservations,
            qualityEvents.Count,
            defectEvents,
            dataQualityIssues,
            riskScores.Count,
            highRiskMaterials,
            correlationResults,
            defectRate,
            highRiskRate,
            metrics,
            trend,
            topContributors);

        _logger.LogInformation("Dashboard overview generated. SiteId={SiteId}, Materials={Materials}, DefectRate={DefectRate}", siteId, dto.Materials, dto.DefectRatePercent);
        return ApplicationResult<DashboardOverviewDto>.Success(dto);
    }

    public async Task<ApplicationResult<QualityDashboardDto>> GetQualityDashboardAsync(Guid? siteId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var range = NormalizeRange(fromUtc, toUtc);
        var materialIds = await GetMaterialIdsAsync(siteId, range.FromUtc, range.ToUtc, cancellationToken);
        var materialIdSet = materialIds.ToHashSet();

        var eventsRaw = await _dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => !x.IsDeleted && materialIdSet.Contains(x.MaterialUnitId) && x.EventAtUtc >= range.FromUtc && x.EventAtUtc <= range.ToUtc)
            .Select(x => new { x.Id, x.DefectCatalogId, x.EventType, x.Decision })
            .ToListAsync(cancellationToken);

        var defectIds = eventsRaw.Where(x => x.DefectCatalogId.HasValue).Select(x => x.DefectCatalogId!.Value).Distinct().ToList();
        var defectLookup = await _dbContext.DefectCatalogs
            .AsNoTracking()
            .Where(x => defectIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DefectCode, x.DefectName, x.DefectCategory })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var defects = eventsRaw.Where(x => x.EventType.Equals("Defect", StringComparison.OrdinalIgnoreCase) || x.DefectCatalogId.HasValue).ToList();
        var defectBreakdown = defects
            .GroupBy(x => x.DefectCatalogId)
            .Select(x =>
            {
                defectLookup.TryGetValue(x.Key ?? Guid.Empty, out var defect);
                return new DefectBreakdownDto(
                    defect?.DefectCode,
                    defect?.DefectName,
                    defect?.DefectCategory,
                    x.Count(),
                    defects.Count == 0 ? 0 : Math.Round((decimal)x.Count() / defects.Count * 100m, 2));
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var decisionBreakdown = eventsRaw
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Decision) ? "Unknown" : x.Decision)
            .Select(x => new DecisionBreakdownDto(x.Key, x.Count(), eventsRaw.Count == 0 ? 0 : Math.Round((decimal)x.Count() / eventsRaw.Count * 100m, 2)))
            .OrderByDescending(x => x.Count)
            .ToList();

        return ApplicationResult<QualityDashboardDto>.Success(new QualityDashboardDto(
            DateTime.UtcNow,
            siteId,
            eventsRaw.Count,
            defects.Count,
            eventsRaw.Count == 0 ? 0 : Math.Round((decimal)defects.Count / eventsRaw.Count * 100m, 2),
            defectBreakdown,
            decisionBreakdown));
    }

    public async Task<ApplicationResult<RiskDashboardDto>> GetRiskDashboardAsync(Guid? siteId, int highRiskTake, CancellationToken cancellationToken)
    {
        var materialIds = await GetMaterialIdsAsync(siteId, null, null, cancellationToken);
        var materialIdSet = materialIds.ToHashSet();
        var take = Math.Clamp(highRiskTake, 1, 500);

        var riskScores = await _dbContext.RiskScores
            .AsNoTracking()
            .Where(x => !x.IsDeleted && materialIdSet.Contains(x.MaterialUnitId))
            .Select(x => new { x.MaterialUnitId, x.RiskType, x.Score, x.RiskClass, x.ModelVersion, x.ScoredAtUtc })
            .ToListAsync(cancellationToken);

        var latestRiskByMaterial = riskScores
            .GroupBy(x => x.MaterialUnitId)
            .Select(x => x.OrderByDescending(r => r.ScoredAtUtc).First())
            .ToList();

        var materialLookup = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => materialIdSet.Contains(x.Id))
            .Select(x => new { x.Id, x.MaterialCode, x.MaterialUnitType, x.ProductFamily, x.GradeOrRecipe })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var classBreakdown = latestRiskByMaterial
            .GroupBy(x => x.RiskClass ?? "Unknown")
            .Select(x => new RiskClassBreakdownDto(x.Key, x.Count(), latestRiskByMaterial.Count == 0 ? 0 : Math.Round((decimal)x.Count() / latestRiskByMaterial.Count * 100m, 2)))
            .OrderByDescending(x => x.Count)
            .ToList();

        var highRisk = latestRiskByMaterial
            .Where(x => x.Score >= 0.70m || (x.RiskClass ?? "").Equals("High", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Score)
            .Take(take)
            .Select(x =>
            {
                materialLookup.TryGetValue(x.MaterialUnitId, out var material);
                return new HighRiskMaterialDto(
                    x.MaterialUnitId,
                    material?.MaterialCode ?? x.MaterialUnitId.ToString("N"),
                    material?.MaterialUnitType ?? "Unknown",
                    material?.ProductFamily,
                    material?.GradeOrRecipe,
                    x.RiskType,
                    x.Score,
                    x.RiskClass,
                    x.ModelVersion,
                    x.ScoredAtUtc);
            })
            .ToList();

        return ApplicationResult<RiskDashboardDto>.Success(new RiskDashboardDto(
            DateTime.UtcNow,
            siteId,
            latestRiskByMaterial.Count,
            highRisk.Count,
            latestRiskByMaterial.Count == 0 ? 0 : Math.Round(latestRiskByMaterial.Average(x => x.Score), 6),
            classBreakdown,
            highRisk));
    }

    public async Task<ApplicationResult<DataQualityDashboardDto>> GetDataQualityDashboardAsync(Guid? siteId, CancellationToken cancellationToken)
    {
        var materialIds = await GetMaterialIdsAsync(siteId, null, null, cancellationToken);
        var materialIdSet = materialIds.ToHashSet();

        var issues = await _dbContext.DataQualityIssues
            .AsNoTracking()
            .Where(x => !x.IsDeleted && (!x.MaterialUnitId.HasValue || materialIdSet.Contains(x.MaterialUnitId.Value)))
            .Select(x => new { x.IssueType, x.Severity })
            .ToListAsync(cancellationToken);

        var severity = issues.GroupBy(x => x.Severity)
            .Select(x => new DataQualityIssueBreakdownDto(x.Key, x.Count(), issues.Count == 0 ? 0 : Math.Round((decimal)x.Count() / issues.Count * 100m, 2)))
            .OrderByDescending(x => x.Count)
            .ToList();

        var issueType = issues.GroupBy(x => x.IssueType)
            .Select(x => new DataQualityIssueBreakdownDto(x.Key, x.Count(), issues.Count == 0 ? 0 : Math.Round((decimal)x.Count() / issues.Count * 100m, 2)))
            .OrderByDescending(x => x.Count)
            .ToList();

        return ApplicationResult<DataQualityDashboardDto>.Success(new DataQualityDashboardDto(DateTime.UtcNow, siteId, issues.Count, issues.Count, severity, issueType));
    }

    private async Task<List<Guid>> GetMaterialIdsAsync(Guid? siteId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var query = _dbContext.MaterialUnits.AsNoTracking().Where(x => !x.IsDeleted);
        if (siteId.HasValue) query = query.Where(x => x.SiteId == siteId.Value);
        if (fromUtc.HasValue) query = query.Where(x => !x.ProductionEndUtc.HasValue || x.ProductionEndUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(x => !x.ProductionStartUtc.HasValue || x.ProductionStartUtc <= toUtc.Value);
        return await query.Select(x => x.Id).ToListAsync(cancellationToken);
    }

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeRange(DateTime? fromUtc, DateTime? toUtc)
    {
        var to = EnsureUtc(toUtc ?? DateTime.UtcNow);
        var from = EnsureUtc(fromUtc ?? to.AddDays(-30));
        if (from > to) (from, to) = (to, from);
        return (from, to);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
