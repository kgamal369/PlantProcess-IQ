using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// Represents a registered external data source (MES, L2, historian, QMS, lab, inspection,
/// downtime/CMMS, ERP, CSV, Excel, API) that PlantProcess IQ reads from.
/// IsReadOnlySource = true enforces the audit/no-write contract for pilot deployments.
/// IsActive = false disables the source without deleting its history.
/// </summary>
public class SourceSystemDefinition : BaseEntity
{
    // ─── Properties ───────────────────────────────────────────────────────────

    public string SourceSystemCode { get; private set; } = null!;

    public string SourceSystemName { get; private set; } = null!;

    public string SourceSystemType { get; private set; } = null!;
    // Examples: MES, Level2, SCADA, Historian, QMS, Lab, Inspection, CMMS, ERP, CSV, Excel, API

    public string? Description { get; private set; }

    /// <summary>
    /// When true, PlantProcess IQ must only read from this source system.
    /// A non-read-only source system is flagged by the data-quality scan.
    /// </summary>
    public bool IsReadOnlySource { get; private set; } = true;

    /// <summary>
    /// Controls whether this source system is available for new import batches.
    /// Deactivated sources are retained for historical audit.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    // ─── EF Core parameterless constructor ────────────────────────────────────
    private SourceSystemDefinition() { }

    // ─── Public constructor ───────────────────────────────────────────────────
    public SourceSystemDefinition(
        string sourceSystemCode,
        string sourceSystemName,
        string sourceSystemType,
        bool isSynthetic,
        string? description = null,
        bool isReadOnlySource = true,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(sourceSystemCode))
            throw new ArgumentException("Source system code is required.", nameof(sourceSystemCode));

        if (string.IsNullOrWhiteSpace(sourceSystemName))
            throw new ArgumentException("Source system name is required.", nameof(sourceSystemName));

        if (string.IsNullOrWhiteSpace(sourceSystemType))
            throw new ArgumentException("Source system type is required.", nameof(sourceSystemType));

        SourceSystemCode = sourceSystemCode.Trim();
        SourceSystemName = sourceSystemName.Trim();
        SourceSystemType = sourceSystemType.Trim();
        Description = description?.Trim();
        IsReadOnlySource = isReadOnlySource;
        IsActive = true;

        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    // ─── Domain behaviour ─────────────────────────────────────────────────────

    /// <summary>
    /// Activates a previously deactivated source system.
    /// After activation it can be used for new import batches.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }

    /// <summary>
    /// Deactivates the source system without deleting it.
    /// Existing import batches and historical data are retained.
    /// A deactivated system should not be used for new import batches.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }

    /// <summary>
    /// Updates the human-readable description of the source system.
    /// </summary>
    public void UpdateDescription(string? description)
    {
        Description = description?.Trim();
        MarkAsUpdated();
    }
}