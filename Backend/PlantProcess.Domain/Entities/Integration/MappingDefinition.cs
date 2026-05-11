using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

public class MappingDefinition : BaseEntity
{
    public Guid SourceSystemDefinitionId { get; private set; }

    public string MappingCode { get; private set; } = null!;

    public string MappingName { get; private set; } = null!;

    public string SourceObjectName { get; private set; } = null!;
    // Example: MES_HEATS table, L2_CAST_DATA view, inspection.csv

    public string TargetEntityName { get; private set; } = null!;
    // Example: MaterialUnit, ParameterObservation, QualityEvent

    public string MappingJson { get; private set; } = null!;
    // JSON mapping rules, for example:
    // { "MaterialCode": "heat_id", "GradeOrRecipe": "steel_grade" }

    public string MappingVersion { get; private set; } = "v1";

    public bool IsActive { get; private set; } = true;

    public string? Description { get; private set; }

    private MappingDefinition()
    {
    }

    public MappingDefinition(
        Guid sourceSystemDefinitionId,
        string mappingCode,
        string mappingName,
        string sourceObjectName,
        string targetEntityName,
        string mappingJson,
        bool isSynthetic,
        string mappingVersion = "v1",
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (sourceSystemDefinitionId == Guid.Empty)
            throw new ArgumentException("Source system definition ID is required.", nameof(sourceSystemDefinitionId));

        if (string.IsNullOrWhiteSpace(mappingCode))
            throw new ArgumentException("Mapping code is required.", nameof(mappingCode));

        if (string.IsNullOrWhiteSpace(mappingName))
            throw new ArgumentException("Mapping name is required.", nameof(mappingName));

        if (string.IsNullOrWhiteSpace(sourceObjectName))
            throw new ArgumentException("Source object name is required.", nameof(sourceObjectName));

        if (string.IsNullOrWhiteSpace(targetEntityName))
            throw new ArgumentException("Target entity name is required.", nameof(targetEntityName));

        if (string.IsNullOrWhiteSpace(mappingJson))
            throw new ArgumentException("Mapping JSON is required.", nameof(mappingJson));

        SourceSystemDefinitionId = sourceSystemDefinitionId;
        MappingCode = mappingCode.Trim();
        MappingName = mappingName.Trim();
        SourceObjectName = sourceObjectName.Trim();
        TargetEntityName = targetEntityName.Trim();
        MappingJson = mappingJson.Trim();
        MappingVersion = string.IsNullOrWhiteSpace(mappingVersion)
            ? "v1"
            : mappingVersion.Trim();
        Description = description?.Trim();

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

    public void UpdateMappingJson(string mappingJson, string mappingVersion)
    {
        if (string.IsNullOrWhiteSpace(mappingJson))
            throw new ArgumentException("Mapping JSON is required.", nameof(mappingJson));

        if (string.IsNullOrWhiteSpace(mappingVersion))
            throw new ArgumentException("Mapping version is required.", nameof(mappingVersion));

        MappingJson = mappingJson.Trim();
        MappingVersion = mappingVersion.Trim();
        MarkAsUpdated();
    }
}