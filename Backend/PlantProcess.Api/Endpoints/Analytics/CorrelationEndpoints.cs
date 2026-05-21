using Microsoft.EntityFrameworkCore;
using PlantProcess.Api.Extensions;
using PlantProcess.Application.Analytics.Contracts;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Licensing.Contracts;
using PlantProcess.Application.Licensing.Interfaces;
using PlantProcess.Domain.Entities.Analytics;
using PlantProcess.Infrastructure.Persistence;
using System.Text.Json;

namespace PlantProcess.Api.Endpoints.Analytics;

public static class CorrelationEndpoints
{
    public static IEndpointRouteBuilder MapCorrelationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/analytics/correlations")
            .WithTags("Correlation Analytics")
            .RequireAuthorization("PlantProcessViewer");

        // Existing MVP endpoints
        group.MapGet("/parameter-defect", GetParameterDefectCorrelationAsync);
        group.MapGet("/equipment-defect-rate", GetEquipmentDefectRateAsync);
        group.MapGet("/operation-defect-rate", GetOperationDefectRateAsync);
        group.MapGet("/materials/{materialUnitId:guid}/context", GetMaterialCorrelationContextAsync);

        // Persisted Correlation Metadata Patch
        group.MapPost("/runs", PersistCorrelationRunAsync);
        group.MapGet("/runs", GetCorrelationRunsAsync);
        group.MapGet("/runs/{id:guid}", GetCorrelationRunByIdAsync);

        // Phase 10: genealogy-aware endpoint for process-to-quality intelligence.
        group.MapGet("/parameter-defect/genealogy-aware", GetGenealogyAwareParameterDefectCorrelationAsync);

