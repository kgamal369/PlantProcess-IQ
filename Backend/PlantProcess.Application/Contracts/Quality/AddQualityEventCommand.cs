using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Quality;

public sealed record AddQualityEventCommand(
    Guid MaterialUnitId,
    Guid? DefectCatalogId,
    string EventType,
    DateTime EventAtUtc,
    string? Severity,
    string? Decision,
    string? Description,
    string? PlantTimeZoneId,
    int? PlantUtcOffsetMinutes,
    CommandMetadata Metadata);



