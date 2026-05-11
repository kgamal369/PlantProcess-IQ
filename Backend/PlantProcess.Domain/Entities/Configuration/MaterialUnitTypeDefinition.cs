using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Configuration;

public class MaterialUnitTypeDefinition : BaseEntity
{
    public Guid IndustryTemplateId { get; private set; }

    public string MaterialUnitTypeCode { get; private set; } = null!;
    // Examples: Heat, Cast, Slab, Coil, Batch, Lot, Roll, Tire, Component

    public string MaterialUnitTypeName { get; private set; } = null!;

    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; } = true;

    private MaterialUnitTypeDefinition()
    {
    }

    public MaterialUnitTypeDefinition(
        Guid industryTemplateId,
        string materialUnitTypeCode,
        string materialUnitTypeName,
        bool isSynthetic,
        int sortOrder = 0,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (industryTemplateId == Guid.Empty)
            throw new ArgumentException("Industry template ID is required.", nameof(industryTemplateId));

        if (string.IsNullOrWhiteSpace(materialUnitTypeCode))
            throw new ArgumentException("Material unit type code is required.", nameof(materialUnitTypeCode));

        if (string.IsNullOrWhiteSpace(materialUnitTypeName))
            throw new ArgumentException("Material unit type name is required.", nameof(materialUnitTypeName));

        IndustryTemplateId = industryTemplateId;
        MaterialUnitTypeCode = materialUnitTypeCode.Trim();
        MaterialUnitTypeName = materialUnitTypeName.Trim();
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