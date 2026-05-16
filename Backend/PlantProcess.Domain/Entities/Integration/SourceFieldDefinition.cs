using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// Stores discovered source fields for a source dataset.
/// 
/// For CSV/Excel this comes from uploaded sample headers and sample values.
/// For SQL providers this will later come from INFORMATION_SCHEMA / system catalogs.
/// </summary>
public class SourceFieldDefinition : BaseEntity
{
    public Guid SourceDatasetDefinitionId { get; private set; }

    public string FieldName { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public string SourceDataType { get; private set; } = null!;

    public int Ordinal { get; private set; }

    public bool IsNullable { get; private set; }

    public int? MaxLength { get; private set; }

    public int? NumericPrecision { get; private set; }

    public int? NumericScale { get; private set; }

    public string? SampleValue { get; private set; }

    public bool IsPrimaryKeyCandidate { get; private set; }

    public bool IsTimestampCandidate { get; private set; }

    public bool IsActive { get; private set; } = true;

    private SourceFieldDefinition()
    {
    }

    public SourceFieldDefinition(
        Guid sourceDatasetDefinitionId,
        string fieldName,
        string displayName,
        string sourceDataType,
        int ordinal,
        bool isNullable,
        bool isSynthetic,
        int? maxLength = null,
        int? numericPrecision = null,
        int? numericScale = null,
        string? sampleValue = null,
        bool isPrimaryKeyCandidate = false,
        bool isTimestampCandidate = false,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (sourceDatasetDefinitionId == Guid.Empty)
            throw new ArgumentException("Source dataset definition ID is required.", nameof(sourceDatasetDefinitionId));

        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name is required.", nameof(fieldName));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(sourceDataType))
            throw new ArgumentException("Source data type is required.", nameof(sourceDataType));

        if (ordinal < 1)
            throw new ArgumentOutOfRangeException(nameof(ordinal), "Ordinal must be greater than zero.");

        SourceDatasetDefinitionId = sourceDatasetDefinitionId;
        FieldName = fieldName.Trim();
        DisplayName = displayName.Trim();
        SourceDataType = sourceDataType.Trim();
        Ordinal = ordinal;
        IsNullable = isNullable;
        MaxLength = maxLength;
        NumericPrecision = numericPrecision;
        NumericScale = numericScale;
        SampleValue = Clean(sampleValue);
        IsPrimaryKeyCandidate = isPrimaryKeyCandidate;
        IsTimestampCandidate = isTimestampCandidate;
        IsActive = true;

        IsSynthetic = isSynthetic;
        SourceSystem = Clean(sourceSystem);
        SourceRecordId = Clean(sourceRecordId);
    }

    public void UpdateProfile(
        string displayName,
        string sourceDataType,
        bool isNullable,
        int? maxLength,
        int? numericPrecision,
        int? numericScale,
        string? sampleValue,
        bool isPrimaryKeyCandidate,
        bool isTimestampCandidate)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(sourceDataType))
            throw new ArgumentException("Source data type is required.", nameof(sourceDataType));

        DisplayName = displayName.Trim();
        SourceDataType = sourceDataType.Trim();
        IsNullable = isNullable;
        MaxLength = maxLength;
        NumericPrecision = numericPrecision;
        NumericScale = numericScale;
        SampleValue = Clean(sampleValue);
        IsPrimaryKeyCandidate = isPrimaryKeyCandidate;
        IsTimestampCandidate = isTimestampCandidate;

        MarkAsUpdated();
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

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}