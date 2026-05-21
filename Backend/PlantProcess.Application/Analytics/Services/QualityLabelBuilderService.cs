using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Common.Persistence;

namespace PlantProcess.Application.Analytics.Services;

public sealed class QualityLabelBuilderService : IQualityLabelBuilderService
{
    private readonly IPlantProcessDbContext _dbContext;

    public QualityLabelBuilderService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<QualityTrainingLabelPreviewDto> BuildPreviewAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var safeLimit = Math.Clamp(limit, 1, 500);

        var materialIdsFromQuality = await _dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => x.MaterialUnitId)
            .Distinct()
            .Take(safeLimit)
            .ToListAsync(cancellationToken);

        if (materialIdsFromQuality.Count < safeLimit)
        {
            var additionalMaterialIds = await _dbContext.MaterialUnits
                .AsNoTracking()
                .Where(x => !x.IsDeleted && !materialIdsFromQuality.Contains(x.Id))
                .OrderByDescending(x => x.ProductionStartUtc ?? x.CreatedAtUtc)
                .Select(x => x.Id)
                .Take(safeLimit - materialIdsFromQuality.Count)
                .ToListAsync(cancellationToken);

            materialIdsFromQuality.AddRange(additionalMaterialIds);
        }

        var labels = new List<QualityTrainingLabelDto>();

        foreach (var materialId in materialIdsFromQuality.Distinct())
        {
            var material = await _dbContext.MaterialUnits
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == materialId && !x.IsDeleted, cancellationToken);

            if (material is null)
                continue;

            var qualityEvents = await _dbContext.QualityEvents
                .AsNoTracking()
                .Where(x => x.MaterialUnitId == materialId && !x.IsDeleted)
                .OrderBy(x => x.EventAtUtc)
                .ToListAsync(cancellationToken);

            var defectCatalogIds = qualityEvents
                .Where(x => x.DefectCatalogId.HasValue)
                .Select(x => x.DefectCatalogId!.Value)
                .Distinct()
                .ToList();

            Dictionary<Guid, DefectCatalogProjection> defectCatalogs;

            if (defectCatalogIds.Count == 0)
            {
                defectCatalogs = new Dictionary<Guid, DefectCatalogProjection>();
            }
            else
            {
                defectCatalogs = await _dbContext.DefectCatalogs
                    .AsNoTracking()
                    .Where(x => defectCatalogIds.Contains(x.Id) && !x.IsDeleted)
                    .Select(x => new DefectCatalogProjection(
                        x.Id,
                        x.DefectCode,
                        x.DefectName,
                        x.DefectCategory))
                    .ToDictionaryAsync(
                        x => x.Id,
                        x => x,
                        cancellationToken);
            }

            var directParentIds = await _dbContext.GenealogyEdges
                .AsNoTracking()
                .Where(x => x.ChildMaterialUnitId == materialId && !x.IsDeleted)
                .Select(x => x.ParentMaterialUnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var materialAndParents = directParentIds
                .Append(materialId)
                .Distinct()
                .ToList();

            var upstreamObservationCount = await _dbContext.ParameterObservations
                .AsNoTracking()
                .CountAsync(
                    x => materialAndParents.Contains(x.MaterialUnitId) && !x.IsDeleted,
                    cancellationToken);

            var lastObservationAtUtc = await _dbContext.ParameterObservations
                .AsNoTracking()
                .Where(x => materialAndParents.Contains(x.MaterialUnitId) && !x.IsDeleted)
                .Select(x => (DateTime?)x.ObservedAtUtc)
                .MaxAsync(cancellationToken);

            var genealogyEdgeCount = await _dbContext.GenealogyEdges
                .AsNoTracking()
                .CountAsync(
                    x => !x.IsDeleted
                         && (x.ChildMaterialUnitId == materialId
                             || x.ParentMaterialUnitId == materialId),
                    cancellationToken);

            var hasDefect = qualityEvents.Any(x =>
                x.EventType.Equals("Defect", StringComparison.OrdinalIgnoreCase)
                || x.DefectCatalogId.HasValue);

            var isRejected = qualityEvents.Any(x =>
                ContainsAny(x.EventType, "reject")
                || ContainsAny(x.Decision, "reject", "scrap"));

            var isDowngraded = qualityEvents.Any(x =>
                ContainsAny(x.EventType, "downgrade")
                || ContainsAny(x.Decision, "downgrade"));

            var isReworked = qualityEvents.Any(x =>
                ContainsAny(x.EventType, "rework")
                || ContainsAny(x.Decision, "rework"));

            var firstDefectEvent = qualityEvents
                .FirstOrDefault(x => x.DefectCatalogId.HasValue);

            string? primaryDefectCode = null;
            string? primaryDefectName = null;
            string? primaryDefectCategory = null;

            if (firstDefectEvent?.DefectCatalogId is Guid defectCatalogId
                && defectCatalogs.TryGetValue(defectCatalogId, out var defect))
            {
                primaryDefectCode = defect.Code;
                primaryDefectName = defect.Name;
                primaryDefectCategory = defect.Category;
            }

            var labelCode = BuildLabelCode(
                hasDefect,
                isRejected,
                isDowngraded,
                isReworked,
                primaryDefectCategory);

            labels.Add(new QualityTrainingLabelDto(
                MaterialUnitId: material.Id,
                MaterialCode: material.MaterialCode,
                MaterialUnitType: material.MaterialUnitType,
                LabelCode: labelCode,
                HasDefect: hasDefect,
                IsRejected: isRejected,
                IsDowngraded: isDowngraded,
                IsReworked: isReworked,
                PrimaryDefectCode: primaryDefectCode,
                PrimaryDefectName: primaryDefectName,
                PrimaryDefectCategory: primaryDefectCategory,
                QualityEventCount: qualityEvents.Count,
                UpstreamObservationCount: upstreamObservationCount,
                GenealogyEdgeCount: genealogyEdgeCount,
                FirstQualityEventAtUtc: qualityEvents.FirstOrDefault()?.EventAtUtc,
                LastObservationAtUtc: lastObservationAtUtc));
        }

        return new QualityTrainingLabelPreviewDto(
            GeneratedAtUtc: DateTime.UtcNow,
            RequestedLimit: safeLimit,
            ReturnedCount: labels.Count,
            Labels: labels);
    }

    public async Task<int> CountLabeledMaterialsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => x.MaterialUnitId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    private static string BuildLabelCode(
        bool hasDefect,
        bool isRejected,
        bool isDowngraded,
        bool isReworked,
        string? primaryDefectCategory)
    {
        if (isRejected)
            return "REJECTED";

        if (isReworked)
            return "REWORKED";

        if (isDowngraded)
            return "DOWNGRADED";

        if (hasDefect && !string.IsNullOrWhiteSpace(primaryDefectCategory))
            return $"DEFECT_{Normalize(primaryDefectCategory)}";

        if (hasDefect)
            return "DEFECT_OTHER";

        return "ACCEPTED_OR_NO_QUALITY_EVENT";
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .ToUpperInvariant()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace("/", "_");
    }

    private static bool ContainsAny(string? value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return tokens.Any(token =>
            value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record DefectCatalogProjection(
        Guid Id,
        string Code,
        string Name,
        string? Category);
}