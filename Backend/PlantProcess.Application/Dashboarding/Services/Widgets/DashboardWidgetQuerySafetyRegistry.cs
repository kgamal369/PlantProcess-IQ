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
        DashboardMetadataCodes.ChartTypes.Kpi,
        DashboardMetadataCodes.ChartTypes.Bar,
        DashboardMetadataCodes.ChartTypes.Line,
        DashboardMetadataCodes.ChartTypes.Area,
        DashboardMetadataCodes.ChartTypes.Pie,
        DashboardMetadataCodes.ChartTypes.Donut,
        DashboardMetadataCodes.ChartTypes.Scatter,
        DashboardMetadataCodes.ChartTypes.Heatmap,
        DashboardMetadataCodes.ChartTypes.Table
    };

    private static readonly HashSet<string> SupportedDimensions = new(StringComparer.OrdinalIgnoreCase)
    {
        DashboardMetadataCodes.Dimensions.Site,
        DashboardMetadataCodes.Dimensions.Area,
        DashboardMetadataCodes.Dimensions.Equipment,
        DashboardMetadataCodes.Dimensions.SourceSystem,
        DashboardMetadataCodes.Dimensions.MaterialUnitType,
        DashboardMetadataCodes.Dimensions.ProductFamily,
        DashboardMetadataCodes.Dimensions.GradeOrRecipe,
        DashboardMetadataCodes.Dimensions.ShiftCode,
        DashboardMetadataCodes.Dimensions.DefectType,
        DashboardMetadataCodes.Dimensions.ParameterCode,
        DashboardMetadataCodes.Dimensions.Day,
        DashboardMetadataCodes.Dimensions.Week,
        DashboardMetadataCodes.Dimensions.Month,
        DashboardMetadataCodes.Dimensions.RiskClass
    };

    private static readonly HashSet<string> SupportedMeasures = new(StringComparer.OrdinalIgnoreCase)
    {
        DashboardMetadataCodes.Measures.MaterialCount,
        DashboardMetadataCodes.Measures.DefectCount,
        DashboardMetadataCodes.Measures.DefectRate,
        DashboardMetadataCodes.Measures.AvgParameterValue,
        DashboardMetadataCodes.Measures.MaxParameterValue,
        DashboardMetadataCodes.Measures.MinParameterValue,
        DashboardMetadataCodes.Measures.DowntimeMinutes,
        DashboardMetadataCodes.Measures.RiskScore,
        DashboardMetadataCodes.Measures.ProcessStepDuration,
        DashboardMetadataCodes.Measures.DataQualityIssueCount
    };

    private static readonly HashSet<string> MeasuresRequiringParameter = new(StringComparer.OrdinalIgnoreCase)
    {
        DashboardMetadataCodes.Measures.AvgParameterValue,
        DashboardMetadataCodes.Measures.MaxParameterValue,
        DashboardMetadataCodes.Measures.MinParameterValue
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
        return !string.IsNullOrWhiteSpace(dimensionCode) &&
               SupportedDimensions.Contains(dimensionCode.Trim());
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
                or DashboardMetadataCodes.Measures.DefectRate;
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



