using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Materials;

public sealed record CreateGenealogyEdgeCommand(
    Guid ParentMaterialUnitId,
    Guid ChildMaterialUnitId,
    string RelationshipType,
    DateTime? EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    decimal? Quantity,
    string? UnitOfMeasure,
    CommandMetadata Metadata);


