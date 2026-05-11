using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Process;

public class ParameterDefinition : BaseEntity
{
    public string ParameterCode { get; private set; } = null!;

    public string ParameterName { get; private set; } = null!;

    public string ValueType { get; private set; } = "Numeric";
    // Numeric, Text, Boolean, Categorical, DateTime

    public string? UnitOfMeasure { get; private set; }

    public string? ParameterCategory { get; private set; }

    public string? IndustryTemplate { get; private set; }

    public decimal? ExpectedMinValue { get; private set; }

    public decimal? ExpectedMaxValue { get; private set; }

    private ParameterDefinition()
    {
    }

    public ParameterDefinition(
        string parameterCode,
        string parameterName,
        string valueType,
        string? unitOfMeasure,
        string? parameterCategory,
        string? industryTemplate,
        bool isSynthetic,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(parameterCode))
            throw new ArgumentException("Parameter code is required.", nameof(parameterCode));

        if (string.IsNullOrWhiteSpace(parameterName))
            throw new ArgumentException("Parameter name is required.", nameof(parameterName));

        ParameterCode = parameterCode.Trim();
        ParameterName = parameterName.Trim();
        ValueType = string.IsNullOrWhiteSpace(valueType) ? "Numeric" : valueType.Trim();
        UnitOfMeasure = unitOfMeasure?.Trim();
        ParameterCategory = parameterCategory?.Trim();
        IndustryTemplate = industryTemplate?.Trim();
        IsSynthetic = isSynthetic;
        SourceSystem = sourceSystem?.Trim();
        SourceRecordId = sourceRecordId?.Trim();
    }

    public void SetExpectedRange(decimal? minValue, decimal? maxValue)
    {
        if (minValue.HasValue && maxValue.HasValue && maxValue.Value < minValue.Value)
            throw new InvalidOperationException("Expected max value cannot be less than expected min value.");

        ExpectedMinValue = minValue;
        ExpectedMaxValue = maxValue;
        MarkAsUpdated();
    }
}