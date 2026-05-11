using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Materials;

public class MaterialAlias : BaseEntity
{
    public Guid MaterialUnitId { get; private set; }

    public string AliasCode { get; private set; } = null!;

    public string AliasType { get; private set; } = "SourceSystemId";

    private MaterialAlias()
    {
    }

    public MaterialAlias(
        Guid materialUnitId,
        string aliasCode,
        string sourceSystem,
        string? aliasType,
        bool isSynthetic)
    {
        if (materialUnitId == Guid.Empty)
            throw new ArgumentException("Material unit ID is required.", nameof(materialUnitId));

        if (string.IsNullOrWhiteSpace(aliasCode))
            throw new ArgumentException("Alias code is required.", nameof(aliasCode));

        if (string.IsNullOrWhiteSpace(sourceSystem))
            throw new ArgumentException("Source system is required.", nameof(sourceSystem));

        MaterialUnitId = materialUnitId;
        AliasCode = aliasCode.Trim();
        SourceSystem = sourceSystem.Trim();
        AliasType = string.IsNullOrWhiteSpace(aliasType)
            ? "SourceSystemId"
            : aliasType.Trim();
        IsSynthetic = isSynthetic;
    }
}