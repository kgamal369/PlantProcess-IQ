namespace PlantProcess.Application.Dashboarding.Contracts;

public sealed record DashboardMetadataDto(
    DateTime GeneratedAtUtc,
    IReadOnlyList<DashboardDimensionMetadataDto> Dimensions,
    IReadOnlyList<DashboardMeasureMetadataDto> Measures,
    IReadOnlyList<DashboardChartTypeMetadataDto> ChartTypes,
    IReadOnlyList<DashboardFilterMetadataDto> Filters,
    IReadOnlyList<DashboardPurposeMetadataDto> Purposes,
    IReadOnlyList<DashboardCompatibilityRuleDto> CompatibilityRules,
    IReadOnlyList<DashboardWidgetKindMetadataDto> WidgetKinds,
    DashboardQuerySafetyLimitsDto SafetyLimits);

/// PPIQ T-041. THE STRUCTURAL WIDGET-KIND GRAMMAR.
///
/// Chapter 4 closes this list at seven. A kind is what a widget IS on the page.
/// It is NOT the chart type - Bar and Line are chart types under the Chart kind -
/// and it is NOT the filter variant - List and Date Range are variants under the
/// Filter kind. Mixing the two levels is what the retiring Page Builder union
/// did, and it is why a picker could offer "line" beside "filter-date" as if
/// they were the same sort of choice.
///
/// FIXED PRODUCT GRAMMAR. There is no registry, no plugin seam and no extension
/// path: a customer supplies dimensions, measures, filters and data, never a
/// structural kind.
public sealed record DashboardWidgetKindMetadataDto(
    string Code,
    string Label,
    bool UsesChartType,
    bool UsesQuery,
    string Description);

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

/// T-046. Availability is a PRODUCT fact carried beside the type, not a filter
/// applied before the client sees it. The authoring surface shows what the
/// product HAS and offers what it can draw, which are two different lists, and
/// a client that only receives the second cannot explain the difference.
public sealed record DashboardChartTypeMetadataDto(
    string Code,
    string Label,
    string Category,
    bool SupportsDimension,
    bool SupportsMeasure,
    bool SupportsMultipleSeries,
    bool SupportsParameterSelection,
    string Availability,
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

/// T-046. A refusal must be able to SAY WHY. The previous shape carried only the
/// allowed list, so a type that did not appear was indistinguishable from a type
/// that had never existed, and an author could not tell a modelling mistake from
/// a missing renderer.
public sealed record DashboardChartRefusalDto(
    string ChartTypeCode,
    string Reason);

public sealed record DashboardCompatibilityRuleDto(
    string DimensionCode,
    string MeasureCode,
    IReadOnlyList<string> AllowedChartTypes,
    IReadOnlyList<DashboardChartRefusalDto> RefusedChartTypes,
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

    /// PPIQ T-041. The seven structural kinds, closed.
    ///
    /// Kpi, Chart and Table REUSE the codes WidgetTypes already shipped, because
    /// a shipped code is renamed by a migration and not by a new feature. They
    /// sit in their own namespace because ChartTypes also declares "kpi" and
    /// "table": the same token means a chart type there and a structural kind
    /// here, and reading one list as the other would be a silent category error.
    public static class WidgetKinds
    {
        public const string Chart = "chart";
        public const string Table = "table";
        public const string Kpi = "kpi";
        public const string CalculatedLabel = "calculated-label";
        public const string Filter = "filter";
        public const string Container = "container";
        public const string Text = "text";
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
        public const string Pareto = "pareto";

        // T-046. Chapter 4 5.1.5 defines the product grammar as SEVENTEEN chart
        // types. Declaring only the ten with renderers showed a customer a
        // smaller product than the one they receive. Availability is carried in
        // DashboardChartGrammar, not here: a code is what the product HAS, and
        // whether it can be drawn today is a separate fact.
        public const string StackedColumn = "stackedColumn";
        public const string Combo = "combo";
        public const string BoxPlot = "boxPlot";
        public const string Histogram = "histogram";
        public const string Gauge = "gauge";
        public const string Waterfall = "waterfall";
        public const string PivotTable = "pivotTable";
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
        public const string ObservationCount = "observationCount";
        public const string DefectRate = "defectRate";
        public const string AvgParameterValue = "avgParameterValue";
        public const string MaxParameterValue = "maxParameterValue";
        public const string MinParameterValue = "minParameterValue";
        public const string DowntimeMinutes = "downtimeMinutes";
        public const string RiskScore = "riskScore";
        public const string ProcessStepDuration = "processStepDuration";
        public const string DataQualityIssueCount = "dataQualityIssueCount";

        // T-045 Pack B. Class-2 native-rich measures. They declare their own
        // columns and rows and never project into WidgetFact. The measure code
        // is the only discriminator the engine uses.
        public const string FindingStatus = "findingStatus";
        public const string ScoringCoverage = "scoringCoverage";
        public const string AnalysisReadiness = "analysisReadiness";

        // T-047 Pack A. Class-2 native distribution measures.
        //
        // A SOURCE IS A SEMANTIC QUESTION, NOT A CHART. These two publish the
        // same renderer-facing column roles and are drawn by the same
        // histogram, but they are different populations asked different
        // questions, and neither is named after the visual.
        public const string ParameterValueDistribution = "parameterValueDistribution";
        public const string RiskScoreDistribution = "riskScoreDistribution";

        // T-047 Pack B. The question is DISPERSION: how does this parameter's
        // value spread differ between the groups of a chosen dimension? A box
        // plot is one way to draw that answer and not the only one, so the
        // measure is not named after it.
        public const string ParameterValueSpread = "parameterValueSpread";

        // T-047 Pack C2. The question is ASSOCIATION between two process
        // variables over the materials where both were measured. A scatter is
        // one way to draw it; a hexbin or a regression band would be another.
        public const string ParameterRelationship = "parameterRelationship";

        // T-047 Pack D. Two DIFFERENT questions that happen to share a shape.
        // Both publish category / series / value, and one stacked renderer
        // draws either - which is why neither is called a stack.
        public const string MaterialThroughputByShift = "materialThroughputByShift";
        public const string DefectTypeMix = "defectTypeMix";
    }
}




