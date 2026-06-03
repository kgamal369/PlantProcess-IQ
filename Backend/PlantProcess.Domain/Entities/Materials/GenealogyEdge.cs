using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Materials;

public class GenealogyEdge : BaseEntity
{
    public Guid ParentMaterialUnitId { get; private set; }

    public Guid ChildMaterialUnitId { get; private set; }

    public string RelationshipType { get; private set; } = null!;
    // ProducedInto, SplitInto, RolledInto, PackedInto, ProcessedInto

    public DateTime? EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    /// <summary>
    /// Doctrine v5 P06: contribution of this parent to the child material.
    /// Single-parent edges default to 1.0. Transition/blended children have
    /// multiple edges whose weights must sum to approximately 1.0.
    /// </summary>
    public decimal ContributionWeight { get; private set; } = 1.0m;

    /// <summary>
    /// True when the edge participates in blended/transition provenance.
    /// Example: transition coil receives 0.62 from heat A and 0.38 from heat B.
    /// </summary>
    public bool IsTransition { get; private set; }

    /// <summary>
    /// Confidence in the provenance allocation, 0..1.
    /// </summary>
    public decimal ProvenanceConfidence { get; private set; } = 1.0m;

    private GenealogyEdge()
    {
    }

    public GenealogyEdge(
        Guid parentMaterialUnitId,
        Guid childMaterialUnitId,
        string relationshipType,
        bool isSynthetic,
        string? sourceSystem = null,
        string? sourceRecordId = null,
        decimal contributionWeight = 1.0m,
        bool isTransition = false,
        decimal provenanceConfidence = 1.0m)
    {
        if (parentMaterialUnitId == Guid.Empty)
            throw new ArgumentException("Parent material unit ID is required.", nameof(parentMaterialUnitId));

        if (childMaterialUnitId == Guid.Empty)
            throw new ArgumentException("Child material unit ID is required.", nameof(childMaterialUnitId));

        if (parentMaterialUnitId == childMaterialUnitId)
            throw new InvalidOperationException("A material cannot be its own parent.");

        if (string.IsNullOrWhiteSpace(relationshipType))
            throw new ArgumentException("Relationship type is required.", nameof(relationshipType));

        ParentMaterialUnitId = parentMaterialUnitId;
        ChildMaterialUnitId = childMaterialUnitId;
        RelationshipType = relationshipType.Trim();
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();

        SetProvenance(contributionWeight, isTransition, provenanceConfidence);
    }

    public void SetEffectiveWindow(DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc.HasValue && toUtc.HasValue && toUtc.Value < fromUtc.Value)
            throw new InvalidOperationException("Effective end cannot be before effective start.");

        EffectiveFromUtc = fromUtc.HasValue ? EnsureUtc(fromUtc.Value) : null;
        EffectiveToUtc = toUtc.HasValue ? EnsureUtc(toUtc.Value) : null;
        MarkAsUpdated();
    }

    public void SetProvenance(
        decimal contributionWeight,
        bool isTransition,
        decimal provenanceConfidence)
    {
        if (contributionWeight <= 0m || contributionWeight > 1m)
            throw new InvalidOperationException("Contribution weight must be > 0 and <= 1.");

        if (provenanceConfidence < 0m || provenanceConfidence > 1m)
            throw new InvalidOperationException("Provenance confidence must be between 0 and 1.");

        ContributionWeight = decimal.Round(contributionWeight, 6);
        IsTransition = isTransition;
        ProvenanceConfidence = decimal.Round(provenanceConfidence, 6);
        MarkAsUpdated();
    }

    public static void ValidateChildContributionWeights(IEnumerable<GenealogyEdge> childEdges, decimal tolerance = 0.015m)
    {
        var activeEdges = childEdges
            .Where(x => !x.IsDeleted)
            .ToArray();

        if (activeEdges.Length == 0)
            return;

        var childIds = activeEdges
            .Select(x => x.ChildMaterialUnitId)
            .Distinct()
            .ToArray();

        if (childIds.Length != 1)
            throw new InvalidOperationException("Contribution-weight validation must be executed for one child material at a time.");

        var sum = activeEdges.Sum(x => x.ContributionWeight);

        if (Math.Abs(sum - 1.0m) > tolerance)
            throw new InvalidOperationException($"Contribution weights must sum to 1.0 per child. Current sum: {sum}.");
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}