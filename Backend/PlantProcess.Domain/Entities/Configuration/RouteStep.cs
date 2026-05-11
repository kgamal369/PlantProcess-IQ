using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Configuration;

public class RouteStep : BaseEntity
{
    public Guid RouteId { get; private set; }

    public Guid OperationDefinitionId { get; private set; }

    public int SequenceNo { get; private set; }

    public string? ExpectedMaterialUnitType { get; private set; }
    // Examples: Heat, Cast, Slab, Coil, Batch, Lot, Roll

    public bool IsRequired { get; private set; } = true;

    public string? Description { get; private set; }

    private RouteStep()
    {
    }

    public RouteStep(
        Guid routeId,
        Guid operationDefinitionId,
        int sequenceNo,
        bool isSynthetic,
        string? expectedMaterialUnitType = null,
        bool isRequired = true,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (routeId == Guid.Empty)
            throw new ArgumentException("Route ID is required.", nameof(routeId));

        if (operationDefinitionId == Guid.Empty)
            throw new ArgumentException("Operation definition ID is required.", nameof(operationDefinitionId));

        if (sequenceNo <= 0)
            throw new ArgumentOutOfRangeException(nameof(sequenceNo), "Sequence number must be greater than zero.");

        RouteId = routeId;
        OperationDefinitionId = operationDefinitionId;
        SequenceNo = sequenceNo;
        ExpectedMaterialUnitType = expectedMaterialUnitType?.Trim();
        IsRequired = isRequired;
        Description = description?.Trim();

        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }
}