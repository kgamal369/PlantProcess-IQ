using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Integration.Contracts.Mapping;
using PlantProcess.Application.Relationships;
using PlantProcess.Application.Security.Tenancy;
using PlantProcess.Domain.Common;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Entities.Materials;

namespace PlantProcess.Application.Integration.Services.Mapping;

/// <summary>
/// Advanced projection validation classes PV09..PV14. PV15 is classified by
/// the genealogy reference resolver because its meaning is specifically
/// "reference may arrive from another batch".
///
/// Relationship path decisions are delegated to the existing RelationshipResolver.
/// This class does not implement a second path algorithm.
/// </summary>
public sealed class ProjectionRowValidationService
{
    private const decimal WeightTolerance = 0.015m;

    private readonly IPlantProcessDbContext _dbContext;
    private readonly IRelationshipStore? _relationshipStore;

    public ProjectionRowValidationService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public ProjectionRowValidationService(
        IPlantProcessDbContext dbContext,
        IRelationshipStore relationshipStore)
    {
        _dbContext = dbContext;
        _relationshipStore = relationshipStore;
    }

    public async Task<RowValidationRefusal?> ValidateParameterObservationRangeAsync(
        Guid materialId,
        Guid parameterDefinitionId,
        DateTime observedAtUtc,
        decimal? numericValue,
        CancellationToken cancellationToken)
    {
        if (!numericValue.HasValue)
            return null;

        var material = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.Id == materialId)
            .Select(x => new { x.ProductFamily, x.GradeOrRecipe })
            .SingleAsync(cancellationToken);

        decimal? min = null;
        decimal? max = null;
        string authority = "parameter definition";

        if (!string.IsNullOrWhiteSpace(material.GradeOrRecipe))
        {
            var specification = await _dbContext.ProductSpecifications
                .AsNoTracking()
                .Where(x =>
                    x.ParameterDefinitionId == parameterDefinitionId &&
                    x.GradeOrRecipe == material.GradeOrRecipe &&
                    (x.ProductFamily == null || x.ProductFamily == material.ProductFamily) &&
                    x.EffectiveFromUtc <= observedAtUtc &&
                    (x.EffectiveToUtc == null || x.EffectiveToUtc >= observedAtUtc))
                .OrderByDescending(x => x.EffectiveFromUtc)
                .Select(x => new { x.SpecificationCode, x.MinValue, x.MaxValue })
                .FirstOrDefaultAsync(cancellationToken);

            if (specification is not null &&
                (specification.MinValue.HasValue || specification.MaxValue.HasValue))
            {
                min = specification.MinValue;
                max = specification.MaxValue;
                authority = $"product specification '{specification.SpecificationCode}'";
            }
        }

        if (!min.HasValue && !max.HasValue)
        {
            var definition = await _dbContext.ParameterDefinitions
                .AsNoTracking()
                .Where(x => x.Id == parameterDefinitionId)
                .Select(x => new { x.ParameterCode, x.ExpectedMinValue, x.ExpectedMaxValue })
                .SingleAsync(cancellationToken);

            min = definition.ExpectedMinValue;
            max = definition.ExpectedMaxValue;
            authority = $"parameter definition '{definition.ParameterCode}'";
        }

        if (!min.HasValue && !max.HasValue)
            return null;

        if ((!min.HasValue || numericValue.Value >= min.Value) &&
            (!max.HasValue || numericValue.Value <= max.Value))
            return null;

