using PlantProcess.Application.Dashboarding.Contracts;

namespace PlantProcess.Application.Dashboarding.Services.Widgets;

public static class DashboardWidgetQuerySafetyRegistry
{
    public const int DefaultMaxRows = 100;
    public const int AbsoluteMaxRows = 500;

    public const int DefaultRawRowLimit = 50_000;
    public const int AbsoluteRawRowLimit = 250_000;

    public const int DefaultLookbackDays = 90;
    public const int AbsoluteLookbackDays = 730;

    private static readonly HashSet<string> SupportedWidgetTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        DashboardMetadataCodes.WidgetTypes.Kpi,
        DashboardMetadataCodes.WidgetTypes.Chart,
        DashboardMetadataCodes.WidgetTypes.Table
    };

    private static readonly HashSet<string> SupportedChartTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // T-046-R1. Heatmap was already accepted here; Combo was not, so a
        // seeded paired widget would have been rejected by the validator
        // before any grammar question was reached.
        DashboardMetadataCodes.ChartTypes.Combo,
        DashboardMetadataCodes.ChartTypes.Kpi,
        DashboardMetadataCodes.ChartTypes.Bar,
        DashboardMetadataCodes.ChartTypes.Line,
        DashboardMetadataCodes.ChartTypes.Area,
        DashboardMetadataCodes.ChartTypes.Pie,
        DashboardMetadataCodes.ChartTypes.Donut,
        DashboardMetadataCodes.ChartTypes.Scatter,
        DashboardMetadataCodes.ChartTypes.Heatmap,
        DashboardMetadataCodes.ChartTypes.Pareto,
        DashboardMetadataCodes.ChartTypes.Table,

        // T-047 Pack A. MEASURED GATE, 13-Aug: this set still held the
        // pre-T-046 ten. A seeded histogram widget was rejected here, by the
        // validator, before any grammar or availability question was reached.
        // Flipping availability alone would have produced a chart the product
        // claims to draw and the validator refuses to accept.
        DashboardMetadataCodes.ChartTypes.Histogram,
        DashboardMetadataCodes.ChartTypes.BoxPlot,
        DashboardMetadataCodes.ChartTypes.StackedColumn
    };

    // T-046 Pack 3A. The second dimension catalogue lived here: the same
    // fourteen codes the metadata service already described, with none of their
    // meaning. The service that decided whether a dimension EXISTED could not
    // say what it MEANT. DashboardDimensionRegistry is the single authority now.

    private static readonly HashSet<string> SupportedMeasures = new(StringComparer.OrdinalIgnoreCase)
    {
        DashboardMetadataCodes.Measures.DefectPositionDensity,
        DashboardMetadataCodes.Measures.SpecificationLimits,
        DashboardMetadataCodes.Measures.RiskScoringProvenance,
        DashboardMetadataCodes.Measures.RiskScoreContributions,
        DashboardMetadataCodes.Measures.RiskScoreHistory,
        DashboardMetadataCodes.Measures.EquipmentStoppageAndImpact,
        DashboardMetadataCodes.Measures.ParameterValueSpread,
        DashboardMetadataCodes.Measures.ParameterRelationship,
        DashboardMetadataCodes.Measures.MaterialThroughputByShift,
        DashboardMetadataCodes.Measures.DefectTypeMix,
        DashboardMetadataCodes.Measures.MaterialCount,
        DashboardMetadataCodes.Measures.DefectCount,
        DashboardMetadataCodes.Measures.ObservationCount,
        DashboardMetadataCodes.Measures.DefectRate,
        DashboardMetadataCodes.Measures.AvgParameterValue,
        DashboardMetadataCodes.Measures.MaxParameterValue,
        DashboardMetadataCodes.Measures.MinParameterValue,
        DashboardMetadataCodes.Measures.DowntimeMinutes,
        DashboardMetadataCodes.Measures.RiskScore,
        DashboardMetadataCodes.Measures.ProcessStepDuration,
        DashboardMetadataCodes.Measures.DataQualityIssueCount,
        DashboardMetadataCodes.Measures.FindingStatus,
        DashboardMetadataCodes.Measures.ScoringCoverage,
        DashboardMetadataCodes.Measures.AnalysisReadiness,
        DashboardMetadataCodes.Measures.ParameterValueDistribution,
        DashboardMetadataCodes.Measures.RiskScoreDistribution
    };

    private static readonly HashSet<string> MeasuresRequiringParameter = new(StringComparer.OrdinalIgnoreCase)
    {
        // T-047 Pack B. Spreading "some parameter" across every parameter in
        // the plant mixes units, exactly as averaging them would.
        DashboardMetadataCodes.Measures.ParameterValueSpread,
        DashboardMetadataCodes.Measures.ParameterRelationship,
        DashboardMetadataCodes.Measures.AvgParameterValue,
        DashboardMetadataCodes.Measures.MaxParameterValue,
        DashboardMetadataCodes.Measures.MinParameterValue,

        // T-045 Pack B. The analysis target travels on the existing parameter
        // carrier: a readiness widget names the outcome it reports on, and the
        // grain comes from that outcome's governed definition rather than from
        // the widget.
        DashboardMetadataCodes.Measures.AnalysisReadiness,

        // T-047 Pack A. Distributing "some parameter" across every parameter in
        // the plant mixes units and is not a question. The parameter travels on
        // the existing carrier, exactly as the parameter aggregates do.
        DashboardMetadataCodes.Measures.ParameterValueDistribution
    };

    // T-045 Pack B. Measures whose source declares its own columns and rows.
    // The validator asks this general question instead of naming a measure, so
    // a future native source is a registry entry and not a validator edit.
    private static readonly HashSet<string> MeasuresProvidingOwnColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        DashboardMetadataCodes.Measures.DefectPositionDensity,
        DashboardMetadataCodes.Measures.SpecificationLimits,
        DashboardMetadataCodes.Measures.RiskScoringProvenance,
        DashboardMetadataCodes.Measures.RiskScoreContributions,
        DashboardMetadataCodes.Measures.RiskScoreHistory,
        DashboardMetadataCodes.Measures.EquipmentStoppageAndImpact,
        DashboardMetadataCodes.Measures.ParameterValueSpread,
        DashboardMetadataCodes.Measures.ParameterRelationship,
        DashboardMetadataCodes.Measures.MaterialThroughputByShift,
        DashboardMetadataCodes.Measures.DefectTypeMix,
        DashboardMetadataCodes.Measures.FindingStatus,
        DashboardMetadataCodes.Measures.ScoringCoverage,
        DashboardMetadataCodes.Measures.AnalysisReadiness,
        DashboardMetadataCodes.Measures.ParameterValueDistribution,
        DashboardMetadataCodes.Measures.RiskScoreDistribution
    };

    public static bool IsSupportedWidgetType(string? widgetType)
    {
        return !string.IsNullOrWhiteSpace(widgetType) &&
               SupportedWidgetTypes.Contains(widgetType.Trim());
    }

    public static bool IsSupportedChartType(string? chartType)
    {
        return !string.IsNullOrWhiteSpace(chartType) &&
               SupportedChartTypes.Contains(chartType.Trim());
    }

    public static bool IsSupportedDimension(string? dimensionCode)
    {
        return DashboardDimensionRegistry.IsRegistered(dimensionCode);
    }

    public static bool IsSupportedMeasure(string? measureCode)
    {
        return !string.IsNullOrWhiteSpace(measureCode) &&
               SupportedMeasures.Contains(measureCode.Trim());
    }

    public static bool MeasureRequiresParameterCode(string? measureCode)
    {
        return !string.IsNullOrWhiteSpace(measureCode) &&
               MeasuresRequiringParameter.Contains(measureCode.Trim());
    }

    /// <summary>
    /// True when the measure's result source supplies its own column set. Such
    /// a measure is not grouped by a BI dimension, so requiring one would
    /// reject a valid widget; and it never reaches the aggregate executor, so
    /// no merge rule can be applied to it.
    /// </summary>
    public static bool MeasureProvidesOwnColumns(string? measureCode)
    {
        return !string.IsNullOrWhiteSpace(measureCode) &&
               MeasuresProvidingOwnColumns.Contains(measureCode.Trim());
    }

    public static bool ChartRequiresDimension(string? chartType)
    {
        return !string.Equals(chartType, DashboardMetadataCodes.ChartTypes.Kpi, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsChartCompatibleWithMeasure(string chartType, string measureCode)
    {
        if (string.Equals(chartType, DashboardMetadataCodes.ChartTypes.Kpi, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(chartType, DashboardMetadataCodes.ChartTypes.Scatter, StringComparison.OrdinalIgnoreCase))
        {
            return measureCode is DashboardMetadataCodes.Measures.AvgParameterValue
                or DashboardMetadataCodes.Measures.RiskScore
                or DashboardMetadataCodes.Measures.DefectRate
                // T-047 Pack C2. The only measure that publishes two numeric
                // axes for the same population. Without this the validator
                // refuses the query before the source is reached.
                or DashboardMetadataCodes.Measures.ParameterRelationship;
        }

        return true;
    }

    public static int ClampMaxRows(int? requested)
    {
        return Math.Clamp(requested ?? DefaultMaxRows, 1, AbsoluteMaxRows);
    }

    public static int ClampRawRowLimit(int? requested)
    {
        return Math.Clamp(requested ?? DefaultRawRowLimit, 1, AbsoluteRawRowLimit);
    }

    public static string NormalizeSortDirection(string? sortDirection)
    {
        return string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase)
            ? "asc"
            : "desc";
    }

    public static DashboardQuerySafetyLimitsDto BuildLimitsDto()
    {
        return new DashboardQuerySafetyLimitsDto(
            DefaultMaxRows,
            AbsoluteMaxRows,
            DefaultRawRowLimit,
            AbsoluteRawRowLimit,
            DefaultLookbackDays,
            AbsoluteLookbackDays);
    }
}



