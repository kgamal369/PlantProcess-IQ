namespace PlantProcess.Application.Contracts.Analytics;

public sealed record DashboardMetadataDto(
    DateTime GeneratedAtUtc,
    IReadOnlyList<DashboardDimensionMetadataDto> Dimensions,
    IReadOnlyList<DashboardMeasureMetadataDto> Measures,
    IReadOnlyList<DashboardChartTypeMetadataDto> ChartTypes,
    IReadOnlyList<DashboardFilterMetadataDto> Filters,
    IReadOnlyList<DashboardPurposeMetadataDto> Purposes,
    IReadOnlyList<DashboardCompatibilityRuleDto> CompatibilityRules,
    DashboardQuerySafetyLimitsDto SafetyLimits);

public sealed record DashboardDimensionMetadataDto(
    string Code,
    string Label,
    string Category,
    string DataType,
    bool RequiresParameterCode,
    IReadOnlyList<string> CompatibleChartTypes,
    string? Description);

public sealed record DashboardMeasureMetadataDto(
    string Code,
    string Label,
    string Category,
    string Aggregation,
    string? Unit,
    bool RequiresParameterCode,
    IReadOnlyList<string> CompatibleChartTypes,
    string? Description);

public sealed record DashboardChartTypeMetadataDto(
    string Code,
    string Label,
    string Category,
    bool SupportsDimension,
    bool SupportsMeasure,
    bool SupportsMultipleSeries,
    bool SupportsParameterSelection,
    string? Description);

public sealed record DashboardFilterMetadataDto(
    string Code,
    string Label,
    string Category,
    string DataType,
    string OperatorMode,
    bool IsRequired,
    string? SourceCatalog,
    string? Description);

public sealed record DashboardPurposeMetadataDto(
    string Code,
    string Label,
    string Description,
    IReadOnlyList<string> RecommendedDimensions,
    IReadOnlyList<string> RecommendedMeasures,
    IReadOnlyList<string> RecommendedChartTypes);

public sealed record DashboardCompatibilityRuleDto(
    string DimensionCode,
    string MeasureCode,
    IReadOnlyList<string> AllowedChartTypes,
    bool RequiresParameterCode,
    string? WarningMessage);

public sealed record DashboardQuerySafetyLimitsDto(
    int DefaultMaxRows,
    int AbsoluteMaxRows,
    int DefaultRawRowLimit,
    int AbsoluteRawRowLimit,
    int DefaultLookbackDays,
    int AbsoluteLookbackDays);

public sealed record DashboardWidgetQueryDto(
    string? WidgetType,
    string? ChartType,
    string? DimensionCode,
    string? MeasureCode,
    string? ParameterCode,
    DashboardWidgetFiltersDto? Filters,
    DashboardWidgetQueryOptionsDto? Options);

public sealed record DashboardWidgetFiltersDto(
    Guid? SiteId,
    Guid? AreaId,
    Guid? EquipmentId,
    string? MaterialCode,
    string? MaterialUnitType,
    string? SourceSystem,
    string? DefectType,
    string? RiskClass,
    string? ShiftCode,
    string? ParameterCode,
    DateTime? FromUtc,
    DateTime? ToUtc);
    
public sealed record DashboardWidgetQueryOptionsDto(
    int? MaxRows,
    int? RawRowLimit,
    string? SortDirection,
    bool? IncludeWarnings);

public sealed record DashboardWidgetQueryResultDto(
    DateTime GeneratedAtUtc,
    DashboardWidgetResolvedDto Widget,
    IReadOnlyList<DashboardWidgetColumnDto> Columns,
    IReadOnlyList<IDictionary<string, object?>> Rows,
    IReadOnlyList<string> Warnings);

public sealed record DashboardWidgetResolvedDto(
    string WidgetType,
    string ChartType,
    string? DimensionCode,
    string MeasureCode,
    string? ParameterCode,
    int MaxRows,
    int RawRowLimit,
    string SortDirection,
    DateTime? FromUtc,
    DateTime? ToUtc);

public sealed record DashboardWidgetColumnDto(
    string Code,
    string Label,
    string DataType);

public sealed record DashboardWidgetValidationResultDto(
    bool IsValid,
    IReadOnlyDictionary<string, string[]> Errors,
    IReadOnlyList<string> Warnings,
    DashboardWidgetResolvedDto? ResolvedWidget);

public static class DashboardMetadataCodes
{
    public static class WidgetTypes
    {
        public const string Kpi = "kpi";
        public const string Chart = "chart";
        public const string Table = "table";
    }

    public static class Purposes
    {
        public const string Quality = "quality";
        public const string Productivity = "productivity";
        public const string Downtime = "downtime";
        public const string Risk = "risk";
        public const string MaterialInvestigation = "materialInvestigation";
        public const string DataQuality = "dataQuality";
    }

    public static class ChartTypes
    {
        public const string Kpi = "kpi";
        public const string Bar = "bar";
        public const string Line = "line";
        public const string Area = "area";
        public const string Pie = "pie";
        public const string Donut = "donut";
        public const string Scatter = "scatter";
        public const string Heatmap = "heatmap";
        public const string Table = "table";
    }

    public static class Dimensions
    {
        public const string Site = "site";
        public const string Area = "area";
        public const string Equipment = "equipment";
        public const string SourceSystem = "sourceSystem";
        public const string MaterialUnitType = "materialUnitType";
        public const string ProductFamily = "productFamily";
        public const string GradeOrRecipe = "gradeOrRecipe";
        public const string ShiftCode = "shiftCode";
        public const string DefectType = "defectType";
        public const string ParameterCode = "parameterCode";
        public const string Day = "day";
        public const string Week = "week";
        public const string Month = "month";
        public const string RiskClass = "riskClass";
    }

    public static class Measures
    {
        public const string MaterialCount = "materialCount";
        public const string DefectCount = "defectCount";
        public const string DefectRate = "defectRate";
        public const string AvgParameterValue = "avgParameterValue";
        public const string MaxParameterValue = "maxParameterValue";
        public const string MinParameterValue = "minParameterValue";
        public const string DowntimeMinutes = "downtimeMinutes";
        public const string RiskScore = "riskScore";
        public const string ProcessStepDuration = "processStepDuration";
        public const string DataQualityIssueCount = "dataQualityIssueCount";
    }
}