        return app;
    }

    private static async Task<IResult> GetParameterDefectCorrelationAsync(
        string parameterCode,
        string defectType,
        Guid? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int? bins,
        int? minimumObservationsPerBin,
        bool? persistResult,
        ILicenseService licenseService,
        ICorrelationService service,
        CancellationToken cancellationToken)
    {
        var gate = licenseService.EnsureFeatureEnabled(LicenseFeature.CorrelationManualRun);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());

        var result = await service.GetParameterDefectCorrelationAsync(
            new ParameterDefectCorrelationQuery(
                parameterCode,
                defectType,
                siteId,
                fromUtc,
                toUtc,
                bins ?? 8,
                minimumObservationsPerBin ?? 5,
                persistResult ?? false),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetEquipmentDefectRateAsync(
        string defectType,
        Guid? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int? minimumMaterialsPerEquipment,
        ILicenseService licenseService,
        ICorrelationService service,
        CancellationToken cancellationToken)
    {
        var gate = licenseService.EnsureFeatureEnabled(LicenseFeature.CorrelationManualRun);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());

        var result = await service.GetEquipmentDefectRateAsync(
            new EquipmentDefectRateQuery(
                defectType,
                siteId,
                fromUtc,
                toUtc,
                minimumMaterialsPerEquipment ?? 5),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetOperationDefectRateAsync(
        string defectType,
        Guid? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int? minimumMaterialsPerOperation,
        ILicenseService licenseService,
        ICorrelationService service,
        CancellationToken cancellationToken)
    {
        var gate = licenseService.EnsureFeatureEnabled(LicenseFeature.CorrelationManualRun);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());

        var result = await service.GetOperationDefectRateAsync(
            new OperationDefectRateQuery(
                defectType,
                siteId,
                fromUtc,
                toUtc,
                minimumMaterialsPerOperation ?? 5),
            cancellationToken);

        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> GetMaterialCorrelationContextAsync(
        Guid materialUnitId,
        string defectType,
        ILicenseService licenseService,
        ICorrelationService service,
        CancellationToken cancellationToken)
    {
        var gate = licenseService.EnsureFeatureEnabled(LicenseFeature.CorrelationManualRun);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());

        var result = await service.GetMaterialCorrelationContextAsync(materialUnitId, defectType, cancellationToken);
        return result.ToHttpResult(value => Results.Ok(value));
    }

    private static async Task<IResult> PersistCorrelationRunAsync(
        PersistCorrelationRunRequest request,
        ILicenseService licenseService,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var gate = licenseService.EnsureFeatureEnabled(LicenseFeature.CorrelationManualRun);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());

        if (string.IsNullOrWhiteSpace(request.CorrelationType))
            return Results.BadRequest(new { message = "CorrelationType is required." });

        if (string.IsNullOrWhiteSpace(request.SubjectCode))
            return Results.BadRequest(new { message = "SubjectCode is required." });

        if (string.IsNullOrWhiteSpace(request.OutcomeCode))
            return Results.BadRequest(new { message = "OutcomeCode is required." });

        var resultJson = JsonSerializer.Serialize(new
        {
            request.FiltersJson,
            request.ResultJson,
            request.Notes,
            persistedBy = "PlantProcessIQ.Api",
            persistedAtUtc = DateTime.UtcNow
        });

        var correlation = new CorrelationResult(
            correlationType: request.CorrelationType,
            subjectCode: request.SubjectCode,
            outcomeCode: request.OutcomeCode,
            score: request.Score,
            resultJson: resultJson,
            isSynthetic: request.IsSynthetic,
            sourceSystem: "PlantProcessIQ.ReactDashboard",
            sourceRecordId: request.SourceRecordId);

        dbContext.CorrelationResults.Add(correlation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/analytics/correlations/runs/{correlation.Id}", new
        {
            correlation.Id,
            correlation.CorrelationType,
            correlation.SubjectCode,
            correlation.OutcomeCode,
            correlation.Score,
            correlation.CalculatedAtUtc
        });
    }

    private static async Task<IResult> GetCorrelationRunsAsync(
        string? correlationType,
        string? subjectCode,
        string? outcomeCode,
        int? page,
        int? pageSize,
        ILicenseService licenseService,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var gate = licenseService.EnsureFeatureEnabled(LicenseFeature.CorrelationManualRun);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());

        var safePage = Math.Max(page ?? 1, 1);
        var safePageSize = Math.Clamp(pageSize ?? 25, 1, 200);

        var query = dbContext.CorrelationResults
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(correlationType))
            query = query.Where(x => x.CorrelationType == correlationType);

        if (!string.IsNullOrWhiteSpace(subjectCode))
            query = query.Where(x => x.SubjectCode == subjectCode);

        if (!string.IsNullOrWhiteSpace(outcomeCode))
            query = query.Where(x => x.OutcomeCode == outcomeCode);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CalculatedAtUtc)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new
            {
                x.Id,
                x.CorrelationType,
                x.SubjectCode,
                x.OutcomeCode,
                x.Score,
                x.CalculatedAtUtc,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            items,
            page = safePage,
            pageSize = safePageSize,
            totalCount,
            totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((decimal)totalCount / safePageSize)
        });
    }

    private static async Task<IResult> GetCorrelationRunByIdAsync(
        Guid id,
        ILicenseService licenseService,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var gate = licenseService.EnsureFeatureEnabled(LicenseFeature.CorrelationManualRun);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());

        var item = await dbContext.CorrelationResults
            .AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.CorrelationType,
                x.SubjectCode,
                x.OutcomeCode,
                x.Score,
                x.ResultJson,
                x.CalculatedAtUtc,
                x.SourceSystem,
                x.SourceRecordId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    public sealed record PersistCorrelationRunRequest(
        string CorrelationType,
        string SubjectCode,
        string OutcomeCode,
        decimal? Score,
        string FiltersJson,
        string ResultJson,
        string? Notes,
        bool IsSynthetic,
        string? SourceRecordId);

    /// <summary>
    /// Phase 10 endpoint.
    /// This is intentionally aggregate-only and dashboard-ready.
    /// It does not return raw source rows.
    /// </summary>
    private static async Task<IResult> GetGenealogyAwareParameterDefectCorrelationAsync(
        string parameterCode,
        string defectType,
        Guid? siteId,
        DateTime? fromUtc,
        DateTime? toUtc,
        int? bins,
        int? minimumObservationsPerBin,
        string? linkMode,
        int? genealogyDepth,
        ILicenseService licenseService,
        PlantProcessDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var gate = licenseService.EnsureFeatureEnabled(LicenseFeature.CorrelationManualRun);
        if (gate.IsFailure)
            return gate.ToHttpResult(() => Results.NoContent());

        var safeBins = Math.Clamp(bins ?? 8, 2, 25);
        var safeMinimumObservations = Math.Clamp(minimumObservationsPerBin ?? 5, 1, 10_000);
        var safeDepth = Math.Clamp(genealogyDepth ?? 3, 0, 20);
        var normalizedLinkMode = NormalizeLinkMode(linkMode);

        var parameter = await dbContext.ParameterDefinitions
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                (x.ParameterCode == parameterCode || x.ParameterName == parameterCode))
            .Select(x => new
            {
                x.Id,
                x.ParameterCode,
                x.ParameterName,
                x.UnitOfMeasure
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (parameter is null)
        {
            return Results.NotFound(new
            {
                message = "Parameter definition not found.",
                parameterCode
            });
        }

        var observationsQuery =
            from observation in dbContext.ParameterObservations.AsNoTracking()
            join material in dbContext.MaterialUnits.AsNoTracking()
                on observation.MaterialUnitId equals material.Id
            where
                !observation.IsDeleted &&
                !material.IsDeleted &&
                observation.ParameterDefinitionId == parameter.Id &&
                observation.NumericValue != null
            select new ObservationProjection(
                observation.MaterialUnitId,
                material.MaterialCode,
                material.SiteId,
                observation.ObservedAtUtc,
                observation.NumericValue!.Value,
                observation.EquipmentId,
                observation.SourceSystem);

        if (siteId.HasValue)
            observationsQuery = observationsQuery.Where(x => x.SiteId == siteId.Value);

        if (fromUtc.HasValue)
            observationsQuery = observationsQuery.Where(x => x.ObservedAtUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            observationsQuery = observationsQuery.Where(x => x.ObservedAtUtc <= toUtc.Value);

        var observations = await observationsQuery
            .OrderBy(x => x.ObservedAtUtc)
            .Take(250_000)
            .ToListAsync(cancellationToken);

        if (observations.Count == 0)
        {
            return Results.Ok(new GenealogyAwareParameterDefectCorrelationResponse(
                GeneratedAtUtc: DateTime.UtcNow,
                ParameterCode: parameter.ParameterCode,
                ParameterName: parameter.ParameterName,
                UnitOfMeasure: parameter.UnitOfMeasure,
                DefectType: defectType,
                LinkMode: normalizedLinkMode,
                GenealogyDepth: safeDepth,
                BaselineDefectRatePercent: 0,
                TotalObservationCount: 0,
                TotalMaterialCount: 0,
                TotalDefectLinkedObservationCount: 0,
                Bins: Array.Empty<GenealogyAwareCorrelationBinDto>(),
                Message: "No numeric observations found for the selected filter."));
        }

        var observationMaterialIds = observations
            .Select(x => x.MaterialUnitId)
            .Distinct()
            .ToArray();

        var allEdges = await dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new GenealogyEdgeProjection(
                x.ParentMaterialUnitId,
                x.ChildMaterialUnitId))
            .ToListAsync(cancellationToken);

        var linkedMaterialLookup = BuildLinkedMaterialLookup(
            observationMaterialIds,
            allEdges,
            normalizedLinkMode,
            safeDepth);

        var candidateQualityMaterialIds = linkedMaterialLookup
            .SelectMany(x => x.Value)
            .Distinct()
            .ToArray();

        var defectMaterialIds = await (
            from qualityEvent in dbContext.QualityEvents.AsNoTracking()
            join defect in dbContext.DefectCatalogs.AsNoTracking()
                on qualityEvent.DefectCatalogId equals defect.Id into defectJoin
            from defect in defectJoin.DefaultIfEmpty()
            where
                !qualityEvent.IsDeleted &&
                candidateQualityMaterialIds.Contains(qualityEvent.MaterialUnitId) &&
                (
                    qualityEvent.EventType == defectType ||
                    qualityEvent.EventType == "Defect" && defect != null && defect.DefectCode == defectType ||
                    qualityEvent.EventType == "Defect" && defect != null && defect.DefectName == defectType ||
                    defect != null && defect.DefectCode == defectType ||
                    defect != null && defect.DefectName == defectType
                )
            select qualityEvent.MaterialUnitId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var defectMaterialSet = defectMaterialIds.ToHashSet();

        var evaluatedObservations = observations
            .Select(x =>
            {
                var linkedMaterials = linkedMaterialLookup.TryGetValue(x.MaterialUnitId, out var ids)
                    ? ids
                    : new HashSet<Guid> { x.MaterialUnitId };

                var hasLinkedDefect = linkedMaterials.Any(defectMaterialSet.Contains);

                return new EvaluatedObservation(
                    x.MaterialUnitId,
                    x.MaterialCode,
                    x.ObservedAtUtc,
                    x.NumericValue,
                    hasLinkedDefect,
                    linkedMaterials.Count);
            })
            .ToList();

        var totalObservationCount = evaluatedObservations.Count;
        var totalMaterialCount = evaluatedObservations.Select(x => x.MaterialUnitId).Distinct().Count();
        var totalDefectLinkedObservationCount = evaluatedObservations.Count(x => x.HasLinkedDefect);
        var baselineRate = totalObservationCount == 0
            ? 0m
            : Math.Round(totalDefectLinkedObservationCount * 100m / totalObservationCount, 4);

        var minValue = evaluatedObservations.Min(x => x.NumericValue);
        var maxValue = evaluatedObservations.Max(x => x.NumericValue);

        if (minValue == maxValue)
        {
            var allSameBinCount = evaluatedObservations.Count;
            var defectCount = evaluatedObservations.Count(x => x.HasLinkedDefect);
            var rate = allSameBinCount == 0 ? 0 : Math.Round(defectCount * 100m / allSameBinCount, 4);

            return Results.Ok(new GenealogyAwareParameterDefectCorrelationResponse(
                GeneratedAtUtc: DateTime.UtcNow,
                ParameterCode: parameter.ParameterCode,
                ParameterName: parameter.ParameterName,
                UnitOfMeasure: parameter.UnitOfMeasure,
                DefectType: defectType,
                LinkMode: normalizedLinkMode,
                GenealogyDepth: safeDepth,
                BaselineDefectRatePercent: baselineRate,
                TotalObservationCount: totalObservationCount,
                TotalMaterialCount: totalMaterialCount,
                TotalDefectLinkedObservationCount: totalDefectLinkedObservationCount,
                Bins: new[]
                {
                    new GenealogyAwareCorrelationBinDto(
                        BinNo: 1,
                        BinLabel: $"{minValue:0.###}",
                        MinValue: minValue,
                        MaxValue: maxValue,
                        ObservationCount: allSameBinCount,
                        MaterialCount: evaluatedObservations.Select(x => x.MaterialUnitId).Distinct().Count(),
                        DefectLinkedObservationCount: defectCount,
                        DefectRatePercent: rate,
                        LiftVsBaseline: CalculateLift(rate, baselineRate),
                        Confidence: BuildConfidence(allSameBinCount, safeMinimumObservations))
                },
                Message: "Only one numeric value exists for the selected parameter/filter."));
        }

        var width = (maxValue - minValue) / safeBins;
        var binsResult = new List<GenealogyAwareCorrelationBinDto>();

        for (var i = 0; i < safeBins; i++)
        {
            var binMin = minValue + width * i;
            var binMax = i == safeBins - 1 ? maxValue : minValue + width * (i + 1);

            var binRows = evaluatedObservations
                .Where(x =>
                    i == safeBins - 1
                        ? x.NumericValue >= binMin && x.NumericValue <= binMax
                        : x.NumericValue >= binMin && x.NumericValue < binMax)
                .ToList();

            if (binRows.Count < safeMinimumObservations)
                continue;

            var observationCount = binRows.Count;
            var materialCount = binRows.Select(x => x.MaterialUnitId).Distinct().Count();
            var defectLinkedObservationCount = binRows.Count(x => x.HasLinkedDefect);
            var defectRate = observationCount == 0
                ? 0
                : Math.Round(defectLinkedObservationCount * 100m / observationCount, 4);

            binsResult.Add(new GenealogyAwareCorrelationBinDto(
                BinNo: i + 1,
                BinLabel: $"{binMin:0.###} - {binMax:0.###}",
                MinValue: binMin,
                MaxValue: binMax,
                ObservationCount: observationCount,
                MaterialCount: materialCount,
                DefectLinkedObservationCount: defectLinkedObservationCount,
                DefectRatePercent: defectRate,
                LiftVsBaseline: CalculateLift(defectRate, baselineRate),
                Confidence: BuildConfidence(observationCount, safeMinimumObservations)));
        }

        return Results.Ok(new GenealogyAwareParameterDefectCorrelationResponse(
            GeneratedAtUtc: DateTime.UtcNow,
            ParameterCode: parameter.ParameterCode,
            ParameterName: parameter.ParameterName,
            UnitOfMeasure: parameter.UnitOfMeasure,
            DefectType: defectType,
            LinkMode: normalizedLinkMode,
            GenealogyDepth: safeDepth,
            BaselineDefectRatePercent: baselineRate,
            TotalObservationCount: totalObservationCount,
            TotalMaterialCount: totalMaterialCount,
            TotalDefectLinkedObservationCount: totalDefectLinkedObservationCount,
            Bins: binsResult.OrderByDescending(x => x.LiftVsBaseline).ToList(),
            Message: "Genealogy-aware parameter-defect correlation calculated successfully."));
    }

    private static string NormalizeLinkMode(string? linkMode)
    {
        if (string.IsNullOrWhiteSpace(linkMode))
            return "DownstreamChildren";

        return linkMode.Trim() switch
        {
            "SameMaterial" => "SameMaterial",
            "UpstreamParents" => "UpstreamParents",
            "DownstreamChildren" => "DownstreamChildren",
            "FullGenealogy" => "FullGenealogy",
            _ => "DownstreamChildren"
        };
    }

    private static Dictionary<Guid, HashSet<Guid>> BuildLinkedMaterialLookup(
        IReadOnlyCollection<Guid> seedMaterialIds,
        IReadOnlyCollection<GenealogyEdgeProjection> edges,
        string linkMode,
        int maxDepth)
    {
        var lookup = new Dictionary<Guid, HashSet<Guid>>();

        var childrenByParent = edges
            .GroupBy(x => x.ParentMaterialUnitId)
            .ToDictionary(x => x.Key, x => x.Select(e => e.ChildMaterialUnitId).Distinct().ToList());

        var parentsByChild = edges
            .GroupBy(x => x.ChildMaterialUnitId)
            .ToDictionary(x => x.Key, x => x.Select(e => e.ParentMaterialUnitId).Distinct().ToList());

        foreach (var seedId in seedMaterialIds)
        {
            var linked = new HashSet<Guid> { seedId };

            if (linkMode is "DownstreamChildren" or "FullGenealogy")
            {
                foreach (var child in Traverse(seedId, childrenByParent, maxDepth))
                    linked.Add(child);
            }

            if (linkMode is "UpstreamParents" or "FullGenealogy")
            {
                foreach (var parent in Traverse(seedId, parentsByChild, maxDepth))
                    linked.Add(parent);
            }

            lookup[seedId] = linked;
        }

        return lookup;
    }

    private static IEnumerable<Guid> Traverse(
        Guid startId,
        IReadOnlyDictionary<Guid, List<Guid>> adjacency,
        int maxDepth)
    {
        if (maxDepth <= 0)
            yield break;

        var visited = new HashSet<Guid> { startId };
        var queue = new Queue<(Guid Id, int Depth)>();
        queue.Enqueue((startId, 0));

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current.Depth >= maxDepth)
                continue;

            if (!adjacency.TryGetValue(current.Id, out var nextIds))
                continue;

            foreach (var nextId in nextIds)
            {
                if (!visited.Add(nextId))
                    continue;

                yield return nextId;
                queue.Enqueue((nextId, current.Depth + 1));
            }
        }
    }

    private static decimal? CalculateLift(decimal binRatePercent, decimal baselineRatePercent)
    {
        if (baselineRatePercent <= 0)
            return null;

        return Math.Round(binRatePercent / baselineRatePercent, 4);
    }

    private static string BuildConfidence(int observationCount, int minimumObservationCount)
    {
        if (observationCount < minimumObservationCount)
            return "Low";

        if (observationCount >= minimumObservationCount * 5)
            return "High";

        if (observationCount >= minimumObservationCount * 2)
            return "Medium";

        return "Low";
    }

    private sealed record ObservationProjection(
        Guid MaterialUnitId,
        string MaterialCode,
        Guid SiteId,
        DateTime ObservedAtUtc,
        decimal NumericValue,
        Guid? EquipmentId,
        string? SourceSystem);

    private sealed record EvaluatedObservation(
        Guid MaterialUnitId,
        string MaterialCode,
        DateTime ObservedAtUtc,
        decimal NumericValue,
        bool HasLinkedDefect,
        int LinkedMaterialCount);

    private sealed record GenealogyEdgeProjection(
        Guid ParentMaterialUnitId,
        Guid ChildMaterialUnitId);

    private sealed record GenealogyAwareParameterDefectCorrelationResponse(
        DateTime GeneratedAtUtc,
        string ParameterCode,
        string ParameterName,
        string? UnitOfMeasure,
        string DefectType,
        string LinkMode,
        int GenealogyDepth,
        decimal BaselineDefectRatePercent,
        int TotalObservationCount,
        int TotalMaterialCount,
        int TotalDefectLinkedObservationCount,
        IReadOnlyList<GenealogyAwareCorrelationBinDto> Bins,
        string Message);

    private sealed record GenealogyAwareCorrelationBinDto(
        int BinNo,
        string BinLabel,
        decimal MinValue,
        decimal MaxValue,
        int ObservationCount,
        int MaterialCount,
        int DefectLinkedObservationCount,
        decimal DefectRatePercent,
        decimal? LiftVsBaseline,
        string Confidence);
}