using PlantProcess.Application.Contracts.Common;

namespace PlantProcess.Application.Contracts.Process;

public sealed record AddProcessStepCommand(
    Guid MaterialUnitId,
    Guid? EquipmentId,
    Guid? OperationDefinitionId,
    string OperationType,
    string? OperationCode,
    string? CrewCode,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    string? ExecutionStatus,
    string? PlantTimeZoneId,
    int? PlantUtcOffsetMinutes,
    CommandMetadata Metadata);