        return Refuse(
            ProjectionValidationCode.PV09,
            $"Numeric value {numericValue.Value.ToString(CultureInfo.InvariantCulture)} is outside " +
            $"the governed range [{Format(min, "-inf")}, {Format(max, "+inf")}] from {authority}.",
            numericValue.Value.ToString(CultureInfo.InvariantCulture));
    }

    public async Task<RowValidationRefusal?> ValidateGenealogyAsync(
        MappingDefinition mapping,
        IReadOnlyDictionary<string, string> fieldMap,
        StagingRecord stagingRecord,
        Guid tenantId,
        Guid parentId,
        Guid childId,
        string relationshipType,
        string? relationshipCode,
        decimal contributionWeight,
        bool isTransition,
        CancellationToken cancellationToken)
    {
        if (parentId == childId)
        {
            return Refuse(
                ProjectionValidationCode.PV12,
                "A genealogy edge cannot point from a material to itself.",
                parentId.ToString());
        }

        if (await WouldCreateCycleAsync(parentId, childId, cancellationToken))
        {
            return Refuse(
                ProjectionValidationCode.PV12,
                "Adding this edge would create a genealogy cycle.",
                $"{parentId}->{childId}");
        }

        var weightRefusal = await ValidateWeightsAsync(
            fieldMap,
            stagingRecord,
            childId,
            relationshipType,
            contributionWeight,
            isTransition,
            cancellationToken);

        if (weightRefusal is not null)
            return weightRefusal;

        if (string.IsNullOrWhiteSpace(relationshipCode))
            return null;

        if (_relationshipStore is null)
        {
            throw new InvalidOperationException(
                "RelationshipCode was supplied but the governed relationship store is unavailable.");
        }

        var published = await _relationshipStore.ReadPublishedAsync(
            tenantId,
            null,
            cancellationToken);

        var relationship = published.SingleOrDefault(x =>
            string.Equals(
                x.RelationshipCode,
                relationshipCode,
                StringComparison.OrdinalIgnoreCase));

        if (relationship is null)
        {
            return Refuse(
                ProjectionValidationCode.PV06,
                $"RelationshipCode '{relationshipCode}' is not published for this tenant.",
                relationshipCode);
        }

        var cardinalityRefusal = await ValidateCardinalityAsync(
            relationship,
            fieldMap,
            stagingRecord,
            parentId,
            childId,
            relationshipType,
            relationshipCode,
            cancellationToken);

        if (cardinalityRefusal is not null)
            return cardinalityRefusal;

        var grains = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(x => x.Id == parentId || x.Id == childId)
            .Select(x => new { x.Id, x.MaterialUnitType })
            .ToListAsync(cancellationToken);

        var parentGrain = grains.Single(x => x.Id == parentId).MaterialUnitType;
        var childGrain = grains.Single(x => x.Id == childId).MaterialUnitType;

        if (!GrainMatches(relationship.GrainLeft, parentGrain) ||
            !GrainMatches(relationship.GrainRight, childGrain))
        {
            return Refuse(
                ProjectionValidationCode.PV11,
                $"Edge grain '{parentGrain}->{childGrain}' does not match relationship " +
                $"'{relationship.RelationshipCode}' grain " +
                $"'{relationship.GrainLeft}->{relationship.GrainRight}'.",
                $"{parentGrain}->{childGrain}");
        }

        var tenantAccessor = new FixedTenantAccessor(tenantId);
        var relationshipService = new RelationshipService(_relationshipStore, tenantAccessor);
        var resolver = new RelationshipResolver(relationshipService);

        var resolution = await resolver.ResolveAsync(
            relationship.LeftEntity,
            relationship.RightEntity,
            RelationshipConsumerPurposes.Projection,
            cancellationToken);

        if (resolution.IsFailure)
        {
            throw new InvalidOperationException(
                "Relationship resolution failed: " + resolution.Error!.Message);
        }

        if (!resolution.Value!.Resolved &&
            string.Equals(
                resolution.Value.RefusalCode,
                RelationshipRefusalCodes.AmbiguousPath,
                StringComparison.Ordinal))
        {
            return Refuse(
                ProjectionValidationCode.PV14,
                resolution.Value.RefusalMessage ??
                "Relationship resolution is ambiguous and no preferred path is available.",
                relationshipCode);
        }

        return null;
    }

    private async Task<RowValidationRefusal?> ValidateCardinalityAsync(
        RelationshipDto relationship,
        IReadOnlyDictionary<string, string> fieldMap,
        StagingRecord stagingRecord,
        Guid parentId,
        Guid childId,
        string relationshipType,
        string relationshipCode,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
            relationship.Cardinality,
            RelationshipCardinalities.ManyToMany,
            StringComparison.Ordinal))
            return null;

        var rawRows = await _dbContext.StagingRecords
            .AsNoTracking()
            .Where(x =>
                x.ImportBatchId == stagingRecord.ImportBatchId &&
                x.SourceObjectName == stagingRecord.SourceObjectName)
            .Select(x => x.RawJson)
            .ToListAsync(cancellationToken);

        var pairs = new List<(string Parent, string Child)>();

        foreach (var raw in rawRows)
        {
            var source = TryParseFlat(raw);
            if (source is null)
                continue;

            var code = ReadMapped(fieldMap, source, "RelationshipCode");
            var type = ReadMapped(fieldMap, source, "RelationshipType");

            if (!string.Equals(code, relationshipCode, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(type, relationshipType, StringComparison.OrdinalIgnoreCase))
                continue;

            var parent = ReadIdentity(
                fieldMap,
                source,
                "ParentMaterialUnitId",
                "ParentMaterialCode",
                "ParentAliasCode");

            var child = ReadIdentity(
                fieldMap,
                source,
                "ChildMaterialUnitId",
                "ChildMaterialCode",
                "ChildAliasCode");

            if (parent is not null && child is not null)
                pairs.Add((parent, child));
        }

        var batchViolation = relationship.Cardinality switch
        {
            var value when value == RelationshipCardinalities.OneToOne =>
                pairs.GroupBy(x => x.Parent, StringComparer.OrdinalIgnoreCase)
                    .Any(group =>
                        group.Select(x => x.Child)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Count() > 1)
                ||
                pairs.GroupBy(x => x.Child, StringComparer.OrdinalIgnoreCase)
                    .Any(group =>
                        group.Select(x => x.Parent)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Count() > 1),

            var value when value == RelationshipCardinalities.OneToMany =>
                pairs.GroupBy(x => x.Child, StringComparer.OrdinalIgnoreCase)
                    .Any(group =>
                        group.Select(x => x.Parent)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Count() > 1),

            var value when value == RelationshipCardinalities.ManyToOne =>
                pairs.GroupBy(x => x.Parent, StringComparer.OrdinalIgnoreCase)
                    .Any(group =>
                        group.Select(x => x.Child)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Count() > 1),

            _ => false
        };

        var persisted = _dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x => x.RelationshipType == relationshipType);

        var persistedViolation = relationship.Cardinality switch
        {
            var value when value == RelationshipCardinalities.OneToOne =>
                await persisted.AnyAsync(
                    x =>
                        (x.ParentMaterialUnitId == parentId &&
                         x.ChildMaterialUnitId != childId)
                        ||
                        (x.ChildMaterialUnitId == childId &&
                         x.ParentMaterialUnitId != parentId),
                    cancellationToken),

            var value when value == RelationshipCardinalities.OneToMany =>
                await persisted.AnyAsync(
                    x =>
                        x.ChildMaterialUnitId == childId &&
                        x.ParentMaterialUnitId != parentId,
                    cancellationToken),

            var value when value == RelationshipCardinalities.ManyToOne =>
                await persisted.AnyAsync(
                    x =>
                        x.ParentMaterialUnitId == parentId &&
                        x.ChildMaterialUnitId != childId,
                    cancellationToken),

            _ => false
        };

        if (!batchViolation && !persistedViolation)
            return null;

        return Refuse(
            ProjectionValidationCode.PV10,
            $"Relationship '{relationshipCode}' violates declared cardinality " +
            $"'{relationship.Cardinality}'.",
            $"{parentId}->{childId}");
    }

    private async Task<RowValidationRefusal?> ValidateWeightsAsync(
        IReadOnlyDictionary<string, string> fieldMap,
        StagingRecord stagingRecord,
        Guid childId,
        string relationshipType,
        decimal currentWeight,
        bool currentTransition,
        CancellationToken cancellationToken)
    {
        if (currentWeight <= 0m || currentWeight > 1m)
        {
            return Refuse(
                ProjectionValidationCode.PV13,
                "ContributionWeight must be > 0 and <= 1.",
                currentWeight.ToString(CultureInfo.InvariantCulture));
        }

        var currentSource = TryParseFlat(stagingRecord.RawJson);
        var currentChildIdentity = currentSource is null
            ? null
            : ReadIdentity(
                fieldMap,
                currentSource,
                "ChildMaterialUnitId",
                "ChildMaterialCode",
                "ChildAliasCode");

        var batchWeights = new List<decimal>();
        var anyTransition = currentTransition;

        if (currentChildIdentity is not null)
        {
            var rawRows = await _dbContext.StagingRecords
                .AsNoTracking()
                .Where(x =>
                    x.ImportBatchId == stagingRecord.ImportBatchId &&
                    x.SourceObjectName == stagingRecord.SourceObjectName)
                .Select(x => x.RawJson)
                .ToListAsync(cancellationToken);

            foreach (var raw in rawRows)
            {
                var source = TryParseFlat(raw);
                if (source is null)
                    continue;

                var type = ReadMapped(fieldMap, source, "RelationshipType");
                if (!string.Equals(
                    type,
                    relationshipType,
                    StringComparison.OrdinalIgnoreCase))
                    continue;

                var childIdentity = ReadIdentity(
                    fieldMap,
                    source,
                    "ChildMaterialUnitId",
                    "ChildMaterialCode",
                    "ChildAliasCode");

                if (!string.Equals(
                    childIdentity,
                    currentChildIdentity,
                    StringComparison.OrdinalIgnoreCase))
                    continue;

                var rawWeight = ReadMapped(
                    fieldMap,
                    source,
                    "ContributionWeight");

                var parsedWeight = 1m;

                if (!string.IsNullOrWhiteSpace(rawWeight) &&
                    !decimal.TryParse(
                        rawWeight,
                        NumberStyles.Number,
                        CultureInfo.InvariantCulture,
                        out parsedWeight))
                    continue;

                batchWeights.Add(parsedWeight);

                var transition = ReadMapped(
                    fieldMap,
                    source,
                    "IsTransition");

                if (bool.TryParse(transition, out var parsedTransition) &&
                    parsedTransition)
                    anyTransition = true;
            }
        }

        if (batchWeights.Count == 0)
            batchWeights.Add(currentWeight);

        var persistedWeights = await _dbContext.GenealogyEdges
            .AsNoTracking()
            .Where(x =>
                x.ChildMaterialUnitId == childId &&
                x.RelationshipType == relationshipType)
            .Select(x => x.ContributionWeight)
            .ToListAsync(cancellationToken);

        var totalEdgeCount = batchWeights.Count + persistedWeights.Count;
        var anyNonUnitWeight =
            batchWeights.Any(x => x != 1m) ||
            persistedWeights.Any(x => x != 1m);

        if (totalEdgeCount == 1 && !anyNonUnitWeight && !anyTransition)
            return null;

        var sum = batchWeights.Sum() + persistedWeights.Sum();

        if (Math.Abs(sum - 1m) <= WeightTolerance)
            return null;

        return Refuse(
            ProjectionValidationCode.PV13,
            $"Contribution weights for this child sum to " +
            $"{sum.ToString(CultureInfo.InvariantCulture)}; " +
            $"the governed total must be 1.0 Â± " +
            $"{WeightTolerance.ToString(CultureInfo.InvariantCulture)}.",
            sum.ToString(CultureInfo.InvariantCulture));
    }

    private async Task<bool> WouldCreateCycleAsync(
        Guid parentId,
        Guid childId,
        CancellationToken cancellationToken)
    {
        var persisted = await _dbContext.GenealogyEdges
            .AsNoTracking()
            .Select(x => new
            {
                x.ParentMaterialUnitId,
                x.ChildMaterialUnitId
            })
            .ToListAsync(cancellationToken);

        var adjacency = new Dictionary<Guid, List<Guid>>();

        void Add(Guid parent, Guid child)
        {
            if (!adjacency.TryGetValue(parent, out var children))
            {
                children = new List<Guid>();
                adjacency[parent] = children;
            }

            children.Add(child);
        }

        foreach (var edge in persisted)
            Add(edge.ParentMaterialUnitId, edge.ChildMaterialUnitId);

        foreach (var edge in _dbContext.GenealogyEdges.Local.Where(x => !x.IsDeleted))
            Add(edge.ParentMaterialUnitId, edge.ChildMaterialUnitId);

        var stack = new Stack<Guid>();
        var seen = new HashSet<Guid>();

        stack.Push(childId);

        while (stack.Count > 0)
        {
            var node = stack.Pop();

            if (!seen.Add(node))
                continue;

            if (node == parentId)
                return true;

            if (adjacency.TryGetValue(node, out var next))
            {
                foreach (var child in next)
                    stack.Push(child);
            }
        }

        return false;
    }

    private static bool GrainMatches(string governed, string actual)
    {
        if (string.IsNullOrWhiteSpace(governed))
            return true;

        var value = governed.Trim();

        if (value.Equals("unit", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("material", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("materialunit", StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(value, actual, StringComparison.OrdinalIgnoreCase);
    }

    private static string Format(decimal? value, string fallback) =>
        value.HasValue
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : fallback;

    private static RowValidationRefusal Refuse(
        ProjectionValidationCode code,
        string detail,
        string? offendingValue) =>
        new(code, detail, offendingValue);

    private static Dictionary<string, string?>? TryParseFlat(string raw)
    {
        try
        {
            using var document = JsonDocument.Parse(raw);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            var result =
                new Dictionary<string, string?>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => property.Value.GetString(),
                    _ => property.Value.GetRawText().Trim('"')
                };
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadMapped(
        IReadOnlyDictionary<string, string> fieldMap,
        IReadOnlyDictionary<string, string?> source,
        string target)
    {
        if (!fieldMap.TryGetValue(target, out var sourceName) ||
            string.IsNullOrWhiteSpace(sourceName))
            return null;

        if (sourceName.StartsWith(
            "const:",
            StringComparison.OrdinalIgnoreCase))
            return sourceName.Substring(6);

        return source.TryGetValue(sourceName, out var value)
            ? value
            : null;
    }

    private static string? ReadIdentity(
        IReadOnlyDictionary<string, string> fieldMap,
        IReadOnlyDictionary<string, string?> source,
        params string[] targets)
    {
        foreach (var target in targets)
        {
            var value = ReadMapped(fieldMap, source, target);

            if (!string.IsNullOrWhiteSpace(value))
                return target + ":" + value.Trim();
        }

        return null;
    }

    private sealed class FixedTenantAccessor : ITenantAccessor
    {
        private readonly Guid _tenantId;

        public FixedTenantAccessor(Guid tenantId)
        {
            _tenantId = tenantId;
        }

        public Guid TenantId => _tenantId;

        public bool TryGetTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return _tenantId != Guid.Empty;
        }
    }
}