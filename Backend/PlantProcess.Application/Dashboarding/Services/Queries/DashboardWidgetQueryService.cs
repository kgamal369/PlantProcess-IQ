using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Dashboarding.Interfaces;
using PlantProcess.Application.Dashboarding.Services.Widgets;


namespace PlantProcess.Application.Dashboarding.Services.Queries;

public sealed class DashboardWidgetQueryService : IDashboardWidgetQueryService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IDashboardWidgetValidationService _validationService;

    public DashboardWidgetQueryService(
        IPlantProcessDbContext dbContext,
        IDashboardWidgetValidationService validationService)
    {
        _dbContext = dbContext;
        _validationService = validationService;
    }

    public async Task<ApplicationResult<DashboardWidgetQueryResultDto>> ExecuteAsync(
        DashboardWidgetQueryDto query,
        CancellationToken cancellationToken)
    {
        var validation = _validationService.Validate(query);

        if (!validation.IsSuccess)
            return ApplicationResult<DashboardWidgetQueryResultDto>.Failure(validation.Error!);

        var resolved = validation.Value!.ResolvedWidget!;
        var warnings = validation.Value!.Warnings.ToList();

        // The validator compares measure codes case-insensitively; this switch
        // is case-sensitive, so a code the validator accepted could fall to the
        // default arm and return an empty array with HTTP 200. Ten widgets sat
        // silently empty that way. A measure this engine cannot serve is now a
        // NAMED refusal, so the two halves can still disagree but never again
        // without saying so.
        if (!ExecutableMeasures.Contains(resolved.MeasureCode))
        {
            return ApplicationResult<DashboardWidgetQueryResultDto>.Failure(
                ApplicationError.Validation(
                    "The widget measure is published but this engine cannot execute it.",
                    new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                    {
                        [nameof(resolved.MeasureCode)] = new[]
                        {
                            $"'{resolved.MeasureCode}' is not executable. Executable measures are: " +
                            string.Join(", ", ExecutableMeasures.OrderBy(x => x, StringComparer.Ordinal)) +
                            ". Codes are case-sensitive."
                        }
                    }));
        }

        var rows = resolved.MeasureCode switch
        {
            DashboardMetadataCodes.Measures.MaterialCount =>
                await ExecuteMaterialCountAsync(resolved, query.Filters, cancellationToken),

            DashboardMetadataCodes.Measures.DefectCount =>
                await ExecuteDefectCountAsync(resolved, query.Filters, cancellationToken),

            DashboardMetadataCodes.Measures.DefectRate =>
                await ExecuteDefectRateAsync(resolved, query.Filters, cancellationToken),

            DashboardMetadataCodes.Measures.AvgParameterValue =>
                await ExecuteParameterAggregateAsync(resolved, query.Filters, ParameterAggregationMode.Average, cancellationToken),

            DashboardMetadataCodes.Measures.MaxParameterValue =>
                await ExecuteParameterAggregateAsync(resolved, query.Filters, ParameterAggregationMode.Maximum, cancellationToken),

            DashboardMetadataCodes.Measures.MinParameterValue =>
                await ExecuteParameterAggregateAsync(resolved, query.Filters, ParameterAggregationMode.Minimum, cancellationToken),

            DashboardMetadataCodes.Measures.DowntimeMinutes =>
                await ExecuteDowntimeMinutesAsync(resolved, query.Filters, cancellationToken),

            DashboardMetadataCodes.Measures.RiskScore =>
                await ExecuteRiskScoreAsync(resolved, query.Filters, cancellationToken),

            DashboardMetadataCodes.Measures.ProcessStepDuration =>
                await ExecuteProcessStepDurationAsync(resolved, query.Filters, cancellationToken),

            DashboardMetadataCodes.Measures.DataQualityIssueCount =>
                await ExecuteDataQualityIssueCountAsync(resolved, query.Filters, cancellationToken),

            DashboardMetadataCodes.Measures.ObservationCount =>
                await ExecuteObservationCountAsync(resolved, query.Filters, cancellationToken),

            _ => Array.Empty<DashboardAggregateRow>()
        };

        // PRESENTATION CORRECTION. Equipment and Area are dimensioned BY ID, and
        // that is correct: an id is stable and a name is not, so selections,
        // drill-through and filters all travel on the key. What is wrong is
        // showing that key to a plant engineer. Only the LABEL changes here.
        var dimensionLabels = await LoadDimensionLabelsAsync(resolved.DimensionCode, cancellationToken);

        return ApplicationResult<DashboardWidgetQueryResultDto>.Success(
            BuildResult(resolved, rows, warnings, dimensionLabels));
    }

    private async Task<IReadOnlyList<DashboardAggregateRow>> ExecuteMaterialCountAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetFiltersDto? filters,
        CancellationToken cancellationToken)
    {
        var materialIds = await GetFilteredMaterialIdsAsync(filters, cancellationToken);

        if (IsDimension(resolved, DashboardMetadataCodes.Dimensions.Equipment) ||
            IsDimension(resolved, DashboardMetadataCodes.Dimensions.ShiftCode) ||
            IsDimension(resolved, DashboardMetadataCodes.Dimensions.Area))
        {
            var stepFacts = await (
                    from step in _dbContext.ProcessStepExecutions.AsNoTracking()
                    join equipment in _dbContext.Equipment.AsNoTracking()
                        on step.EquipmentId equals equipment.Id
                    where
                        !step.IsDeleted &&
                        materialIds.Contains(step.MaterialUnitId)
                    select new WidgetFact(
                        step.MaterialUnitId,
                        null,
                        equipment.AreaId,
                        step.EquipmentId,
                        null,
                        null,
                        null,
                        null,
                        step.SourceSystem,
                        step.CrewCode,
                        null,
                        null,
                        null,
                        step.StartedAtUtc,
                        1m))
                .Take(resolved.RawRowLimit)
                .ToListAsync(cancellationToken);

            var uniqueMaterialPerDimension = stepFacts
                .GroupBy(x => new
                {
                    Dimension = ResolveDimension(resolved.DimensionCode, x).Key,
                    x.MaterialUnitId
                })
                .Select(g => g.First())
                .ToList();

            return AggregateCount(uniqueMaterialPerDimension, resolved);
        }

        var facts = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => !x.IsDeleted && materialIds.Contains(x.Id))
            .Select(x => new WidgetFact(
                x.Id,
                x.SiteId,
                null,
                null,
                x.MaterialCode,
                x.MaterialUnitType,
                x.ProductFamily,
                x.GradeOrRecipe,
                x.SourceSystem,
                null,
                null,
                null,
                null,
                x.ProductionStartUtc,
                1m))
            .Take(resolved.RawRowLimit)
            .ToListAsync(cancellationToken);

        return AggregateCount(facts, resolved);
    }

    private async Task<IReadOnlyList<DashboardAggregateRow>> ExecuteDefectCountAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetFiltersDto? filters,
        CancellationToken cancellationToken)
    {
        var materialIds = await GetFilteredMaterialIdsAsync(filters, cancellationToken);

        var facts = await (
                from qualityEvent in _dbContext.QualityEvents.AsNoTracking()
                join material in _dbContext.MaterialUnits.AsNoTracking()
                    on qualityEvent.MaterialUnitId equals material.Id
                join defect in _dbContext.DefectCatalogs.AsNoTracking()
                    on qualityEvent.DefectCatalogId equals defect.Id into defectJoin
                from defect in defectJoin.DefaultIfEmpty()
                where
                    !qualityEvent.IsDeleted &&
                    !material.IsDeleted &&
                    materialIds.Contains(qualityEvent.MaterialUnitId)
                select new WidgetFact(
                    qualityEvent.MaterialUnitId,
                    material.SiteId,
                    null,
                    null,
                    material.MaterialCode,
                    material.MaterialUnitType,
                    material.ProductFamily,
                    material.GradeOrRecipe,
                    material.SourceSystem,
                    null,
                    defect != null ? defect.DefectCode : qualityEvent.EventType,
                    null,
                    null,
                    qualityEvent.EventAtUtc,
                    1m))
            .Take(resolved.RawRowLimit)
            .ToListAsync(cancellationToken);

        return AggregateCount(facts, resolved);
    }

    private async Task<IReadOnlyList<DashboardAggregateRow>> ExecuteDefectRateAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetFiltersDto? filters,
        CancellationToken cancellationToken)
    {
        var materialIds = await GetFilteredMaterialIdsAsync(filters, cancellationToken);

        var materialFacts = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => !x.IsDeleted && materialIds.Contains(x.Id))
            .Select(x => new WidgetFact(
                x.Id,
                x.SiteId,
                null,
                null,
                x.MaterialCode,
                x.MaterialUnitType,
                x.ProductFamily,
                x.GradeOrRecipe,
                x.SourceSystem,
                null,
                null,
                null,
                null,
                x.ProductionStartUtc,
                1m))
            .Take(resolved.RawRowLimit)
            .ToListAsync(cancellationToken);

        var defectiveMaterialIds = await _dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => !x.IsDeleted && materialIds.Contains(x.MaterialUnitId))
            .Select(x => x.MaterialUnitId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var defectiveSet = defectiveMaterialIds.ToHashSet();

        var grouped = materialFacts
            .GroupBy(x => ResolveDimension(resolved.DimensionCode, x))
            .Select(g =>
            {
                var totalMaterials = g.Select(x => x.MaterialUnitId).Distinct().Count();
                var defectiveMaterials = g
                    .Select(x => x.MaterialUnitId)
                    .Distinct()
                    .Count(id => id.HasValue && defectiveSet.Contains(id.Value));

                var rate = totalMaterials == 0
                    ? 0m
                    : Math.Round(defectiveMaterials * 100m / totalMaterials, 4);

                return new DashboardAggregateRow(
                    g.Key.Key,
                    g.Key.Label,
                    rate,
                    totalMaterials,
                    defectiveMaterials);
            });

        return SortAndTake(grouped, resolved);
    }

    /// <summary>
    /// observationCount: how many parameter observations exist, grouped by the
    /// chosen dimension.
    ///
    /// It was declared in the registry, published by the metadata endpoint and
    /// offered in the authoring panel, and it had no implementation at all - the
    /// only reference to the constant in the backend was its own declaration.
    /// Five widgets across three dashboards bound to it and every one returned
    /// nothing, with HTTP 200.
    ///
    /// Modelled on ExecuteParameterAggregateAsync so it inherits the same joins,
    /// the same material filter, the same date filter and the same dimension
    /// resolution. Two deliberate differences:
    ///
    ///   The parameter code is OPTIONAL here. Counting how many observations a
    ///   piece of equipment produced is a meaningful question without naming a
    ///   parameter, whereas averaging across different parameters is not. When a
    ///   parameter IS given the count narrows to it.
    ///
    ///   A null numeric value is still an observation. The aggregate methods
    ///   exclude them because there is nothing to average; a count must not,
    ///   or a text-valued reading would vanish from its own tally.
    /// </summary>
    private async Task<IReadOnlyList<DashboardAggregateRow>> ExecuteObservationCountAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetFiltersDto? filters,
        CancellationToken cancellationToken)
    {
        var parameterCode = resolved.ParameterCode ?? filters?.ParameterCode;
        var materialIds = await GetFilteredMaterialIdsAsync(filters, cancellationToken);

        var facts = await (
                from observation in _dbContext.ParameterObservations.AsNoTracking()
                join material in _dbContext.MaterialUnits.AsNoTracking()
                    on observation.MaterialUnitId equals material.Id
                join parameter in _dbContext.ParameterDefinitions.AsNoTracking()
                    on observation.ParameterDefinitionId equals parameter.Id
                join equipment in _dbContext.Equipment.AsNoTracking()
                    on observation.EquipmentId equals equipment.Id into equipmentJoin
                from equipment in equipmentJoin.DefaultIfEmpty()
                where
                    !observation.IsDeleted &&
                    !material.IsDeleted &&
                    materialIds.Contains(observation.MaterialUnitId) &&
                    (parameterCode == null || parameter.ParameterCode == parameterCode)
                select new WidgetFact(
                    observation.MaterialUnitId,
                    material.SiteId,
                    equipment != null ? equipment.AreaId : null,
                    observation.EquipmentId,
                    material.MaterialCode,
                    material.MaterialUnitType,
                    material.ProductFamily,
                    material.GradeOrRecipe,
                    material.SourceSystem,
                    null,
                    null,
                    parameter.ParameterCode,
                    null,
                    observation.ObservedAtUtc,
                    1m))
            .Take(resolved.RawRowLimit)
            .ToListAsync(cancellationToken);

        facts = ApplyFactDateFilter(facts, filters).ToList();

        var grouped = facts
            .GroupBy(x => ResolveDimension(resolved.DimensionCode, x))
            .Select(g => new DashboardAggregateRow(
                g.Key.Key,
                g.Key.Label,
                g.Count(),
                g.Count(),
                0));

        return SortAndTake(grouped, resolved);
    }
    /// <summary>
    /// Every measure this engine can actually execute. It exists so that a code
    /// published in the metadata registry but absent from the switch below is
    /// refused by name rather than answered with an empty result. Adding an arm
    /// to the switch without adding it here is caught on the first request.
    /// </summary>
    private static readonly HashSet<string> ExecutableMeasures = new(StringComparer.Ordinal)
    {
        DashboardMetadataCodes.Measures.MaterialCount,
        DashboardMetadataCodes.Measures.DefectCount,
        DashboardMetadataCodes.Measures.ObservationCount,
        DashboardMetadataCodes.Measures.DefectRate,
        DashboardMetadataCodes.Measures.AvgParameterValue,
        DashboardMetadataCodes.Measures.MaxParameterValue,
        DashboardMetadataCodes.Measures.MinParameterValue,
        DashboardMetadataCodes.Measures.DowntimeMinutes,
        DashboardMetadataCodes.Measures.RiskScore,
        DashboardMetadataCodes.Measures.ProcessStepDuration,
        DashboardMetadataCodes.Measures.DataQualityIssueCount,
    };
    private async Task<IReadOnlyList<DashboardAggregateRow>> ExecuteParameterAggregateAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetFiltersDto? filters,
        ParameterAggregationMode mode,
        CancellationToken cancellationToken)
    {
        var parameterCode = resolved.ParameterCode ?? filters?.ParameterCode;

        if (string.IsNullOrWhiteSpace(parameterCode))
            return Array.Empty<DashboardAggregateRow>();

        var materialIds = await GetFilteredMaterialIdsAsync(filters, cancellationToken);

        var facts = await (
                from observation in _dbContext.ParameterObservations.AsNoTracking()
                join material in _dbContext.MaterialUnits.AsNoTracking()
                    on observation.MaterialUnitId equals material.Id
                join parameter in _dbContext.ParameterDefinitions.AsNoTracking()
                    on observation.ParameterDefinitionId equals parameter.Id
                join equipment in _dbContext.Equipment.AsNoTracking()
                    on observation.EquipmentId equals equipment.Id into equipmentJoin
                from equipment in equipmentJoin.DefaultIfEmpty()
                where
                    !observation.IsDeleted &&
                    !material.IsDeleted &&
                    materialIds.Contains(observation.MaterialUnitId) &&
                    observation.NumericValue != null &&
                    parameter.ParameterCode == parameterCode
                select new WidgetFact(
                    observation.MaterialUnitId,
                    material.SiteId,
                    equipment != null ? equipment.AreaId : null,
                    observation.EquipmentId,
                    material.MaterialCode,
                    material.MaterialUnitType,
                    material.ProductFamily,
                    material.GradeOrRecipe,
                    material.SourceSystem,
                    null,
                    null,
                    parameter.ParameterCode,
                    null,
                    observation.ObservedAtUtc,
                    observation.NumericValue!.Value))
            .Take(resolved.RawRowLimit)
            .ToListAsync(cancellationToken);

        facts = ApplyFactDateFilter(facts, filters).ToList();

        var grouped = facts
            .GroupBy(x => ResolveDimension(resolved.DimensionCode, x))
            .Select(g =>
            {
                var value = mode switch
                {
                    ParameterAggregationMode.Maximum => g.Max(x => x.Value),
                    ParameterAggregationMode.Minimum => g.Min(x => x.Value),
                    _ => g.Average(x => x.Value)
                };

                return new DashboardAggregateRow(
                    g.Key.Key,
                    g.Key.Label,
                    Math.Round(value, 4),
                    g.Count(),
                    0);
            });

        return SortAndTake(grouped, resolved);
    }

    private async Task<IReadOnlyList<DashboardAggregateRow>> ExecuteDowntimeMinutesAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetFiltersDto? filters,
        CancellationToken cancellationToken)
    {
        var materialIds = await GetFilteredMaterialIdsAsync(filters, cancellationToken);

        var facts = await (
                from downtime in _dbContext.DowntimeEvents.AsNoTracking()
                join material in _dbContext.MaterialUnits.AsNoTracking()
                    on downtime.MaterialUnitId equals material.Id into materialJoin
                from material in materialJoin.DefaultIfEmpty()
                join equipment in _dbContext.Equipment.AsNoTracking()
                    on downtime.EquipmentId equals equipment.Id into equipmentJoin
                from equipment in equipmentJoin.DefaultIfEmpty()
                where
                    !downtime.IsDeleted &&
                    (downtime.MaterialUnitId == null || materialIds.Contains(downtime.MaterialUnitId.GetValueOrDefault()))                select new WidgetFact(
                    downtime.MaterialUnitId,
                    material != null ? material.SiteId : null,
                    equipment != null ? equipment.AreaId : null,
                    downtime.EquipmentId,
                    material != null ? material.MaterialCode : null,
                    material != null ? material.MaterialUnitType : null,
                    material != null ? material.ProductFamily : null,
                    material != null ? material.GradeOrRecipe : null,
                    downtime.SourceSystem,
                    null,
                    null,
                    null,
                    null,
                    downtime.StartedAtUtc,
                    downtime.EndedAtUtc == null
                    ? 0m
                    : (decimal)Math.Max(
                        0,
                        (downtime.EndedAtUtc.GetValueOrDefault() - downtime.StartedAtUtc).TotalMinutes)))
                            .Take(resolved.RawRowLimit)
            .ToListAsync(cancellationToken);

        facts = ApplyFactDateFilter(facts, filters).ToList();

        return AggregateSum(facts, resolved);
    }

    private async Task<IReadOnlyList<DashboardAggregateRow>> ExecuteRiskScoreAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetFiltersDto? filters,
        CancellationToken cancellationToken)
    {
        var materialIds = await GetFilteredMaterialIdsAsync(filters, cancellationToken);

        var facts = await (
                from risk in _dbContext.RiskScores.AsNoTracking()
                join material in _dbContext.MaterialUnits.AsNoTracking()
                    on risk.MaterialUnitId equals material.Id
                where
                    !risk.IsDeleted &&
                    !material.IsDeleted &&
                    materialIds.Contains(risk.MaterialUnitId)
                select new WidgetFact(
                    risk.MaterialUnitId,
                    material.SiteId,
                    null,
                    null,
                    material.MaterialCode,
                    material.MaterialUnitType,
                    material.ProductFamily,
                    material.GradeOrRecipe,
                    material.SourceSystem,
                    null,
                    null,
                    null,
                    risk.RiskClass,
                    risk.ScoredAtUtc,
                    risk.Score))
            .Take(resolved.RawRowLimit)
            .ToListAsync(cancellationToken);

        facts = ApplyFactDateFilter(facts, filters).ToList();

        var grouped = facts
            .GroupBy(x => ResolveDimension(resolved.DimensionCode, x))
            .Select(g => new DashboardAggregateRow(
                g.Key.Key,
                g.Key.Label,
                Math.Round(g.Average(x => x.Value), 4),
                g.Count(),
                0));

        return SortAndTake(grouped, resolved);
    }

    private async Task<IReadOnlyList<DashboardAggregateRow>> ExecuteProcessStepDurationAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetFiltersDto? filters,
        CancellationToken cancellationToken)
    {
        var materialIds = await GetFilteredMaterialIdsAsync(filters, cancellationToken);

        var facts = await (
                from step in _dbContext.ProcessStepExecutions.AsNoTracking()
                join material in _dbContext.MaterialUnits.AsNoTracking()
                    on step.MaterialUnitId equals material.Id
                join equipment in _dbContext.Equipment.AsNoTracking()
                    on step.EquipmentId equals equipment.Id
                where
                    !step.IsDeleted &&
                    !material.IsDeleted &&
                    materialIds.Contains(step.MaterialUnitId) &&
                    step.EndedAtUtc != null
                select new WidgetFact(
                    step.MaterialUnitId,
                    material.SiteId,
                    equipment.AreaId,
                    step.EquipmentId,
                    material.MaterialCode,
                    material.MaterialUnitType,
                    material.ProductFamily,
                    material.GradeOrRecipe,
                    material.SourceSystem,
                    step.CrewCode,
                    null,
                    null,
                    null,
                    step.StartedAtUtc,
                    (decimal)Math.Max(0, (step.EndedAtUtc!.Value - step.StartedAtUtc).TotalMinutes)))
            .Take(resolved.RawRowLimit)
            .ToListAsync(cancellationToken);

        facts = ApplyFactDateFilter(facts, filters).ToList();

        var grouped = facts
            .GroupBy(x => ResolveDimension(resolved.DimensionCode, x))
            .Select(g => new DashboardAggregateRow(
                g.Key.Key,
                g.Key.Label,
                Math.Round(g.Average(x => x.Value), 2),
                g.Count(),
                0));

        return SortAndTake(grouped, resolved);
    }

    private async Task<IReadOnlyList<DashboardAggregateRow>> ExecuteDataQualityIssueCountAsync(
        DashboardWidgetResolvedDto resolved,
        DashboardWidgetFiltersDto? filters,
        CancellationToken cancellationToken)
    {
        var materialIds = await GetFilteredMaterialIdsAsync(filters, cancellationToken);

        var facts = await _dbContext.DataQualityIssues
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (!x.MaterialUnitId.HasValue || materialIds.Contains(x.MaterialUnitId.Value)))
            .Select(x => new WidgetFact(
                x.MaterialUnitId,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                x.SourceSystem,
                null,
                x.IssueType,
                null,
                null,
                x.CreatedAtUtc,
                1m))
            .Take(resolved.RawRowLimit)
            .ToListAsync(cancellationToken);

        facts = ApplyFactDateFilter(facts, filters).ToList();

        return AggregateCount(facts, resolved);
    }

    private async Task<HashSet<Guid>> GetFilteredMaterialIdsAsync(
        DashboardWidgetFiltersDto? filters,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (filters?.SiteId.HasValue == true)
            query = query.Where(x => x.SiteId == filters.SiteId.Value);

       if (!string.IsNullOrWhiteSpace(filters?.MaterialCode))
        {
            var materialCode = filters.MaterialCode.Trim();
            query = query.Where(x => x.MaterialCode.Contains(materialCode));
        }

        if (!string.IsNullOrWhiteSpace(filters?.MaterialUnitType))
        {
            var materialUnitType = filters.MaterialUnitType.Trim();
            query = query.Where(x => x.MaterialUnitType == materialUnitType);
        }

        if (!string.IsNullOrWhiteSpace(filters?.SourceSystem))
        {
            var sourceSystem = filters.SourceSystem.Trim();
            query = query.Where(x => x.SourceSystem == sourceSystem);
        }
        
        if (filters?.FromUtc.HasValue == true)
            query = query.Where(x => x.ProductionStartUtc == null || x.ProductionStartUtc >= filters.FromUtc.Value);

        if (filters?.ToUtc.HasValue == true)
            query = query.Where(x => x.ProductionStartUtc == null || x.ProductionStartUtc <= filters.ToUtc.Value);

        var materialIds = await query
            .Select(x => x.Id)
            .Take(DashboardWidgetQuerySafetyRegistry.AbsoluteRawRowLimit)
            .ToListAsync(cancellationToken);

        var result = materialIds.ToHashSet();

        if (filters?.AreaId.HasValue == true)
        {
            var areaEquipmentIds = await _dbContext.Equipment
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.AreaId == filters.AreaId.Value)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            var areaMaterialIds = await _dbContext.ProcessStepExecutions
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.EquipmentId.HasValue &&
                areaEquipmentIds.Contains(x.EquipmentId.Value))
            .Select(x => x.MaterialUnitId)
            .Distinct()
            .ToListAsync(cancellationToken);

            result.IntersectWith(areaMaterialIds);
        }

        if (filters?.EquipmentId.HasValue == true)
        {
            var equipmentMaterialIds = await _dbContext.ProcessStepExecutions
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.EquipmentId == filters.EquipmentId.Value)
                .Select(x => x.MaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            result.IntersectWith(equipmentMaterialIds);
        }

        if (!string.IsNullOrWhiteSpace(filters?.ShiftCode))
        {
            var shiftMaterialIds = await _dbContext.ProcessStepExecutions
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.CrewCode == filters.ShiftCode)
                .Select(x => x.MaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            result.IntersectWith(shiftMaterialIds);
        }

        if (!string.IsNullOrWhiteSpace(filters?.RiskClass))
        {
            var riskMaterialIds = await _dbContext.RiskScores
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.RiskClass == filters.RiskClass)
                .Select(x => x.MaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            result.IntersectWith(riskMaterialIds);
        }

        if (!string.IsNullOrWhiteSpace(filters?.DefectType))
        {
            var defectMaterialIds = await (
                    from qualityEvent in _dbContext.QualityEvents.AsNoTracking()
                    join defect in _dbContext.DefectCatalogs.AsNoTracking()
                        on qualityEvent.DefectCatalogId equals defect.Id into defectJoin
                    from defect in defectJoin.DefaultIfEmpty()
                    where
                        !qualityEvent.IsDeleted &&
                        (
                            qualityEvent.EventType == filters.DefectType ||
                            defect != null && defect.DefectCode == filters.DefectType ||
                            defect != null && defect.DefectName == filters.DefectType
                        )
                    select qualityEvent.MaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            result.IntersectWith(defectMaterialIds);
        }

        return result;
    }

    private static IEnumerable<WidgetFact> ApplyFactDateFilter(
        IEnumerable<WidgetFact> facts,
        DashboardWidgetFiltersDto? filters)
    {
        var result = facts;

        if (filters?.FromUtc.HasValue == true)
            result = result.Where(x => !x.EventTimeUtc.HasValue || x.EventTimeUtc >= filters.FromUtc.Value);

        if (filters?.ToUtc.HasValue == true)
            result = result.Where(x => !x.EventTimeUtc.HasValue || x.EventTimeUtc <= filters.ToUtc.Value);

        return result;
    }

    private static IReadOnlyList<DashboardAggregateRow> AggregateCount(
        IEnumerable<WidgetFact> facts,
        DashboardWidgetResolvedDto resolved)
    {
        var grouped = facts
            .GroupBy(x => ResolveDimension(resolved.DimensionCode, x))
            .Select(g => new DashboardAggregateRow(
                g.Key.Key,
                g.Key.Label,
                g.Count(),
                g.Count(),
                0));

        return SortAndTake(grouped, resolved);
    }

    private static IReadOnlyList<DashboardAggregateRow> AggregateSum(
        IEnumerable<WidgetFact> facts,
        DashboardWidgetResolvedDto resolved)
    {
        var grouped = facts
            .GroupBy(x => ResolveDimension(resolved.DimensionCode, x))
            .Select(g => new DashboardAggregateRow(
                g.Key.Key,
                g.Key.Label,
                Math.Round(g.Sum(x => x.Value), 2),
                g.Count(),
                0));

        return SortAndTake(grouped, resolved);
    }

    private static IReadOnlyList<DashboardAggregateRow> SortAndTake(
        IEnumerable<DashboardAggregateRow> rows,
        DashboardWidgetResolvedDto resolved)
    {
        var sorted = resolved.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? rows.OrderBy(x => x.Value).ThenBy(x => x.DimensionLabel)
            : rows.OrderByDescending(x => x.Value).ThenBy(x => x.DimensionLabel);

        return sorted
            .Take(resolved.MaxRows)
            .ToList();
    }

    private static DashboardWidgetQueryResultDto BuildResult(
        DashboardWidgetResolvedDto resolved,
        IReadOnlyList<DashboardAggregateRow> aggregateRows,
        IReadOnlyList<string> warnings,
        IReadOnlyDictionary<string, string> dimensionLabels)
    {
        var dimensionCode = resolved.DimensionCode ?? "kpi";

        var columns = new List<DashboardWidgetColumnDto>
        {
            new(dimensionCode, resolved.DimensionCode ?? "KPI", "string"),
            new("dimensionLabel", "Dimension Label", "string"),
            new("value", "Value", "number"),
            new("observationCount", "Observation Count", "number"),
            new("secondaryCount", "Secondary Count", "number")
        };

        var rows = aggregateRows
            .Select(row => new Dictionary<string, object?>
            {
                [dimensionCode] = row.DimensionKey,
                ["dimensionLabel"] = LabelFor(dimensionLabels, row.DimensionKey, row.DimensionLabel),
                ["value"] = row.Value,
                ["observationCount"] = row.ObservationCount,
                ["secondaryCount"] = row.SecondaryCount
            } as IDictionary<string, object?>)
            .ToList();

        return new DashboardWidgetQueryResultDto(
            DateTime.UtcNow,
            resolved,
            columns,
            rows,
            warnings);
    }

    /// <summary>
    /// Names for the dimension actually being drawn, and nothing else. Two
    /// dimensions are keyed by an id whose name lives in another table, so
    /// without this a bar chart reads as a column of GUIDs. Loaded once per
    /// query rather than joined into every fact projection, which would have
    /// meant touching ten of them.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> LoadDimensionLabelsAsync(
        string? dimensionCode,
        CancellationToken cancellationToken)
    {
        if (IsDimensionCode(dimensionCode, DashboardMetadataCodes.Dimensions.Equipment))
        {
            return await _dbContext.Equipment
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .ToDictionaryAsync(x => x.Id.ToString(), x => x.EquipmentName, cancellationToken);
        }

        if (IsDimensionCode(dimensionCode, DashboardMetadataCodes.Dimensions.Area))
        {
            return await _dbContext.Areas
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .ToDictionaryAsync(x => x.Id.ToString(), x => x.AreaName, cancellationToken);
        }

        return new Dictionary<string, string>();
    }

    private static bool IsDimensionCode(string? dimensionCode, string expected)
    {
        return string.Equals(dimensionCode, expected, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The name where one exists, the label already computed where it does not.
    /// A missing name is never invented and never blanked - a row whose id has
    /// no matching record keeps reading as that id, which is the truth.
    /// </summary>
    private static string LabelFor(
        IReadOnlyDictionary<string, string> labels,
        string key,
        string fallback)
    {
        if (labels.TryGetValue(key, out var name) && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return fallback;
    }

    private static bool IsDimension(DashboardWidgetResolvedDto resolved, string dimensionCode)
    {
        return string.Equals(resolved.DimensionCode, dimensionCode, StringComparison.OrdinalIgnoreCase);
    }

    private static DimensionValue ResolveDimension(string? dimensionCode, WidgetFact fact)
    {
        if (string.IsNullOrWhiteSpace(dimensionCode))
            return new DimensionValue("kpi", "KPI");

        return dimensionCode switch
        {
            DashboardMetadataCodes.Dimensions.Site =>
                BuildDimension(fact.SiteId?.ToString(), fact.SiteId?.ToString(), "No site"),

            DashboardMetadataCodes.Dimensions.Area =>
                BuildDimension(fact.AreaId?.ToString(), fact.AreaId?.ToString(), "No area"),

            DashboardMetadataCodes.Dimensions.Equipment =>
                BuildDimension(fact.EquipmentId?.ToString(), fact.EquipmentId?.ToString(), "No equipment"),

            DashboardMetadataCodes.Dimensions.SourceSystem =>
                BuildDimension(fact.SourceSystem, fact.SourceSystem, "No source system"),

            DashboardMetadataCodes.Dimensions.MaterialUnitType =>
                BuildDimension(fact.MaterialUnitType, fact.MaterialUnitType, "No material type"),

            DashboardMetadataCodes.Dimensions.ProductFamily =>
                BuildDimension(fact.ProductFamily, fact.ProductFamily, "No product family"),

            DashboardMetadataCodes.Dimensions.GradeOrRecipe =>
                BuildDimension(fact.GradeOrRecipe, fact.GradeOrRecipe, "No grade / recipe"),

            DashboardMetadataCodes.Dimensions.ShiftCode =>
                BuildDimension(fact.ShiftCode, fact.ShiftCode, "No shift"),

            DashboardMetadataCodes.Dimensions.DefectType =>
                BuildDimension(fact.DefectType, fact.DefectType, "No defect"),

            DashboardMetadataCodes.Dimensions.ParameterCode =>
                BuildDimension(fact.ParameterCode, fact.ParameterCode, "No parameter"),

            DashboardMetadataCodes.Dimensions.RiskClass =>
                BuildDimension(fact.RiskClass, fact.RiskClass, "No risk class"),

            DashboardMetadataCodes.Dimensions.Day =>
                BuildDateDimension(fact.EventTimeUtc, "yyyy-MM-dd", "No day"),

            DashboardMetadataCodes.Dimensions.Week =>
                BuildWeekDimension(fact.EventTimeUtc),

            DashboardMetadataCodes.Dimensions.Month =>
                BuildDateDimension(fact.EventTimeUtc, "yyyy-MM", "No month"),

            _ => new DimensionValue("unknown", "Unknown")
        };
    }

    private static DimensionValue BuildDimension(
        string? key,
        string? label,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new DimensionValue("unknown", fallback);

        return new DimensionValue(key.Trim(), string.IsNullOrWhiteSpace(label) ? key.Trim() : label.Trim());
    }

    private static DimensionValue BuildDateDimension(
        DateTime? value,
        string format,
        string fallback)
    {
        if (!value.HasValue)
            return new DimensionValue("unknown", fallback);

        var text = value.Value.ToString(format);
        return new DimensionValue(text, text);
    }

    private static DimensionValue BuildWeekDimension(DateTime? value)
    {
        if (!value.HasValue)
            return new DimensionValue("unknown", "No week");

        var date = value.Value.Date;
        var firstDayOfYear = new DateTime(date.Year, 1, 1);
        var week = (int)Math.Ceiling((date.DayOfYear + (int)firstDayOfYear.DayOfWeek) / 7.0);
        var key = $"{date.Year}-W{week:00}";

        return new DimensionValue(key, key);
    }

    private sealed record WidgetFact(
        Guid? MaterialUnitId,
        Guid? SiteId,
        Guid? AreaId,
        Guid? EquipmentId,
        string? MaterialCode,
        string? MaterialUnitType,
        string? ProductFamily,
        string? GradeOrRecipe,
        string? SourceSystem,
        string? ShiftCode,
        string? DefectType,
        string? ParameterCode,
        string? RiskClass,
        DateTime? EventTimeUtc,
        decimal Value);

    private sealed record DashboardAggregateRow(
        string DimensionKey,
        string DimensionLabel,
        decimal Value,
        int ObservationCount,
        int SecondaryCount);

    private sealed record DimensionValue(string Key, string Label);

    private enum ParameterAggregationMode
    {
        Average,
        Maximum,
        Minimum
    }
}



