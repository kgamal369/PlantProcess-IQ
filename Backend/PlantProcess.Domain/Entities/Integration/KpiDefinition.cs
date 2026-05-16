using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// Represents a customer-defined KPI created from a SchemaViewDefinition.
/// 
/// Example:
/// - DefectRateByShift
/// - DowntimeMinutesByEquipment
/// - AvgTemperatureByProductFamily
/// 
/// This is metadata only. The KPI value is calculated through preview/query execution.
/// </summary>
public class KpiDefinition : BaseEntity
{
    public Guid? SchemaViewDefinitionId { get; private set; }

    public string KpiCode { get; private set; } = null!;

    public string KpiName { get; private set; } = null!;

    public string KpiCategory { get; private set; } = null!;
    // Quality, Process, Downtime, Production, Energy, Custom

    public string ValueExpression { get; private set; } = null!;
    // Example: defect_count::decimal / nullif(total_count, 0)

    public string? Unit { get; private set; }

    public string? DimensionExpression { get; private set; }
    // Example: shift_code, equipment_code, product_family

    public string? FilterExpression { get; private set; }

    public string AggregationType { get; private set; } = "Custom";
    // Sum, Avg, Min, Max, Count, Ratio, Custom

    public string KpiOptionsJson { get; private set; } = "{}";

    public bool IsActive { get; private set; } = true;

    public string? Description { get; private set; }

    private KpiDefinition()
    {
    }

    public KpiDefinition(
        string kpiCode,
        string kpiName,
        string kpiCategory,
        string valueExpression,
        bool isSynthetic,
        Guid? schemaViewDefinitionId = null,
        string? unit = null,
        string? dimensionExpression = null,
        string? filterExpression = null,
        string? aggregationType = null,
        string? kpiOptionsJson = null,
        string? description = null,
        string? sourceSystem = null,
        string? sourceRecordId = null)
    {
        if (string.IsNullOrWhiteSpace(kpiCode))
            throw new ArgumentException("KPI code is required.", nameof(kpiCode));

        if (string.IsNullOrWhiteSpace(kpiName))
            throw new ArgumentException("KPI name is required.", nameof(kpiName));

        if (string.IsNullOrWhiteSpace(kpiCategory))
            throw new ArgumentException("KPI category is required.", nameof(kpiCategory));

        if (string.IsNullOrWhiteSpace(valueExpression))
            throw new ArgumentException("KPI value expression is required.", nameof(valueExpression));

        SchemaViewDefinitionId = schemaViewDefinitionId;
        KpiCode = kpiCode.Trim();
        KpiName = kpiName.Trim();
        KpiCategory = NormalizeCategory(kpiCategory);
        ValueExpression = valueExpression.Trim();
        Unit = Clean(unit);
        DimensionExpression = Clean(dimensionExpression);
        FilterExpression = Clean(filterExpression);
        AggregationType = string.IsNullOrWhiteSpace(aggregationType)
            ? "Custom"
            : aggregationType.Trim();

        KpiOptionsJson = string.IsNullOrWhiteSpace(kpiOptionsJson)
            ? "{}"
            : kpiOptionsJson.Trim();

        Description = Clean(description);
        IsActive = true;

        IsSynthetic = isSynthetic;
        SourceSystem = Clean(sourceSystem);
        SourceRecordId = Clean(sourceRecordId);
    }

    public void Update(
        string kpiName,
        string kpiCategory,
        string valueExpression,
        Guid? schemaViewDefinitionId,
        string? unit,
        string? dimensionExpression,
        string? filterExpression,
        string? aggregationType,
        string? kpiOptionsJson,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(kpiName))
            throw new ArgumentException("KPI name is required.", nameof(kpiName));

        if (string.IsNullOrWhiteSpace(kpiCategory))
            throw new ArgumentException("KPI category is required.", nameof(kpiCategory));

        if (string.IsNullOrWhiteSpace(valueExpression))
            throw new ArgumentException("KPI value expression is required.", nameof(valueExpression));

        KpiName = kpiName.Trim();
        KpiCategory = NormalizeCategory(kpiCategory);
        ValueExpression = valueExpression.Trim();
        SchemaViewDefinitionId = schemaViewDefinitionId;
        Unit = Clean(unit);
        DimensionExpression = Clean(dimensionExpression);
        FilterExpression = Clean(filterExpression);
        AggregationType = string.IsNullOrWhiteSpace(aggregationType)
            ? "Custom"
            : aggregationType.Trim();

        KpiOptionsJson = string.IsNullOrWhiteSpace(kpiOptionsJson)
            ? "{}"
            : kpiOptionsJson.Trim();

        Description = Clean(description);

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

    private static string NormalizeCategory(string value)
    {
        var normalized = value.Trim();

        return normalized.ToLowerInvariant() switch
        {
            "quality" => "Quality",
            "process" => "Process",
            "downtime" => "Downtime",
            "production" => "Production",
            "energy" => "Energy",
            "custom" => "Custom",
            _ => normalized
        };
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}