namespace PlantProcess.Application.Integration.Contracts.Mapping;

/// <summary>
/// PPIQ T-099. What happened to exactly one staged row.
///
/// There is deliberately no Failed member. An expected refusal is Quarantined
/// and carries a code; an unexpected infrastructure fault is not an outcome at
/// all and propagates as a genuine operation failure, because a database that
/// is down is not a property of the row.
/// </summary>
public enum RowProjectionKind
{
    Mapped = 0,
    Skipped = 1,
    Quarantined = 2
}

public sealed record RowProjectionOutcome(
    RowProjectionKind Kind,
    Guid? CanonicalEntityId,
    string? CanonicalEntityName,
    string? Message,
    RowValidationRefusal? Refusal)
{
    public static RowProjectionOutcome Mapped(Guid id, string entityName) =>
        new(RowProjectionKind.Mapped, id, entityName, null, null);

    public static RowProjectionOutcome Skipped(Guid? id, string entityName, string reason) =>
        new(RowProjectionKind.Skipped, id, entityName, reason, null);

    public static RowProjectionOutcome Quarantined(RowValidationRefusal refusal) =>
        new(RowProjectionKind.Quarantined, null, null, refusal.Detail, refusal);
}
