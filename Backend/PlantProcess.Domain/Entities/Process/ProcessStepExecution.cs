using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Process;

public class ProcessStepExecution : BaseEntity
{
    private static readonly HashSet<string> AllowedStatusesSet = new(StringComparer.OrdinalIgnoreCase)
    {
        "Pending",
        "Running",
        "Completed",
        "Aborted",
        "Failed"
    };

    public static IReadOnlyCollection<string> AllowedStatuses => AllowedStatusesSet.ToArray();

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

        if (equipmentId.HasValue && equipmentId.Value == Guid.Empty)
            throw new ArgumentException("Equipment ID cannot be empty.", nameof(equipmentId));

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

        ExecutionStatus = NormalizeStatus(executionStatus ?? (EndedAtUtc.HasValue ? "Completed" : "Running"));

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

    public void Complete(DateTime endedAtUtc, string? executionStatus = null)
    {
        var normalizedEnd = EnsureUtcStrict(endedAtUtc);

        if (normalizedEnd < StartedAtUtc)
            throw new InvalidOperationException("Process step end time cannot be before start time.");

        EndedAtUtc = normalizedEnd;
        ExecutionStatus = NormalizeStatus(executionStatus ?? "Completed");

        if (!ExecutionStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Complete operation can only set status to Completed.");

        EndedAtLocal = DateTime.SpecifyKind(
            EndedAtUtc.Value.AddMinutes(PlantUtcOffsetMinutes),
            DateTimeKind.Unspecified);

        MarkAsUpdated();
    }

    public void Abort(DateTime endedAtUtc, string? reason = null)
    {
        var normalizedEnd = EnsureUtcStrict(endedAtUtc);

        if (normalizedEnd < StartedAtUtc)
            throw new InvalidOperationException("Process step abort time cannot be before start time.");

        EndedAtUtc = normalizedEnd;
        ExecutionStatus = "Aborted";
        EndedAtLocal = DateTime.SpecifyKind(
            EndedAtUtc.Value.AddMinutes(PlantUtcOffsetMinutes),
            DateTimeKind.Unspecified);

        if (!string.IsNullOrWhiteSpace(reason))
            SourceRecordId = string.IsNullOrWhiteSpace(SourceRecordId)
                ? $"AbortReason:{reason.Trim()}"
                : SourceRecordId;

        MarkAsUpdated();
    }

    public void MarkAsFailed()
    {
        ExecutionStatus = "Failed";
        MarkAsUpdated();
    }

    private static string NormalizeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return "Running";

        var trimmed = status.Trim();
        if (!AllowedStatusesSet.Contains(trimmed))
            throw new ArgumentException(
                $"Invalid execution status '{status}'. Allowed values: {string.Join(", ", AllowedStatusesSet.OrderBy(x => x))}.",
                nameof(status));

        return AllowedStatusesSet.First(x => x.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static DateTime EnsureUtcStrict(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTime value must be UTC.");

        return value;
    }
}
