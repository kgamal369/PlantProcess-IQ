using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Configuration;

public class OperationDefinition : BaseEntity
{
    public Guid IndustryTemplateId { get; private set; }

    public string OperationCode { get; private set; } = null!;
    // Examples: EAF_Melting, LF_Treatment, Continuous_Casting, Hot_Rolling, Mixing, Curing, Filling

    public string OperationName { get; private set; } = null!;

    public string? OperationCategory { get; private set; }
    // Examples: Melting, Casting, Rolling, Inspection, Mixing, Packaging

    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    private OperationDefinition()
    {
    }

    public OperationDefinition(
        Guid industryTemplateId,
        string operationCode,
        string operationName,
        bool isSynthetic,
        string? operationCategory = null,
        int sortOrder = 0,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (industryTemplateId == Guid.Empty)
            throw new ArgumentException("Industry template ID is required.", nameof(industryTemplateId));

        if (string.IsNullOrWhiteSpace(operationCode))
            throw new ArgumentException("Operation code is required.", nameof(operationCode));

        if (string.IsNullOrWhiteSpace(operationName))
            throw new ArgumentException("Operation name is required.", nameof(operationName));

        IndustryTemplateId = industryTemplateId;
        OperationCode = operationCode.Trim();
        OperationName = operationName.Trim();
        OperationCategory = operationCategory?.Trim();
        Description = description?.Trim();
        SortOrder = sortOrder;

        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    public void Activate()
    {
        IsActive = true;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }
}