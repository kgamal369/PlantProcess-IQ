using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

public class SourceSystemDefinition : BaseEntity
{
    public string SourceSystemCode { get; private set; } = null!;

    public string SourceSystemName { get; private set; } = null!;

    public string SourceSystemType { get; private set; } = null!;
    // Examples: MES, Level2, SCADA, Historian, QMS, Lab, Inspection, ERP, CSV, Excel, API

    public string? Description { get; private set; }

    public bool IsReadOnlySource { get; private set; } = true;

    private SourceSystemDefinition()
    {
    }

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

        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }
}