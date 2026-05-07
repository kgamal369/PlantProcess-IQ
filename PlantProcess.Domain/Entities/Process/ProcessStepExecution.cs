using PlantProcess.Domain.Common;
using PlantProcess.Domain.Entities.PlantLayout;

namespace PlantProcess.Domain.Entities.Process;

public class ProcessStepExecution : BaseEntity
{
    public Guid MaterialUnitId { get; private set; }

    public Guid? EquipmentId { get; private set; }

    public string OperationType { get; private set; } = null!;
    // Steel: EAF_Melting, LF_Treatment, Continuous_Casting, Hot_Rolling
    // Pharma: Mixing, Filling, Packaging
    // Tire: Mixing, Curing, Inspection

    public string? OperationCode { get; private set; }

    public string? CrewCode { get; private set; }

    public DateTime StartedAtUtc { get; private set; }

    public DateTime? EndedAtUtc { get; private set; }

    public DateTime StartedAtLocal { get; private set; }

    public DateTime? EndedAtLocal { get; private set; }

    public string PlantTimeZoneId { get; private set; } = "Europe/Berlin";

    public int PlantUtcOffsetMinutes { get; private set; }

    public string ExecutionStatus { get; private set; } = "Completed";
    // Planned, Running, Completed, Failed, Cancelled, Unknown

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
        Guid? productionLineId = null,
        string? crewCode = null,
        string? executionStatus = null,
        string? sourceSystem = null,
        string? sourceRecordId = null,
        string plantTimeZoneId = "Europe/Berlin",
        int plantUtcOffsetMinutes = 60)
    {
        if (materialUnitId == Guid.Empty)
            throw new ArgumentException("Material unit ID is required.", nameof(materialUnitId));

        if (string.IsNullOrWhiteSpace(operationType))
            throw new ArgumentException("Operation type is required.", nameof(operationType));

        StartedAtUtc = EnsureUtc(startedAtUtc);
        EndedAtUtc = endedAtUtc.HasValue ? EnsureUtc(endedAtUtc.Value) : null;

        if (EndedAtUtc.HasValue && EndedAtUtc.Value < StartedAtUtc)
            throw new InvalidOperationException("Process step end time cannot be before start time.");

        MaterialUnitId = materialUnitId;
        OperationType = operationType.Trim();
        EquipmentId = equipmentId;
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
            ? "Europe/Berlin"
            : timeZoneId.Trim();

        PlantUtcOffsetMinutes = utcOffsetMinutes;
        MarkAsUpdated();
    }

    public void Complete(DateTime endedAtUtc)
    {
        var normalizedEnd = EnsureUtc(endedAtUtc);

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

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}