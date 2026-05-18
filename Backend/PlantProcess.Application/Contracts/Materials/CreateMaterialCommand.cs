using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Materials;

public sealed record CreateMaterialCommand(
    string MaterialCode,
    string MaterialUnitType,
    Guid SiteId,
    string? ProductFamily,
    string? GradeOrRecipe,
    DateTime? ProductionStartUtc,
    DateTime? ProductionEndUtc,
    string? PlantTimeZoneId,
    int? PlantUtcOffsetMinutes,
    CommandMetadata Metadata);



