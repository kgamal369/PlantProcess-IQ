using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Process;

public sealed record AddDowntimeEventCommand(
    Guid? MaterialUnitId,
    Guid? ProcessStepExecutionId,
    Guid? EquipmentId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    string DowntimeType,
    string? ReasonCode,
    string? Description,
    string? PlantTimeZoneId,
    int? PlantUtcOffsetMinutes,
    CommandMetadata Metadata);



