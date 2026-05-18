using PlantProcess.Application.Common.Paging;

namespace PlantProcess.Application.Contracts.Quality;

public sealed record QualityEventReadDto(
    Guid Id,
    Guid MaterialUnitId,
    string MaterialCode,
    string MaterialUnitType,
    Guid? DefectCatalogId,
    string? DefectCode,
    string? DefectName,
    string? DefectCategory,
    string EventType,
    DateTime EventAtUtc,
    DateTime EventAtLocal,
    string PlantTimeZoneId,
    int PlantUtcOffsetMinutes,
    string? Severity,
    string? Decision,
    string? Description,
    string? SourceSystem,
    string? SourceRecordId,
    bool IsSynthetic);

public sealed record QualityEventQuery(
    Guid? MaterialUnitId,
    Guid? DefectCatalogId,
    string? EventType,
    string? Decision,
    string? Severity,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Page,
    int PageSize);



