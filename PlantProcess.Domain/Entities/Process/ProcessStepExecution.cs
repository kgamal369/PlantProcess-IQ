using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Process;

public class ProcessStepExecution : BaseEntity
{
    public Guid MaterialUnitId { get; private set; }

    public Guid? EquipmentId { get; private set; }

    public Guid? OperationDefinitionId { get; private set; }

    public string OperationType { get; private set; } = null!;

    public string? OperationCode { get; private set; }

    public string? CrewCode { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? EndedAtUtc { get; private set; }

    public DateTime StartedAtLocal { get; private set; }

    public DateTime? EndedAtLocal { get; private set; }

    public string PlantTimeZoneId { get; private set; } = "UTC";

    public int PlantUtcOffsetMinutes { get; private set; }

    public string ExecutionStatus { get; private set; } = "Completed";

    private ProcessStepExecution()
    {
    }

    public ProcessStepExecution(
        Guid materialUnitId,
        string operationType,
        DateTime startedAtUtc,
        DateTime? endedAtUtc,
        bool isSynthetic,
        Guid? equipmentId = null,
        string? operationCode = null,
        Guid? operationDefinitionId = null,
        string? crewCode = null,
        string? executionStatus = null,
        string? sourceSystem = null,
        string? sourceRecordId = null,
        string plantTimeZoneId = "UTC",
        int plantUtcOffsetMinutes = 0)
    {
        if (materialUnitId == Guid.Empty)
            throw new ArgumentException("Material unit ID is required.", nameof(materialUnitId));

        if (string.IsNullOrWhiteSpace(operationType))
            throw new ArgumentException("Operation type is required.", nameof(operationType));

        if (operationDefinitionId.HasValue && operationDefinitionId.Value == Guid.Empty)
            throw new ArgumentException("Operation definition ID cannot be empty.", nameof(operationDefinitionId));

        StartedAtUtc = EnsureUtcStrict(startedAtUtc);
        EndedAtUtc = endedAtUtc.HasValue ? EnsureUtcStrict(endedAtUtc.Value) : null;

        if (EndedAtUtc.HasValue && EndedAtUtc.Value < StartedAtUtc)
            throw new InvalidOperationException("Process step end time cannot be before start time.");

        MaterialUnitId = materialUnitId;
        EquipmentId = equipmentId;
        OperationDefinitionId = operationDefinitionId;
        OperationType = operationType.Trim();
        OperationCode = operationCode?.Trim();
        CrewCode = crewCode?.Trim();

        ExecutionStatus = string.IsNullOrWhiteSpace(executionStatus)
            ? "Completed"
            : executionStatus.Trim();

        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();

        SetLocalTimeContext(
            StartedAtUtc.AddMinutes(plantUtcOffsetMinutes),
            EndedAtUtc?.AddMinutes(plantUtcOffsetMinutes),
            plantTimeZoneId,
            plantUtcOffsetMinutes);
    }

    public void SetLocalTimeContext(
        DateTime startedAtLocal,
        DateTime? endedAtLocal,
        string timeZoneId,
        int utcOffsetMinutes)
    {
        StartedAtLocal = DateTime.SpecifyKind(startedAtLocal, DateTimeKind.Unspecified);
        EndedAtLocal = endedAtLocal.HasValue
            ? DateTime.SpecifyKind(endedAtLocal.Value, DateTimeKind.Unspecified)
            : null;

        PlantTimeZoneId = string.IsNullOrWhiteSpace(timeZoneId)
            ? "UTC"
            : timeZoneId.Trim();

        PlantUtcOffsetMinutes = utcOffsetMinutes;
        MarkAsUpdated();
    }

    public void Complete(DateTime endedAtUtc)
    {
        var normalizedEnd = EnsureUtcStrict(endedAtUtc);

        if (normalizedEnd < StartedAtUtc)
            throw new InvalidOperationException("Process step end time cannot be before start time.");

        EndedAtUtc = normalizedEnd;
        ExecutionStatus = "Completed";

        EndedAtLocal = DateTime.SpecifyKind(
            EndedAtUtc.Value.AddMinutes(PlantUtcOffsetMinutes),
            DateTimeKind.Unspecified);

        MarkAsUpdated();
    }

    public void MarkAsFailed()
    {
        ExecutionStatus = "Failed";
        MarkAsUpdated();
    }

    private static DateTime EnsureUtcStrict(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTime value must be UTC.");

        return value;
    }
}