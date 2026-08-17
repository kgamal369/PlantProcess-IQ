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

/// <summary>
/// PR-050-01. Who is asking, in the installation's own vocabulary.
///
/// T-073 hashes the page and widget codes into the evidence fingerprint and
/// renders them into the evidence sentence, so an execution that wants evidence
/// must be able to name itself. It is optional on the request because an
/// ordinary render does not need evidence and must not be forced to invent an
/// identity to get a chart back.
/// </summary>
public sealed record DashboardWidgetExecutionIdentityDto(
    string? PageCode,
    string? WidgetCode,
    Guid? WidgetDefinitionId);

public sealed record DashboardWidgetQueryDto(
    string? WidgetType,
    string? ChartType,
    string? DimensionCode,
    string? MeasureCode,
    string? ParameterCode,
    DashboardWidgetFiltersDto? Filters,
    DashboardWidgetQueryOptionsDto? Options,
    DashboardWidgetExecutionIdentityDto? ExecutionIdentity = null);

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
    bool? IncludeWarnings,
    // PR-050-01. Evidence persistence is OPT-IN. An ordinary dashboard render
    // is a read: it must not turn a refresh, a filter move or an auto-refresh
    // into a row in the evidence store. Only a caller that actually wants
    // evidence - a drill-down - asks for it.
    bool? IncludeExecutionEvidence = null);

/// <summary>
/// PR-050-01. The wire shape of the EXISTING provenance handle.
///
/// This is not a second provenance system and not a dashboard-local evidence
/// type. ProvenanceHandle carries its kind as an enum; every surface that
/// already emits a handle emits Kind.ToString(), and the frontend type has
/// always been { kind, id, detail? }. This record states that shape once
/// instead of a ninth anonymous object.
/// </summary>
public sealed record ProvenanceHandleRefDto(
    string Kind,
    string Id,
    string? Detail = null);

/// <summary>
/// PR-050-01. What ONE returned row represents - the population predicate
/// behind the point, not the point's current number.
///
/// RowFingerprint is derived from semantic identity only: effective filter
/// context, dimension bindings and values, measure and parameter. It
/// deliberately excludes the aggregate value, the rendered label, the
/// generation time and the row's position, so re-ordering the same result
/// cannot invent new populations and a changed number cannot silently become a
/// changed population.
///
/// PopulationCount is null when the executing source cannot truthfully supply
/// one. It is NEVER the number of returned rows: five bars do not mean five of
/// anything.
/// </summary>
public sealed record DashboardWidgetRowPopulationDto(
    int RowIndex,
    string? RowFingerprint,
    IReadOnlyDictionary<string, string?> DimensionBindings,
    string MeasureCode,
    string? ParameterCode,
    string FilterContextFingerprint,
    int? PopulationCount);

public sealed record DashboardWidgetQueryResultDto(
    DateTime GeneratedAtUtc,
    DashboardWidgetResolvedDto Widget,
    IReadOnlyList<DashboardWidgetColumnDto> Columns,
    IReadOnlyList<IDictionary<string, object?>> Rows,
    IReadOnlyList<string> Warnings,
    // PR-050-01. Execution-level, never row-level. It identifies the exact
    // widget execution that produced these values. It is NOT physical row
    // lineage and no consumer may present it as such.
    ProvenanceHandleRefDto? ExecutionEvidenceHandle = null,
    // PR-050-01. One descriptor per returned row, same order as Rows. Additive
    // metadata beside the rows, never inside them, so chart data stays clean.
    IReadOnlyList<DashboardWidgetRowPopulationDto>? RowPopulations = null);

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

        // T-045-R1-D. Two independent canonical quantities reported side by
        // side. The question is what a stoppage COST, which is not the same as
        // how long it lasted.
        public const string EquipmentStoppageAndImpact = "equipmentStoppageAndImpact";

        // T-045-R1-C. Three questions about the SAME persisted population.
        // None of them scores anything; all three report what is already on
        // the row and refuse where the row says nothing.
        public const string RiskScoringProvenance = "riskScoringProvenance";
        public const string RiskScoreContributions = "riskScoreContributions";
        public const string RiskScoreHistory = "riskScoreHistory";

        // T-047 final. Two questions the pages could not ask until T-044-R1
        // materialised the facts behind them.
        public const string DefectPositionDensity = "defectPositionDensity";
        public const string SpecificationLimits = "specificationLimits";
    }
}




