using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;
using PlantProcess.Application.Services.Dashboard.Interfaces;

namespace PlantProcess.Application.Services.Dashboard.Services;

public sealed class DashboardQueryService : IDashboardQueryService
{
    private readonly IPlantProcessDbContext _dbContext;

    public DashboardQueryService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationResult<DashboardWorkspaceDto>> GetWorkspaceAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken)
    {
        var overview = await GetOverviewAsync(query, cancellationToken);
        if (!overview.IsSuccess) return ApplicationResult<DashboardWorkspaceDto>.Failure(overview.Error!);

        var quality = await GetQualityDashboardAsync(query, cancellationToken);
        if (!quality.IsSuccess) return ApplicationResult<DashboardWorkspaceDto>.Failure(quality.Error!);

        var risk = await GetRiskDashboardAsync(query, cancellationToken);
        if (!risk.IsSuccess) return ApplicationResult<DashboardWorkspaceDto>.Failure(risk.Error!);

        var dataQuality = await GetDataQualityDashboardAsync(query, cancellationToken);
        if (!dataQuality.IsSuccess) return ApplicationResult<DashboardWorkspaceDto>.Failure(dataQuality.Error!);

        var materials = await SearchMaterialsAsync(query, cancellationToken);
        if (!materials.IsSuccess) return ApplicationResult<DashboardWorkspaceDto>.Failure(materials.Error!);

        return ApplicationResult<DashboardWorkspaceDto>.Success(
            new DashboardWorkspaceDto(
                DateTime.UtcNow,
                NormalizeQuery(query),
                overview.Value!,
                quality.Value!,
                risk.Value!,
                dataQuality.Value!,
                materials.Value!));
    }

    public async Task<ApplicationResult<DashboardOverviewDto>> GetOverviewAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeQuery(query);
        var materialIds = await GetFilteredMaterialIdsAsync(normalized, cancellationToken);
        var materialSet = materialIds.ToHashSet();

        var sites = await _dbContext.Sites.AsNoTracking().CountAsync(cancellationToken);

        var materialCount = materialIds.Count;

        var processStepsQuery = _dbContext.ProcessStepExecutions.AsNoTracking()
            .Where(x => materialSet.Contains(x.MaterialUnitId));

        var parameterQuery = _dbContext.ParameterObservations.AsNoTracking()
            .Where(x => materialSet.Contains(x.MaterialUnitId));

        var qualityQuery = _dbContext.QualityEvents.AsNoTracking()
            .Where(x => materialSet.Contains(x.MaterialUnitId));

        var riskQuery = _dbContext.RiskScores.AsNoTracking()
            .Where(x => materialSet.Contains(x.MaterialUnitId));

        ApplyTimeFiltersToProcessAndObservation(normalized, ref processStepsQuery, ref parameterQuery, ref qualityQuery);

        if (!string.IsNullOrWhiteSpace(normalized.SourceSystem))
        {
            processStepsQuery = processStepsQuery.Where(x => x.SourceSystem == normalized.SourceSystem);
            parameterQuery = parameterQuery.Where(x => x.SourceSystem == normalized.SourceSystem);
            qualityQuery = qualityQuery.Where(x => x.SourceSystem == normalized.SourceSystem);
            riskQuery = riskQuery.Where(x => x.SourceSystem == normalized.SourceSystem);
        }

        var qualityEvents = await qualityQuery
            .Select(x => new { x.Id, x.EventType, x.EventAtUtc })
            .ToListAsync(cancellationToken);

        var defectEvents = qualityEvents
            .Where(x => IsDefectEvent(x.EventType))
            .ToList();

        var risks = await riskQuery
            .Select(x => new
            {
                x.MaterialUnitId,
                x.Score,
                x.RiskClass,
                x.MainContributorsJson
            })
            .ToListAsync(cancellationToken);

        var latestRisks = risks
            .GroupBy(x => x.MaterialUnitId)
            .Select(x => x.OrderByDescending(r => r.Score).First())
            .ToList();

        if (!string.IsNullOrWhiteSpace(normalized.RiskClass))
        {
            latestRisks = latestRisks
                .Where(x => string.Equals(x.RiskClass, normalized.RiskClass, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var highRiskMaterials = latestRisks
            .Count(x => x.Score >= 0.70m || string.Equals(x.RiskClass, "High", StringComparison.OrdinalIgnoreCase) || string.Equals(x.RiskClass, "Critical", StringComparison.OrdinalIgnoreCase));

        var correlationCount = await _dbContext.CorrelationResults
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var dataQualityCount = await _dbContext.DataQualityIssues.AsNoTracking()
            .Where(x => !x.MaterialUnitId.HasValue || materialSet.Contains(x.MaterialUnitId.Value))
            .CountAsync(cancellationToken);

        var trend = qualityEvents
            .GroupBy(x => x.EventAtUtc.Date)
            .OrderBy(x => x.Key)
            .Select(x =>
            {
                var qualityCount = x.Count();
                var defectCount = x.Count(y => IsDefectEvent(y.EventType));

                return new DashboardTrendPointDto(
                    x.Key,
                    MaterialCount: materialCount,
                    QualityEventCount: qualityCount,
                    DefectEventCount: defectCount,
                    DefectRatePercent: qualityCount == 0 ? 0 : Math.Round((decimal)defectCount / qualityCount * 100m, 2));
            })
            .ToList();

        var contributors = latestRisks
            .SelectMany(x => ExtractContributorCodes(x.MainContributorsJson)
                .Select(code => new { Code = code, x.Score }))
            .GroupBy(x => x.Code)
            .Select(x => new DashboardRiskContributorDto(
                "RiskContributor",
                x.Key,
                x.Count(),
                Math.Round(x.Average(y => y.Score), 6)))
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.AverageRiskScore)
            .Take(10)
            .ToList();

        var processStepCount = await processStepsQuery.CountAsync(cancellationToken);
        var parameterCount = await parameterQuery.CountAsync(cancellationToken);
        var riskScoreCount = await riskQuery.CountAsync(cancellationToken);

        var defectRate = qualityEvents.Count == 0
            ? 0
            : Math.Round((decimal)defectEvents.Count / qualityEvents.Count * 100m, 2);

        var highRiskRate = materialCount == 0
            ? 0
            : Math.Round((decimal)highRiskMaterials / materialCount * 100m, 2);

        var metrics = new List<DashboardMetricDto>
        {
            new("MATERIALS", "Materials", materialCount, null, "Traceable material/batch units in the selected context."),
            new("PROCESS_STEPS", "Process Steps", processStepCount, null, "Executed operations linked to selected materials."),
            new("PARAMETER_OBS", "Parameter Observations", parameterCount, null, "Aggregated process values available for analytics."),
            new("DEFECT_RATE", "Defect Rate", defectRate, "%", "Defect events divided by quality events."),
            new("HIGH_RISK", "High-Risk Materials", highRiskMaterials, null, "Materials with high or critical risk class or score >= 0.70."),
            new("DQ_ISSUES", "Data-Quality Issues", dataQualityCount, null, "Detected missing/broken/inconsistent records.")
        };

        return ApplicationResult<DashboardOverviewDto>.Success(
            new DashboardOverviewDto(
                DateTime.UtcNow,
                normalized.SiteId,
                sites,
                materialCount,
                processStepCount,
                parameterCount,
                qualityEvents.Count,
                defectEvents.Count,
                dataQualityCount,
                riskScoreCount,
                highRiskMaterials,
                correlationCount,
                defectRate,
                highRiskRate,
                metrics,
                trend,
                contributors));
    }

    public async Task<ApplicationResult<QualityDashboardDto>> GetQualityDashboardAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeQuery(query);
        var materialIds = await GetFilteredMaterialIdsAsync(normalized, cancellationToken);
        var materialSet = materialIds.ToHashSet();

        var eventsQuery =
            from qualityEvent in _dbContext.QualityEvents.AsNoTracking()
            join defect in _dbContext.DefectCatalogs.AsNoTracking()
                on qualityEvent.DefectCatalogId equals defect.Id into defectJoin
            from defect in defectJoin.DefaultIfEmpty()
            where materialSet.Contains(qualityEvent.MaterialUnitId)
            select new
            {
                qualityEvent.Id,
                qualityEvent.EventType,
                qualityEvent.EventAtUtc,
                qualityEvent.Decision,
                qualityEvent.SourceSystem,
                DefectCode = defect == null ? null : defect.DefectCode,
                DefectName = defect == null ? null : defect.DefectName,
                DefectCategory = defect == null ? null : defect.DefectCategory
            };

        if (normalized.FromUtc.HasValue)
            eventsQuery = eventsQuery.Where(x => x.EventAtUtc >= normalized.FromUtc.Value);

        if (normalized.ToUtc.HasValue)
            eventsQuery = eventsQuery.Where(x => x.EventAtUtc <= normalized.ToUtc.Value);

        if (!string.IsNullOrWhiteSpace(normalized.SourceSystem))
            eventsQuery = eventsQuery.Where(x => x.SourceSystem == normalized.SourceSystem);

        if (!string.IsNullOrWhiteSpace(normalized.DefectType))
        {
            eventsQuery = eventsQuery.Where(x =>
                x.EventType == normalized.DefectType ||
                x.DefectCode == normalized.DefectType ||
                x.DefectName == normalized.DefectType);
        }

        var eventsRaw = await eventsQuery.ToListAsync(cancellationToken);
        var defects = eventsRaw.Where(x => IsDefectEvent(x.EventType)).ToList();

        var defectBreakdown = defects
            .GroupBy(x => new
            {
                Code = string.IsNullOrWhiteSpace(x.DefectCode) ? x.EventType : x.DefectCode,
                Name = string.IsNullOrWhiteSpace(x.DefectName) ? x.EventType : x.DefectName,
                Category = string.IsNullOrWhiteSpace(x.DefectCategory) ? "Unknown" : x.DefectCategory
            })
            .Select(x => new DefectBreakdownDto(
                x.Key.Code,
                x.Key.Name,
                x.Key.Category,
                x.Count(),
                defects.Count == 0 ? 0 : Math.Round((decimal)x.Count() / defects.Count * 100m, 2)))
            .OrderByDescending(x => x.Count)
            .ToList();

        var decisionBreakdown = eventsRaw
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Decision) ? "Unknown" : x.Decision)
            .Select(x => new DecisionBreakdownDto(
                x.Key,
                x.Count(),
                eventsRaw.Count == 0 ? 0 : Math.Round((decimal)x.Count() / eventsRaw.Count * 100m, 2)))
            .OrderByDescending(x => x.Count)
            .ToList();

        return ApplicationResult<QualityDashboardDto>.Success(
            new QualityDashboardDto(
                DateTime.UtcNow,
                normalized.SiteId,
                eventsRaw.Count,
                defects.Count,
                eventsRaw.Count == 0 ? 0 : Math.Round((decimal)defects.Count / eventsRaw.Count * 100m, 2),
                defectBreakdown,
                decisionBreakdown));
    }

    public async Task<ApplicationResult<RiskDashboardDto>> GetRiskDashboardAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeQuery(query);
        var materialIds = await GetFilteredMaterialIdsAsync(normalized, cancellationToken);
        var materialSet = materialIds.ToHashSet();

        var riskScoresQuery = _dbContext.RiskScores
            .AsNoTracking()
            .Where(x => materialSet.Contains(x.MaterialUnitId));

        if (!string.IsNullOrWhiteSpace(normalized.SourceSystem))
            riskScoresQuery = riskScoresQuery.Where(x => x.SourceSystem == normalized.SourceSystem);

        if (!string.IsNullOrWhiteSpace(normalized.RiskClass))
            riskScoresQuery = riskScoresQuery.Where(x => x.RiskClass == normalized.RiskClass);

        var riskScores = await riskScoresQuery
            .Select(x => new
            {
                x.MaterialUnitId,
                x.RiskType,
                x.Score,
                x.RiskClass,
                x.ModelVersion,
                x.ScoredAtUtc
            })
            .ToListAsync(cancellationToken);

        var latestRiskByMaterial = riskScores
            .GroupBy(x => x.MaterialUnitId)
            .Select(x => x.OrderByDescending(r => r.ScoredAtUtc).First())
            .ToList();

        var materialLookup = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => materialSet.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.MaterialCode,
                x.MaterialUnitType,
                x.ProductFamily,
                x.GradeOrRecipe
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var classBreakdown = latestRiskByMaterial
            .GroupBy(x => string.IsNullOrWhiteSpace(x.RiskClass) ? "Unknown" : x.RiskClass)
            .Select(x => new RiskClassBreakdownDto(
                x.Key,
                x.Count(),
                latestRiskByMaterial.Count == 0 ? 0 : Math.Round((decimal)x.Count() / latestRiskByMaterial.Count * 100m, 2)))
            .OrderByDescending(x => x.Count)
            .ToList();

        var highRisk = latestRiskByMaterial
            .Where(x => x.Score >= 0.70m || string.Equals(x.RiskClass, "High", StringComparison.OrdinalIgnoreCase) || string.Equals(x.RiskClass, "Critical", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Score)
            .Take(normalized.SafePageSize)
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

        return ApplicationResult<RiskDashboardDto>.Success(
            new RiskDashboardDto(
                DateTime.UtcNow,
                normalized.SiteId,
                latestRiskByMaterial.Count,
                highRisk.Count,
                latestRiskByMaterial.Count == 0 ? 0 : Math.Round(latestRiskByMaterial.Average(x => x.Score), 6),
                classBreakdown,
                highRisk));
    }

    public async Task<ApplicationResult<DataQualityDashboardDto>> GetDataQualityDashboardAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeQuery(query);
        var materialIds = await GetFilteredMaterialIdsAsync(normalized, cancellationToken);
        var materialSet = materialIds.ToHashSet();

        var issues = await _dbContext.DataQualityIssues
            .AsNoTracking()
            .Where(x => !x.MaterialUnitId.HasValue || materialSet.Contains(x.MaterialUnitId.Value))
            .Select(x => new { x.IssueType, x.Severity })
            .ToListAsync(cancellationToken);

        var severity = issues
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Severity) ? "Unknown" : x.Severity)
            .Select(x => new DataQualityIssueBreakdownDto(
                x.Key,
                x.Count(),
                issues.Count == 0 ? 0 : Math.Round((decimal)x.Count() / issues.Count * 100m, 2)))
            .OrderByDescending(x => x.Count)
            .ToList();

        var issueType = issues
            .GroupBy(x => string.IsNullOrWhiteSpace(x.IssueType) ? "Unknown" : x.IssueType)
            .Select(x => new DataQualityIssueBreakdownDto(
                x.Key,
                x.Count(),
                issues.Count == 0 ? 0 : Math.Round((decimal)x.Count() / issues.Count * 100m, 2)))
            .OrderByDescending(x => x.Count)
            .ToList();

        return ApplicationResult<DataQualityDashboardDto>.Success(
            new DataQualityDashboardDto(
                DateTime.UtcNow,
                normalized.SiteId,
                issues.Count,
                issues.Count,
                severity,
                issueType));
    }

    public async Task<ApplicationResult<DashboardPagedResultDto<DashboardMaterialRowDto>>> SearchMaterialsAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeQuery(query);
        var materialIds = await GetFilteredMaterialIdsAsync(normalized, cancellationToken);
        var materialSet = materialIds.ToHashSet();

       var materialsQuery = _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(material => materialSet.Contains(material.Id));

        materialsQuery = ApplyMaterialSort(
            materialsQuery,
            normalized.SortBy,
            normalized.SafeSortDirection);

        var queryable =
            from material in materialsQuery
            join site in _dbContext.Sites.AsNoTracking()
                on material.SiteId equals site.Id
            select new
            {
                material.Id,
                material.MaterialCode,
                material.MaterialUnitType,
                material.ProductFamily,
                material.GradeOrRecipe,
                material.SiteId,
                site.SiteName,
                material.ProductionStartUtc,
                material.ProductionEndUtc,
                material.SourceSystem
            };
        var totalCount = await queryable.CountAsync(cancellationToken);

        var rows = await queryable
            .Skip((normalized.SafePage - 1) * normalized.SafePageSize)
            .Take(normalized.SafePageSize)
            .ToListAsync(cancellationToken);

        var pageMaterialIds = rows.Select(x => x.Id).ToHashSet();

        var processCounts = await _dbContext.ProcessStepExecutions
            .AsNoTracking()
            .Where(x => pageMaterialIds.Contains(x.MaterialUnitId))
            .GroupBy(x => x.MaterialUnitId)
            .Select(x => new { MaterialUnitId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.MaterialUnitId, x => x.Count, cancellationToken);

        var parameterCounts = await _dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => pageMaterialIds.Contains(x.MaterialUnitId))
            .GroupBy(x => x.MaterialUnitId)
            .Select(x => new { MaterialUnitId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.MaterialUnitId, x => x.Count, cancellationToken);

        var qualityCounts = await _dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => pageMaterialIds.Contains(x.MaterialUnitId))
            .GroupBy(x => x.MaterialUnitId)
            .Select(x => new
            {
                MaterialUnitId = x.Key,
                QualityCount = x.Count(),
                DefectCount = x.Count(e => e.EventType == "Defect")
            })
            .ToDictionaryAsync(x => x.MaterialUnitId, cancellationToken);

        var latestRisks = await _dbContext.RiskScores
            .AsNoTracking()
            .Where(x => pageMaterialIds.Contains(x.MaterialUnitId))
            .GroupBy(x => x.MaterialUnitId)
            .Select(x => x.OrderByDescending(r => r.ScoredAtUtc).First())
            .ToDictionaryAsync(x => x.MaterialUnitId, cancellationToken);

        var items = rows.Select(row =>
        {
            processCounts.TryGetValue(row.Id, out var processCount);
            parameterCounts.TryGetValue(row.Id, out var parameterCount);
            qualityCounts.TryGetValue(row.Id, out var quality);
            latestRisks.TryGetValue(row.Id, out var risk);

            return new DashboardMaterialRowDto(
                row.Id,
                row.MaterialCode,
                row.MaterialUnitType,
                row.ProductFamily,
                row.GradeOrRecipe,
                row.SiteId,
                row.SiteName,
                row.ProductionStartUtc,
                row.ProductionEndUtc,
                row.SourceSystem,
                processCount,
                parameterCount,
                quality?.QualityCount ?? 0,
                quality?.DefectCount ?? 0,
                risk?.Score,
                risk?.RiskClass,
                risk?.ScoredAtUtc);
        }).ToList();

        var totalPages = totalCount == 0
            ? 0
            : (int)Math.Ceiling((decimal)totalCount / normalized.SafePageSize);

        return ApplicationResult<DashboardPagedResultDto<DashboardMaterialRowDto>>.Success(
            new DashboardPagedResultDto<DashboardMaterialRowDto>(
                items,
                normalized.SafePage,
                normalized.SafePageSize,
                totalCount,
                totalPages,
                normalized.SortBy,
                normalized.SafeSortDirection));
    }

    private async Task<List<Guid>> GetFilteredMaterialIdsAsync(
        DashboardQueryDto query,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeQuery(query);

        var materialsQuery = _dbContext.MaterialUnits
            .AsNoTracking()
            .AsQueryable();

        if (normalized.SiteId.HasValue)
            materialsQuery = materialsQuery.Where(x => x.SiteId == normalized.SiteId.Value);

        if (normalized.FromUtc.HasValue)
            materialsQuery = materialsQuery.Where(x => !x.ProductionEndUtc.HasValue || x.ProductionEndUtc >= normalized.FromUtc.Value);

        if (normalized.ToUtc.HasValue)
            materialsQuery = materialsQuery.Where(x => !x.ProductionStartUtc.HasValue || x.ProductionStartUtc <= normalized.ToUtc.Value);

        if (!string.IsNullOrWhiteSpace(normalized.MaterialCode))
        {
            var search = normalized.MaterialCode.Trim().ToLower();
            materialsQuery = materialsQuery.Where(x => x.MaterialCode.ToLower().Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(normalized.SourceSystem))
            materialsQuery = materialsQuery.Where(x => x.SourceSystem == normalized.SourceSystem);

        var materialIds = await materialsQuery
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var materialSet = materialIds.ToHashSet();

        if (normalized.AreaId.HasValue)
        {
            var equipmentIdsInArea = await _dbContext.Equipment
                .AsNoTracking()
                .Where(x => x.AreaId == normalized.AreaId.Value)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var stepMaterialIds = await _dbContext.ProcessStepExecutions
                .AsNoTracking()
                .Where(x => x.EquipmentId.HasValue && equipmentIdsInArea.Contains(x.EquipmentId.Value))
                .Select(x => x.MaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            materialSet.IntersectWith(stepMaterialIds);
        }

        if (normalized.EquipmentId.HasValue)
        {
            var stepMaterialIds = await _dbContext.ProcessStepExecutions
                .AsNoTracking()
                .Where(x => x.EquipmentId == normalized.EquipmentId.Value)
                .Select(x => x.MaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            materialSet.IntersectWith(stepMaterialIds);
        }

        if (!string.IsNullOrWhiteSpace(normalized.ShiftCode))
        {
            var shiftMaterialIds = await _dbContext.ProcessStepExecutions
                .AsNoTracking()
                .Where(x => x.CrewCode == normalized.ShiftCode)
                .Select(x => x.MaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            materialSet.IntersectWith(shiftMaterialIds);
        }

        if (!string.IsNullOrWhiteSpace(normalized.DefectType))
        {
            var defectMaterialIds = await (
                from qualityEvent in _dbContext.QualityEvents.AsNoTracking()
                join defect in _dbContext.DefectCatalogs.AsNoTracking()
                    on qualityEvent.DefectCatalogId equals defect.Id into defectJoin
                from defect in defectJoin.DefaultIfEmpty()
                where
                    qualityEvent.EventType == normalized.DefectType ||
                    qualityEvent.EventType == "Defect" && defect != null && defect.DefectCode == normalized.DefectType ||
                    qualityEvent.EventType == "Defect" && defect != null && defect.DefectName == normalized.DefectType ||
                    defect != null && defect.DefectCode == normalized.DefectType ||
                    defect != null && defect.DefectName == normalized.DefectType
                select qualityEvent.MaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            materialSet.IntersectWith(defectMaterialIds);
        }

        if (!string.IsNullOrWhiteSpace(normalized.RiskClass))
        {
            var riskMaterialIds = await _dbContext.RiskScores
                .AsNoTracking()
                .Where(x => x.RiskClass == normalized.RiskClass)
                .Select(x => x.MaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            materialSet.IntersectWith(riskMaterialIds);
        }

        return materialSet.ToList();
    }

    private static void ApplyTimeFiltersToProcessAndObservation(
        DashboardQueryDto query,
        ref IQueryable<PlantProcess.Domain.Entities.Process.ProcessStepExecution> processStepsQuery,
        ref IQueryable<PlantProcess.Domain.Entities.Process.ParameterObservation> parameterQuery,
        ref IQueryable<PlantProcess.Domain.Entities.Quality.QualityEvent> qualityQuery)
    {
        if (query.FromUtc.HasValue)
        {
            processStepsQuery = processStepsQuery.Where(x => x.StartedAtUtc >= query.FromUtc.Value);
            parameterQuery = parameterQuery.Where(x => x.ObservedAtUtc >= query.FromUtc.Value);
            qualityQuery = qualityQuery.Where(x => x.EventAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            processStepsQuery = processStepsQuery.Where(x => x.StartedAtUtc <= query.ToUtc.Value);
            parameterQuery = parameterQuery.Where(x => x.ObservedAtUtc <= query.ToUtc.Value);
            qualityQuery = qualityQuery.Where(x => x.EventAtUtc <= query.ToUtc.Value);
        }
    }

    private static IQueryable<PlantProcess.Domain.Entities.Materials.MaterialUnit> ApplyMaterialSort(
    IQueryable<PlantProcess.Domain.Entities.Materials.MaterialUnit> queryable,
    string? sortBy,
    string sortDirection)
{
    var normalizedSortBy = string.IsNullOrWhiteSpace(sortBy)
        ? "productionstartutc"
        : sortBy.Trim().ToLowerInvariant();

        var ascending = string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        return normalizedSortBy switch
        {
            "materialcode" => ascending
                ? queryable
                    .OrderBy(x => x.MaterialCode)
                    .ThenByDescending(x => x.ProductionStartUtc)
                : queryable
                    .OrderByDescending(x => x.MaterialCode)
                    .ThenByDescending(x => x.ProductionStartUtc),

            "materialunittype" => ascending
                ? queryable
                    .OrderBy(x => x.MaterialUnitType)
                    .ThenBy(x => x.MaterialCode)
                : queryable
                    .OrderByDescending(x => x.MaterialUnitType)
                    .ThenBy(x => x.MaterialCode),

            "productfamily" => ascending
                ? queryable
                    .OrderBy(x => x.ProductFamily ?? string.Empty)
                    .ThenBy(x => x.MaterialCode)
                : queryable
                    .OrderByDescending(x => x.ProductFamily ?? string.Empty)
                    .ThenBy(x => x.MaterialCode),

            "gradeorrecipe" => ascending
                ? queryable
                    .OrderBy(x => x.GradeOrRecipe ?? string.Empty)
                    .ThenBy(x => x.MaterialCode)
                : queryable
                    .OrderByDescending(x => x.GradeOrRecipe ?? string.Empty)
                    .ThenBy(x => x.MaterialCode),

            "sourcesystem" => ascending
                ? queryable
                    .OrderBy(x => x.SourceSystem ?? string.Empty)
                    .ThenBy(x => x.MaterialCode)
                : queryable
                    .OrderByDescending(x => x.SourceSystem ?? string.Empty)
                    .ThenBy(x => x.MaterialCode),

            "productionendutc" => ascending
                ? queryable
                    .OrderBy(x => x.ProductionEndUtc)
                    .ThenBy(x => x.MaterialCode)
                : queryable
                    .OrderByDescending(x => x.ProductionEndUtc)
                    .ThenBy(x => x.MaterialCode),

            "productionstartutc" => ascending
                ? queryable
                    .OrderBy(x => x.ProductionStartUtc)
                    .ThenBy(x => x.MaterialCode)
                : queryable
                    .OrderByDescending(x => x.ProductionStartUtc)
                    .ThenBy(x => x.MaterialCode),

            _ => ascending
                ? queryable
                    .OrderBy(x => x.ProductionStartUtc)
                    .ThenBy(x => x.MaterialCode)
                : queryable
                    .OrderByDescending(x => x.ProductionStartUtc)
                    .ThenBy(x => x.MaterialCode)
        };
    }

    private static DashboardQueryDto NormalizeQuery(DashboardQueryDto query)
    {
        return query with
        {
            MaterialCode = NormalizeText(query.MaterialCode),
            SourceSystem = NormalizeText(query.SourceSystem),
            DefectType = NormalizeText(query.DefectType),
            RiskClass = NormalizeText(query.RiskClass),
            ShiftCode = NormalizeText(query.ShiftCode),
            Page = query.SafePage,
            PageSize = query.SafePageSize,
            SortDirection = query.SafeSortDirection
        };
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool IsDefectEvent(string? eventType)
    {
        return string.Equals(eventType, "Defect", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ExtractContributorCodes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        // Keep this robust and cheap for the MVP.
        // It extracts common contributor names from JSON-ish strings without taking dependency on a strict schema.
        var tokens = json
            .Replace("{", " ")
            .Replace("}", " ")
            .Replace("[", " ")
            .Replace("]", " ")
            .Replace("\"", " ")
            .Replace(":", " ")
            .Replace(",", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return tokens
            .Where(x =>
                x.Contains("Speed", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("Temp", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("Force", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("Mould", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("Powder", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("Superheat", StringComparison.OrdinalIgnoreCase) ||
                x.Contains("Equipment", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }
}