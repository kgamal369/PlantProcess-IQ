using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Domain.Entities.Analytics;

namespace PlantProcess.Application.Analytics.Services;

/// <summary>
/// Correlation engine MVP.
/// It intentionally reports suspected contributors / risk indicators, not guaranteed root cause.
/// </summary>
public sealed class CorrelationService : ICorrelationService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<CorrelationService> _logger;

    public CorrelationService(
        IPlantProcessDbContext dbContext,
        ILogger<CorrelationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApplicationResult<ParameterDefectCorrelationResult>> GetParameterDefectCorrelationAsync(
        ParameterDefectCorrelationQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.ParameterCode))
            return ApplicationResult<ParameterDefectCorrelationResult>.Failure(ApplicationError.Validation("ParameterCode is required."));

        if (string.IsNullOrWhiteSpace(query.DefectType))
            return ApplicationResult<ParameterDefectCorrelationResult>.Failure(ApplicationError.Validation("DefectType is required."));

        var bins = Math.Clamp(query.RequestedBins <= 0 ? 8 : query.RequestedBins, 2, 30);
        var minimumObservationsPerBin = Math.Max(1, query.MinimumObservationsPerBin <= 0 ? 5 : query.MinimumObservationsPerBin);

        var parameterDefinitionIds = await _dbContext.ParameterDefinitions
            .AsNoTracking()
            .Where(x => x.ParameterCode == query.ParameterCode)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (parameterDefinitionIds.Count == 0)
            return ApplicationResult<ParameterDefectCorrelationResult>.Failure(ApplicationError.NotFound($"ParameterDefinition '{query.ParameterCode}' was not found."));

        var observationsQuery = _dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => parameterDefinitionIds.Contains(x.ParameterDefinitionId) && x.NumericValue.HasValue);

        if (query.FromUtc.HasValue)
            observationsQuery = observationsQuery.Where(x => x.ObservedAtUtc >= query.FromUtc.Value);

        if (query.ToUtc.HasValue)
            observationsQuery = observationsQuery.Where(x => x.ObservedAtUtc <= query.ToUtc.Value);

        if (query.SiteId.HasValue)
        {
            observationsQuery = observationsQuery.Join(
                _dbContext.MaterialUnits.AsNoTracking().Where(m => m.SiteId == query.SiteId.Value),
                observation => observation.MaterialUnitId,
                material => material.Id,
                (observation, _) => observation);
        }

        var observations = await observationsQuery
            .Select(x => new
            {
                x.MaterialUnitId,
                NumericValue = x.NumericValue!.Value
            })
            .ToListAsync(cancellationToken);

        if (observations.Count == 0)
            return ApplicationResult<ParameterDefectCorrelationResult>.Failure(ApplicationError.NotFound("No numeric observations found for the requested parameter/filter."));

        var materialIds = observations.Select(x => x.MaterialUnitId).Distinct().ToList();

        var defectMaterialIds = await BuildDefectMaterialQuery(query.DefectType, query.FromUtc, query.ToUtc)
            .Where(x => materialIds.Contains(x.MaterialUnitId))
            .Select(x => x.MaterialUnitId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var defectSet = defectMaterialIds.ToHashSet();
        var materialPopulation = materialIds.Count;
        var defectCount = defectSet.Count;
        var overallDefectRate = materialPopulation == 0 ? 0m : Math.Round((decimal)defectCount / materialPopulation, 6);

        var min = observations.Min(x => x.NumericValue);
        var max = observations.Max(x => x.NumericValue);
        var resultBins = new List<ParameterDefectCorrelationBin>();

        if (min == max)
        {
            var materialsInBin = observations.Select(x => x.MaterialUnitId).Distinct().ToList();
            var defectMaterialsInBin = materialsInBin.Count(defectSet.Contains);
            var rate = materialsInBin.Count == 0 ? 0m : Math.Round((decimal)defectMaterialsInBin / materialsInBin.Count, 6);
            resultBins.Add(new ParameterDefectCorrelationBin(1, $"{min:F3}", min, max, observations.Count, materialsInBin.Count, defectMaterialsInBin, rate, CalculateLift(rate, overallDefectRate)));
        }
        else
        {
            var width = (max - min) / bins;
            for (var i = 0; i < bins; i++)
            {
                var binMin = min + (width * i);
                var binMax = i == bins - 1 ? max : min + (width * (i + 1));
                var binObservations = observations
                    .Where(x => i == bins - 1
                        ? x.NumericValue >= binMin && x.NumericValue <= binMax
                        : x.NumericValue >= binMin && x.NumericValue < binMax)
                    .ToList();

                if (binObservations.Count < minimumObservationsPerBin)
                    continue;

                var binMaterials = binObservations.Select(x => x.MaterialUnitId).Distinct().ToList();
                var binDefectMaterials = binMaterials.Count(defectSet.Contains);
                var binRate = binMaterials.Count == 0 ? 0m : Math.Round((decimal)binDefectMaterials / binMaterials.Count, 6);
                var label = $"[{binMin:F3}, {binMax:F3}{(i == bins - 1 ? "]" : ")")}";

                resultBins.Add(new ParameterDefectCorrelationBin(
                    BinNumber: i + 1,
                    BinLabel: label,
                    MinValue: Math.Round(binMin, 6),
                    MaxValue: Math.Round(binMax, 6),
                    ObservationCount: binObservations.Count,
                    MaterialCount: binMaterials.Count,
                    DefectMaterialCount: binDefectMaterials,
                    DefectRate: binRate,
                    LiftVsOverall: CalculateLift(binRate, overallDefectRate)));
            }
        }

        var strongest = resultBins
            .OrderByDescending(x => x.LiftVsOverall)
            .ThenByDescending(x => x.DefectRate)
            .FirstOrDefault();

        Guid? persistedId = null;
        if (query.PersistResult)
        {
            var summaryJson = JsonSerializer.Serialize(new
            {
                query.ParameterCode,
                query.DefectType,
                query.SiteId,
                query.FromUtc,
                query.ToUtc,
                materialPopulation,
                defectCount,
                overallDefectRate,
                strongestBin = strongest?.BinLabel,
                strongestLift = strongest?.LiftVsOverall,
                bins = resultBins
            });

            var correlation = new CorrelationResult(
                correlationType: "ParameterDefectBinning",
                subjectCode: query.ParameterCode,
                outcomeCode: query.DefectType,
                score: strongest?.LiftVsOverall,
                resultJson: summaryJson,
                isSynthetic: false,
                sourceSystem: "PlantProcessIQ.CorrelationService",
                sourceRecordId: null);

            _dbContext.CorrelationResults.Add(correlation);
            await _dbContext.SaveChangesAsync(cancellationToken);
            persistedId = correlation.Id;
        }

        var result = new ParameterDefectCorrelationResult(
            ParameterCode: query.ParameterCode,
            DefectType: query.DefectType,
            SiteId: query.SiteId,
            FromUtc: query.FromUtc,
            ToUtc: query.ToUtc,
            MaterialPopulation: materialPopulation,
            DefectMaterialCount: defectCount,
            OverallDefectRate: overallDefectRate,
            BinCount: resultBins.Count,
            StrongestLift: strongest?.LiftVsOverall,
            StrongestBinLabel: strongest?.BinLabel,
            PersistedCorrelationResultId: persistedId,
            Bins: resultBins);

        _logger.LogInformation(
            "Parameter-defect correlation calculated. ParameterCode={ParameterCode}, DefectType={DefectType}, Materials={Materials}, DefectMaterials={DefectMaterials}, Bins={Bins}, StrongestLift={StrongestLift}",
            result.ParameterCode,
            result.DefectType,
            result.MaterialPopulation,
            result.DefectMaterialCount,
            result.BinCount,
            result.StrongestLift);

        return ApplicationResult<ParameterDefectCorrelationResult>.Success(result);
    }

    public async Task<ApplicationResult<EquipmentDefectRateResult>> GetEquipmentDefectRateAsync(
        EquipmentDefectRateQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.DefectType))
            return ApplicationResult<EquipmentDefectRateResult>.Failure(ApplicationError.Validation("DefectType is required."));

        var stepsQuery = _dbContext.ProcessStepExecutions.AsNoTracking().Where(x => x.EquipmentId.HasValue);

        if (query.FromUtc.HasValue)
            stepsQuery = stepsQuery.Where(x => x.StartedAtUtc >= query.FromUtc.Value);

        if (query.ToUtc.HasValue)
            stepsQuery = stepsQuery.Where(x => x.StartedAtUtc <= query.ToUtc.Value);

        if (query.SiteId.HasValue)
        {
            stepsQuery = stepsQuery.Join(
                _dbContext.MaterialUnits.AsNoTracking().Where(m => m.SiteId == query.SiteId.Value),
                step => step.MaterialUnitId,
                material => material.Id,
                (step, _) => step);
        }

        var exposureRows = await stepsQuery
            .Select(x => new { EquipmentId = x.EquipmentId!.Value, x.MaterialUnitId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var materialIds = exposureRows.Select(x => x.MaterialUnitId).Distinct().ToList();
        var defectSet = (await BuildDefectMaterialQuery(query.DefectType, query.FromUtc, query.ToUtc)
                .Where(x => materialIds.Contains(x.MaterialUnitId))
                .Select(x => x.MaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var equipmentIds = exposureRows.Select(x => x.EquipmentId).Distinct().ToList();
        var equipmentLookup = await _dbContext.Equipment
            .AsNoTracking()
            .Where(x => equipmentIds.Contains(x.Id))
            .Select(x => new { x.Id, x.EquipmentCode, x.EquipmentName, x.EquipmentType })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var minMaterials = Math.Max(1, query.MinimumMaterialsPerEquipment <= 0 ? 5 : query.MinimumMaterialsPerEquipment);
        var rows = exposureRows
            .GroupBy(x => x.EquipmentId)
            .Select(group =>
            {
                var materials = group.Select(x => x.MaterialUnitId).Distinct().ToList();
                var defectMaterials = materials.Count(defectSet.Contains);
                var rate = materials.Count == 0 ? 0m : Math.Round((decimal)defectMaterials / materials.Count, 6);
                equipmentLookup.TryGetValue(group.Key, out var eq);

                return new EquipmentDefectRateRow(
                    EquipmentId: group.Key,
                    EquipmentCode: eq?.EquipmentCode ?? group.Key.ToString(),
                    EquipmentName: eq?.EquipmentName ?? "Unknown equipment",
                    EquipmentType: eq?.EquipmentType ?? "Unknown",
                    MaterialCount: materials.Count,
                    DefectMaterialCount: defectMaterials,
                    DefectRate: rate);
            })
            .Where(x => x.MaterialCount >= minMaterials)
            .OrderByDescending(x => x.DefectRate)
            .ThenByDescending(x => x.MaterialCount)
            .ToList();

        return ApplicationResult<EquipmentDefectRateResult>.Success(new EquipmentDefectRateResult(
            query.DefectType,
            query.SiteId,
            query.FromUtc,
            query.ToUtc,
            rows.Count,
            rows));
    }

    public async Task<ApplicationResult<OperationDefectRateResult>> GetOperationDefectRateAsync(
        OperationDefectRateQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.DefectType))
            return ApplicationResult<OperationDefectRateResult>.Failure(ApplicationError.Validation("DefectType is required."));

        var stepsQuery = _dbContext.ProcessStepExecutions.AsNoTracking();

        if (query.FromUtc.HasValue)
            stepsQuery = stepsQuery.Where(x => x.StartedAtUtc >= query.FromUtc.Value);

        if (query.ToUtc.HasValue)
            stepsQuery = stepsQuery.Where(x => x.StartedAtUtc <= query.ToUtc.Value);

        if (query.SiteId.HasValue)
        {
            stepsQuery = stepsQuery.Join(
                _dbContext.MaterialUnits.AsNoTracking().Where(m => m.SiteId == query.SiteId.Value),
                step => step.MaterialUnitId,
                material => material.Id,
                (step, _) => step);
        }

        var stepRows = await stepsQuery
            .Select(x => new { x.MaterialUnitId, x.OperationType, x.OperationCode })
            .ToListAsync(cancellationToken);

        var materialIds = stepRows.Select(x => x.MaterialUnitId).Distinct().ToList();
        var defectSet = (await BuildDefectMaterialQuery(query.DefectType, query.FromUtc, query.ToUtc)
                .Where(x => materialIds.Contains(x.MaterialUnitId))
                .Select(x => x.MaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var minMaterials = Math.Max(1, query.MinimumMaterialsPerOperation <= 0 ? 5 : query.MinimumMaterialsPerOperation);
        var rows = stepRows
            .GroupBy(x => new { x.OperationType, x.OperationCode })
            .Select(group =>
            {
                var materials = group.Select(x => x.MaterialUnitId).Distinct().ToList();
                var defectMaterials = materials.Count(defectSet.Contains);
                var rate = materials.Count == 0 ? 0m : Math.Round((decimal)defectMaterials / materials.Count, 6);
                return new OperationDefectRateRow(group.Key.OperationType, group.Key.OperationCode, materials.Count, defectMaterials, rate);
            })
            .Where(x => x.MaterialCount >= minMaterials)
            .OrderByDescending(x => x.DefectRate)
            .ThenByDescending(x => x.MaterialCount)
            .ToList();

        return ApplicationResult<OperationDefectRateResult>.Success(new OperationDefectRateResult(
            query.DefectType,
            query.SiteId,
            query.FromUtc,
            query.ToUtc,
            rows.Count,
            rows));
    }

    public async Task<ApplicationResult<MaterialCorrelationContextResult>> GetMaterialCorrelationContextAsync(
        Guid materialUnitId,
        string defectType,
        CancellationToken cancellationToken)
    {
        if (materialUnitId == Guid.Empty)
            return ApplicationResult<MaterialCorrelationContextResult>.Failure(ApplicationError.Validation("MaterialUnitId is required."));

        if (string.IsNullOrWhiteSpace(defectType))
            return ApplicationResult<MaterialCorrelationContextResult>.Failure(ApplicationError.Validation("DefectType is required."));

        var material = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.Id == materialUnitId)
            .Select(x => new { x.Id, x.MaterialCode })
            .FirstOrDefaultAsync(cancellationToken);

        if (material is null)
            return ApplicationResult<MaterialCorrelationContextResult>.Failure(ApplicationError.NotFound("Material unit does not exist."));

        // MVP context: returns the latest numeric parameters and equipment exposures.
        // Full statistical matching against persisted correlation results comes later.
        var parameterIndicators = await _dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && x.NumericValue.HasValue)
            .Join(
                _dbContext.ParameterDefinitions.AsNoTracking(),
                obs => obs.ParameterDefinitionId,
                def => def.Id,
                (obs, def) => new MaterialParameterRiskIndicator(
                    def.ParameterCode,
                    def.ParameterName,
                    obs.NumericValue!.Value,
                    obs.UnitOfMeasure,
                    null,
                    null,
                    null,
                    obs.ObservedAtUtc))
            .OrderByDescending(x => x.ObservedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        var equipmentIndicators = await _dbContext.ProcessStepExecutions
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == materialUnitId && x.EquipmentId.HasValue)
            .Join(
                _dbContext.Equipment.AsNoTracking(),
                step => step.EquipmentId!.Value,
                eq => eq.Id,
                (step, eq) => new MaterialEquipmentRiskIndicator(eq.Id, eq.EquipmentCode, eq.EquipmentName, null, null))
            .Distinct()
            .ToListAsync(cancellationToken);

        return ApplicationResult<MaterialCorrelationContextResult>.Success(new MaterialCorrelationContextResult(
            material.Id,
            material.MaterialCode,
            parameterIndicators,
            equipmentIndicators));
    }

    private IQueryable<PlantProcess.Domain.Entities.Quality.QualityEvent> BuildDefectMaterialQuery(
        string defectType,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var normalized = defectType.Trim();
        var query = _dbContext.QualityEvents.AsNoTracking();

        query = query.Where(x =>
            x.EventType == normalized ||
            x.Decision == normalized ||
            x.Severity == normalized ||
            _dbContext.DefectCatalogs.Any(d => d.Id == x.DefectCatalogId &&
                (d.DefectCode == normalized || d.DefectName == normalized || d.DefectCategory == normalized)));

        if (fromUtc.HasValue)
            query = query.Where(x => x.EventAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(x => x.EventAtUtc <= toUtc.Value);

        return query;
    }

    private static decimal CalculateLift(decimal rate, decimal overallRate)
    {
        if (overallRate <= 0m)
            return 0m;

        return Math.Round(rate / overallRate, 6);
    }
}





