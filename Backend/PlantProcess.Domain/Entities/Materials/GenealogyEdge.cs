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

    private GenealogyEdge()
    {
    }

    public GenealogyEdge(
        Guid parentMaterialUnitId,
        Guid childMaterialUnitId,
        string relationshipType,
        bool isSynthetic,
        string? sourceSystem = null,
        string? sourceRecordId = null)
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
    }

    public void SetEffectiveWindow(DateTime? fromUtc, DateTime? toUtc)
    {
        if (fromUtc.HasValue && toUtc.HasValue && toUtc.Value < fromUtc.Value)
            throw new InvalidOperationException("Effective end cannot be before effective start.");

        EffectiveFromUtc = fromUtc.HasValue ? EnsureUtc(fromUtc.Value) : null;
        EffectiveToUtc = toUtc.HasValue ? EnsureUtc(toUtc.Value) : null;
        MarkAsUpdated();
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}