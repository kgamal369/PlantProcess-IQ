using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Analytics.Services;

public sealed class FeatureEngineeringService : IFeatureEngineeringService
{
    public const string CurrentFeatureVersion = "feature-vector-v1.0-rule-ready";

    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<FeatureEngineeringService> _logger;

    public FeatureEngineeringService(
        IPlantProcessDbContext dbContext,
        ILogger<FeatureEngineeringService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApplicationResult<MaterialFeatureVectorDto>> BuildMaterialFeatureVectorAsync(
        Guid materialUnitId,
        CancellationToken cancellationToken)
    {
        if (materialUnitId == Guid.Empty)
            return ApplicationResult<MaterialFeatureVectorDto>.Failure(ApplicationError.Validation("Material unit ID is required."));

        var generatedAtUtc = DateTime.UtcNow;

        var material = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.Id == materialUnitId && !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.MaterialCode,
                x.MaterialUnitType,
                x.SiteId,
                x.ProductFamily,
                x.GradeOrRecipe,
                x.ProductionStartUtc,
                x.ProductionEndUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
            return ApplicationResult<MaterialFeatureVectorDto>.Failure(ApplicationError.NotFound("Material unit does not exist."));

        var stepsRaw = await _dbContext.ProcessStepExecutions
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderBy(x => x.StartedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.EquipmentId,
                x.OperationDefinitionId,
                x.OperationType,
                x.OperationCode,
                x.CrewCode,
                x.ExecutionStatus,
                x.StartedAtUtc,
                x.EndedAtUtc
            })
            .ToListAsync(cancellationToken);

        var equipmentIds = stepsRaw
            .Where(x => x.EquipmentId.HasValue)
            .Select(x => x.EquipmentId!.Value)
            .Distinct()
            .ToList();

        var equipmentLookup = await _dbContext.Equipment
            .AsNoTracking()
            .Where(x => equipmentIds.Contains(x.Id))
            .Select(x => new EquipmentLookupDto(x.Id, x.EquipmentCode, x.EquipmentName, x.EquipmentType))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var processSteps = stepsRaw.Select(x =>
        {
            equipmentLookup.TryGetValue(x.EquipmentId ?? Guid.Empty, out var equipment);
            return new ProcessStepFeatureDto(
                x.Id,
                x.EquipmentId,
                equipment?.EquipmentCode,
                equipment?.EquipmentName,
                x.OperationDefinitionId,
                x.OperationType,
                x.OperationCode,
                x.CrewCode,
                NormalizeStatus(x.ExecutionStatus),
                x.StartedAtUtc,
                x.EndedAtUtc,
                CalculateMinutes(x.StartedAtUtc, x.EndedAtUtc));
        }).ToList();

        var stepIds = stepsRaw.Select(x => x.Id).ToList();

        var observationsRaw = await _dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (x.MaterialUnitId == materialUnitId ||
                 (x.ProcessStepExecutionId.HasValue && stepIds.Contains(x.ProcessStepExecutionId.Value))))
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.ProcessStepExecutionId,
                x.ParameterDefinitionId,
                x.EquipmentId,
                x.ObservedAtUtc,
                x.NumericValue,
                x.TextValue,
                x.BooleanValue,
                x.UnitOfMeasure,
                x.QualityFlag
            })
            .ToListAsync(cancellationToken);

        var parameterDefinitionIds = observationsRaw.Select(x => x.ParameterDefinitionId).Distinct().ToList();
        var parameterDefinitions = await _dbContext.ParameterDefinitions
            .AsNoTracking()
            .Where(x => parameterDefinitionIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.ParameterCode,
                x.ParameterName,
                x.ValueType,
                x.UnitOfMeasure,
                x.ParameterCategory,
                x.IndustryTemplate,
                x.ExpectedMinValue,
                x.ExpectedMaxValue
            })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var parameterAggregates = observationsRaw
            .GroupBy(x => x.ParameterDefinitionId)
            .Select(group =>
            {
                parameterDefinitions.TryGetValue(group.Key, out var definition);
                var ordered = group.OrderBy(x => x.ObservedAtUtc).ToList();
                var numericValues = ordered.Where(x => x.NumericValue.HasValue).Select(x => x.NumericValue!.Value).ToList();
                var latest = ordered.LastOrDefault();
                var below = definition is null || !definition.ExpectedMinValue.HasValue
                    ? 0
                    : numericValues.Count(v => v < definition.ExpectedMinValue.Value);
                var above = definition is null || !definition.ExpectedMaxValue.HasValue
                    ? 0
                    : numericValues.Count(v => v > definition.ExpectedMaxValue.Value);
                var missing = ordered.Count(x => !x.NumericValue.HasValue && string.IsNullOrWhiteSpace(x.TextValue) && !x.BooleanValue.HasValue);
                var invalidFlags = ordered.Count(x => !string.IsNullOrWhiteSpace(x.QualityFlag) && !IsGoodQualityFlag(x.QualityFlag));
                var anomalyCount = below + above + missing + invalidFlags;
                var count = Math.Max(ordered.Count, 1);

                return new ParameterAggregateFeatureDto(
                    group.Key,
                    definition?.ParameterCode ?? group.Key.ToString("N"),
                    definition?.ParameterName ?? "Unknown parameter",
                    definition?.ParameterCategory,
                    definition?.IndustryTemplate,
                    definition?.ValueType ?? "Unknown",
                    definition?.UnitOfMeasure ?? latest?.UnitOfMeasure,
                    ordered.Count,
                    numericValues.Count == 0 ? null : numericValues.Min(),
                    numericValues.Count == 0 ? null : numericValues.Max(),
                    numericValues.Count == 0 ? null : numericValues.Average(),
                    CalculateStdDev(numericValues),
                    latest?.NumericValue,
                    latest?.TextValue,
                    latest?.BooleanValue,
                    latest?.ObservedAtUtc,
                    definition?.ExpectedMinValue,
                    definition?.ExpectedMaxValue,
                    below,
                    above,
                    missing,
                    invalidFlags,
                    Math.Round((decimal)anomalyCount / count, 6));
            })
            .OrderByDescending(x => x.AnomalyRatio)
            .ThenBy(x => x.ParameterCode)
            .ToList();

        var qualityRaw = await _dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderBy(x => x.EventAtUtc)
            .Select(x => new
            {
                x.Id,
                x.EventType,
                x.DefectCatalogId,
                x.Severity,
                x.Decision,
                x.EventAtUtc,
                x.Description
            })
            .ToListAsync(cancellationToken);

        var defectIds = qualityRaw.Where(x => x.DefectCatalogId.HasValue).Select(x => x.DefectCatalogId!.Value).Distinct().ToList();
        var defectLookup = await _dbContext.DefectCatalogs
            .AsNoTracking()
            .Where(x => defectIds.Contains(x.Id))
            .Select(x => new { x.Id, x.DefectCode, x.DefectName, x.DefectCategory })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var qualitySignals = qualityRaw.Select(x =>
        {
            defectLookup.TryGetValue(x.DefectCatalogId ?? Guid.Empty, out var defect);
            return new QualitySignalFeatureDto(
                x.Id,
                x.EventType,
                defect?.DefectCode,
                defect?.DefectName,
                defect?.DefectCategory,
                x.Severity,
                x.Decision,
                x.EventAtUtc,
                x.Description);
        }).ToList();

        var dataQualitySignals = await _dbContext.DataQualityIssues
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new DataQualitySignalFeatureDto(
                x.Id,
                x.IssueType,
                x.Severity,
                x.Description,
                x.AffectedEntityName,
                x.AffectedEntityId,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var downtimeRaw = await _dbContext.DowntimeEvents
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                ((x.MaterialUnitId.HasValue && x.MaterialUnitId.Value == materialUnitId) ||
                 (x.ProcessStepExecutionId.HasValue && stepIds.Contains(x.ProcessStepExecutionId.Value))))
            .Select(x => new { x.Id, x.StartedAtUtc, x.EndedAtUtc })
            .ToListAsync(cancellationToken);

        var equipmentExposure = BuildEquipmentExposure(
            processSteps,
            observationsRaw.Select(x => new ObservationEquipmentProjection(x.EquipmentId)).ToList(),
            qualitySignals,
            equipmentLookup);

        var correlationHints = await BuildCorrelationHintsAsync(parameterAggregates, qualitySignals, cancellationToken);

        var parameterObservationCount = observationsRaw.Count;
        var parameterAnomalyCount = parameterAggregates.Sum(x => x.BelowExpectedCount + x.AboveExpectedCount + x.MissingValueCount + x.InvalidQualityFlagCount);

        var vector = new MaterialFeatureVectorDto(
            material.Id,
            material.MaterialCode,
            material.MaterialUnitType,
            material.SiteId,
            material.ProductFamily,
            material.GradeOrRecipe,
            material.ProductionStartUtc,
            material.ProductionEndUtc,
            CalculateMinutes(material.ProductionStartUtc, material.ProductionEndUtc),
            processSteps.Count,
            processSteps.Count(x => x.ExecutionStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)),
            processSteps.Count(x => x.ExecutionStatus.Equals("Running", StringComparison.OrdinalIgnoreCase)),
            processSteps.Count(x => x.ExecutionStatus.Equals("Aborted", StringComparison.OrdinalIgnoreCase)),
            processSteps.Where(x => x.EquipmentId.HasValue).Select(x => x.EquipmentId!.Value).Distinct().Count(),
            processSteps.Where(x => !string.IsNullOrWhiteSpace(x.CrewCode)).Select(x => x.CrewCode).Distinct().Count(),
            processSteps.Sum(x => x.DurationMinutes),
            downtimeRaw.Sum(x => CalculateMinutes(x.StartedAtUtc, x.EndedAtUtc)),
            downtimeRaw.Count,
            parameterObservationCount,
            parameterAnomalyCount,
            parameterObservationCount == 0 ? 0 : Math.Round((decimal)parameterAnomalyCount / parameterObservationCount, 6),
            qualitySignals.Count,
            qualitySignals.Count(x => x.EventType.Equals("Defect", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(x.DefectCode)),
            dataQualitySignals.Count,
            CurrentFeatureVersion,
            generatedAtUtc,
            processSteps,
            equipmentExposure,
            parameterAggregates,
            qualitySignals,
            dataQualitySignals,
            correlationHints);

        _logger.LogInformation(
            "Built material feature vector. MaterialUnitId={MaterialUnitId}, Parameters={ParameterCount}, Steps={StepCount}, QualitySignals={QualityCount}, DataQualityIssues={DataQualityIssueCount}",
            vector.MaterialUnitId,
            vector.ParameterAggregates.Count,
            vector.ProcessStepCount,
            vector.QualityEventCount,
            vector.DataQualityIssueCount);

        return ApplicationResult<MaterialFeatureVectorDto>.Success(vector);
    }

    private async Task<List<CorrelationHintFeatureDto>> BuildCorrelationHintsAsync(
        IReadOnlyList<ParameterAggregateFeatureDto> parameterAggregates,
        IReadOnlyList<QualitySignalFeatureDto> qualitySignals,
        CancellationToken cancellationToken)
    {
        var parameterCodes = parameterAggregates.Select(x => x.ParameterCode).Distinct().ToList();
        var outcomeCodes = qualitySignals
            .Select(x => x.DefectCode ?? x.EventType)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (parameterCodes.Count == 0 || outcomeCodes.Count == 0)
            return new List<CorrelationHintFeatureDto>();

        return await _dbContext.CorrelationResults
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                parameterCodes.Contains(x.SubjectCode) &&
                outcomeCodes.Contains(x.OutcomeCode))
            .OrderByDescending(x => x.CalculatedAtUtc)
            .Take(20)
            .Select(x => new CorrelationHintFeatureDto(
                x.Id,
                x.CorrelationType,
                x.SubjectCode,
                x.OutcomeCode,
                x.Score,
                x.CalculatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static List<EquipmentExposureFeatureDto> BuildEquipmentExposure(
        IReadOnlyList<ProcessStepFeatureDto> processSteps,
        IReadOnlyList<ObservationEquipmentProjection> observations,
        IReadOnlyList<QualitySignalFeatureDto> qualitySignals,
        IReadOnlyDictionary<Guid, EquipmentLookupDto> equipmentLookup)
    {
        var result = new List<EquipmentExposureFeatureDto>();

        foreach (var equipmentGroup in processSteps.Where(x => x.EquipmentId.HasValue).GroupBy(x => x.EquipmentId!.Value))
        {
            var key = equipmentGroup.Key;
            equipmentLookup.TryGetValue(key, out var equipment);

            result.Add(new EquipmentExposureFeatureDto(
                key,
                equipment?.EquipmentCode ?? key.ToString("N"),
                equipment?.EquipmentName ?? "Unknown equipment",
                equipment?.EquipmentType ?? "Unknown",
                equipmentGroup.Count(),
                equipmentGroup.Sum(x => x.DurationMinutes),
                observations.Count(x => x.EquipmentId == key),
                qualitySignals.Count));
        }

        return result.OrderByDescending(x => x.TotalDurationMinutes).ToList();
    }

    private sealed record ObservationEquipmentProjection(Guid? EquipmentId);

    private sealed record EquipmentLookupDto(
        Guid Id,
        string EquipmentCode,
        string EquipmentName,
        string EquipmentType);

    private static bool IsGoodQualityFlag(string flag)
    {
        return flag.Equals("Valid", StringComparison.OrdinalIgnoreCase) ||
               flag.Equals("Good", StringComparison.OrdinalIgnoreCase) ||
               flag.Equals("OK", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status) ? "Unknown" : status.Trim();
    }

    private static decimal CalculateMinutes(DateTime? startedAtUtc, DateTime? endedAtUtc)
    {
        if (!startedAtUtc.HasValue || !endedAtUtc.HasValue || endedAtUtc.Value <= startedAtUtc.Value)
            return 0m;

        return Math.Round((decimal)(endedAtUtc.Value - startedAtUtc.Value).TotalMinutes, 3);
    }

    private static decimal? CalculateStdDev(IReadOnlyList<decimal> values)
    {
        if (values.Count <= 1)
            return null;

        var avg = values.Average();
        var sumSquares = values.Sum(v => Math.Pow((double)(v - avg), 2));
        return Math.Round((decimal)Math.Sqrt(sumSquares / (values.Count - 1)), 6);
    }
}





