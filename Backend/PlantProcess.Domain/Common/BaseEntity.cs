namespace PlantProcess.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; protected set; }
    public bool IsSynthetic { get; protected set; }
    public string? SourceSystem { get; protected set; }
    public string? SourceRecordId { get; protected set; }
    public bool IsDeleted { get; protected set; }
    public DateTime? DeletedAtUtc { get; protected set; }
    public string? DeletedReason { get; protected set; }

    public void MarkAsUpdated() => UpdatedAtUtc = DateTime.UtcNow;

    public void SoftDelete(string? reason = null)
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        DeletedReason = reason;
        MarkAsUpdated();
    }
}