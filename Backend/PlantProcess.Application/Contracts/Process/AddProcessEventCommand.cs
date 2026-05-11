using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Process;

public sealed record AddProcessEventCommand(
    Guid? MaterialUnitId,
    Guid? ProcessStepExecutionId,
    Guid? EquipmentId,
    string EventType,
    DateTime EventAtUtc,
    string? EventValue,
    string? Description,
    string? PlantTimeZoneId,
    int? PlantUtcOffsetMinutes,
    CommandMetadata Metadata);