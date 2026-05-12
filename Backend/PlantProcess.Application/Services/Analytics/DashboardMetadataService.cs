using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.Analytics;

namespace PlantProcess.Application.Services.Analytics;

public sealed class DashboardMetadataService : IDashboardMetadataService
{
    private readonly IPlantProcessDbContext _dbContext;

    public DashboardMetadataService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ApplicationResult<DashboardMetadataDto>> GetMetadataAsync(
        CancellationToken cancellationToken)
    {
        var chartTypes = BuildChartTypes();
        var dimensions = BuildDimensions();
        var measures = BuildMeasures();
        var filters = BuildFilters();
        var purposes = BuildPurposes();
        var compatibilityRules = BuildCompatibilityRules(dimensions, measures);

        var metadata = new DashboardMetadataDto(
            GeneratedAtUtc: DateTime.UtcNow,
            Dimensions: dimensions,
            Measures: measures,
            ChartTypes: chartTypes,
            Filters: filters,
            Purposes: purposes,
            CompatibilityRules: compatibilityRules,
            SafetyLimits: DashboardWidgetQuerySafetyRegistry.BuildLimitsDto());

        return Task.FromResult(ApplicationResult<DashboardMetadataDto>.Success(metadata));
    }

    private static IReadOnlyList<DashboardChartTypeMetadataDto> BuildChartTypes()
    {
        return new[]
        {
            new DashboardChartTypeMetadataDto(
                DashboardMetadataCodes.ChartTypes.Kpi,
                "KPI",
                "Summary",
                SupportsDimension: false,
                SupportsMeasure: true,
                SupportsMultipleSeries: false,
                SupportsParameterSelection: true,
                "Single-number metric for quality, risk, productivity, downtime or data readiness."),

            new DashboardChartTypeMetadataDto(
                DashboardMetadataCodes.ChartTypes.Bar,
                "Bar",
                "Comparison",
                SupportsDimension: true,
                SupportsMeasure: true,
                SupportsMultipleSeries: false,
                SupportsParameterSelection: true,
                "Compare categories such as equipment, defect type, shift, source system or product family."),

            new DashboardChartTypeMetadataDto(
                DashboardMetadataCodes.ChartTypes.Line,
                "Line",
                "Trend",
                SupportsDimension: true,
                SupportsMeasure: true,
                SupportsMultipleSeries: true,
                SupportsParameterSelection: true,
                "Analyze performance or quality movement over day, week or month."),

            new DashboardChartTypeMetadataDto(
                DashboardMetadataCodes.ChartTypes.Area,
                "Area",
                "Trend",
                SupportsDimension: true,
                SupportsMeasure: true,
                SupportsMultipleSeries: true,
                SupportsParameterSelection: true,
                "Show cumulative or volume-style movement over time."),

            new DashboardChartTypeMetadataDto(
                DashboardMetadataCodes.ChartTypes.Pie,
                "Pie",
                "Share",
                SupportsDimension: true,
                SupportsMeasure: true,
                SupportsMultipleSeries: false,
                SupportsParameterSelection: false,
                "Show simple distribution across a small number of categories."),

            new DashboardChartTypeMetadataDto(
                DashboardMetadataCodes.ChartTypes.Donut,
                "Donut",
                "Share",
                SupportsDimension: true,
                SupportsMeasure: true,
                SupportsMultipleSeries: false,
                SupportsParameterSelection: false,
                "Show distribution for risk class, decision, source system or defect family."),

            new DashboardChartTypeMetadataDto(
                DashboardMetadataCodes.ChartTypes.Scatter,
                "Scatter",
                "Correlation",
                SupportsDimension: true,
                SupportsMeasure: true,
                SupportsMultipleSeries: false,
                SupportsParameterSelection: true,
                "Explore relationships between process parameters, risk and defect behavior."),

            new DashboardChartTypeMetadataDto(
                DashboardMetadataCodes.ChartTypes.Heatmap,
                "Heatmap",
                "Matrix",
                SupportsDimension: true,
                SupportsMeasure: true,
                SupportsMultipleSeries: true,
                SupportsParameterSelection: true,
                "Analyze concentration patterns by equipment, shift, defect, source or time bucket."),

            new DashboardChartTypeMetadataDto(
                DashboardMetadataCodes.ChartTypes.Table,
                "Table",
                "Detail",
                SupportsDimension: true,
                SupportsMeasure: true,
                SupportsMultipleSeries: true,
                SupportsParameterSelection: true,
                "Show sortable aggregated result rows.")
        };
    }

    private static IReadOnlyList<DashboardDimensionMetadataDto> BuildDimensions()
    {
        return new[]
        {
            Dimension(
                DashboardMetadataCodes.Dimensions.Site,
                "Site",
                "Plant",
                "string",
                false,
                new[] { "bar", "pie", "donut", "table" },
                "Manufacturing site or plant."),

            Dimension(
                DashboardMetadataCodes.Dimensions.Area,
                "Area",
                "Plant",
                "string",
                false,
                new[] { "bar", "pie", "donut", "heatmap", "table" },
                "Flexible plant area or location layer."),

            Dimension(
                DashboardMetadataCodes.Dimensions.Equipment,
                "Equipment",
                "Plant",
                "string",
                false,
                new[] { "bar", "pie", "donut", "scatter", "heatmap", "table" },
                "Machine, line, station, asset or tool."),

            Dimension(
                DashboardMetadataCodes.Dimensions.SourceSystem,
                "Source System",
                "Integration",
                "string",
                false,
                new[] { "bar", "pie", "donut", "table" },
                "MES, Level 2, lab, inspection, ERP, file or API source."),

            Dimension(
                DashboardMetadataCodes.Dimensions.MaterialUnitType,
                "Material Unit Type",
                "Material",
                "string",
                false,
                new[] { "bar", "pie", "donut", "table" },
                "Generic material type such as batch, slab, coil, lot, tire, roll or component."),

            Dimension(
                DashboardMetadataCodes.Dimensions.ProductFamily,
                "Product Family",
                "Material",
                "string",
                false,
                new[] { "bar", "pie", "donut", "table" },
                "Product family, product group or manufacturing family."),

            Dimension(
                DashboardMetadataCodes.Dimensions.GradeOrRecipe,
                "Grade / Recipe",
                "Material",
                "string",
                false,
                new[] { "bar", "pie", "donut", "table" },
                "Grade, recipe, product code or process recipe."),

            Dimension(
                DashboardMetadataCodes.Dimensions.ShiftCode,
                "Shift / Crew",
                "Operations",
                "string",
                false,
                new[] { "bar", "pie", "donut", "heatmap", "table" },
                "Operational shift or crew code."),

            Dimension(
                DashboardMetadataCodes.Dimensions.DefectType,
                "Defect Type",
                "Quality",
                "string",
                false,
                new[] { "bar", "pie", "donut", "heatmap", "table" },
                "Standardized defect or quality event type."),

            Dimension(
                DashboardMetadataCodes.Dimensions.ParameterCode,
                "Parameter",
                "Process",
                "string",
                true,
                new[] { "bar", "line", "scatter", "heatmap", "table" },
                "Process parameter code."),

            Dimension(
                DashboardMetadataCodes.Dimensions.Day,
                "Day",
                "Time",
                "date",
                false,
                new[] { "bar", "line", "area", "table" },
                "Calendar day bucket."),

            Dimension(
                DashboardMetadataCodes.Dimensions.Week,
                "Week",
                "Time",
                "date",
                false,
                new[] { "bar", "line", "area", "table" },
                "Calendar week bucket."),

            Dimension(
                DashboardMetadataCodes.Dimensions.Month,
                "Month",
                "Time",
                "date",
                false,
                new[] { "bar", "line", "area", "table" },
                "Calendar month bucket."),

            Dimension(
                DashboardMetadataCodes.Dimensions.RiskClass,
                "Risk Class",
                "Risk",
                "string",
                false,
                new[] { "bar", "pie", "donut", "table" },
                "Low, medium, high or critical risk classification.")
        };

        static DashboardDimensionMetadataDto Dimension(
            string code,
            string label,
            string category,
            string dataType,
            bool requiresParameterCode,
            IReadOnlyList<string> compatibleCharts,
            string description)
        {
            return new DashboardDimensionMetadataDto(
                code,
                label,
                category,
                dataType,
                requiresParameterCode,
                compatibleCharts,
                description);
        }
    }

    private static IReadOnlyList<DashboardMeasureMetadataDto> BuildMeasures()
    {
        return new[]
        {
            Measure(
                DashboardMetadataCodes.Measures.MaterialCount,
                "Material Count",
                "Production",
                "count",
                "materials",
                false,
                new[] { "kpi", "bar", "line", "area", "pie", "donut", "table" },
                "Number of traceable materials or batches."),

            Measure(
                DashboardMetadataCodes.Measures.DefectCount,
                "Defect Count",
                "Quality",
                "count",
                "defects",
                false,
                new[] { "kpi", "bar", "line", "area", "pie", "donut", "heatmap", "table" },
                "Number of defect or quality issue events."),

            Measure(
                DashboardMetadataCodes.Measures.DefectRate,
                "Defect Rate",
                "Quality",
                "ratio",
                "%",
                false,
                new[] { "kpi", "bar", "line", "area", "scatter", "heatmap", "table" },
                "Defective material count divided by material count."),

            Measure(
                DashboardMetadataCodes.Measures.AvgParameterValue,
                "Average Parameter Value",
                "Process",
                "avg",
                null,
                true,
                new[] { "kpi", "bar", "line", "area", "scatter", "heatmap", "table" },
                "Average numeric value for a selected parameter."),

            Measure(
                DashboardMetadataCodes.Measures.MaxParameterValue,
                "Maximum Parameter Value",
                "Process",
                "max",
                null,
                true,
                new[] { "kpi", "bar", "line", "area", "table" },
                "Maximum numeric value for a selected parameter."),

            Measure(
                DashboardMetadataCodes.Measures.MinParameterValue,
                "Minimum Parameter Value",
                "Process",
                "min",
                null,
                true,
                new[] { "kpi", "bar", "line", "area", "table" },
                "Minimum numeric value for a selected parameter."),

            Measure(
                DashboardMetadataCodes.Measures.DowntimeMinutes,
                "Downtime Minutes",
                "Operations",
                "sum",
                "minutes",
                false,
                new[] { "kpi", "bar", "line", "area", "heatmap", "table" },
                "Total downtime duration in minutes."),

            Measure(
                DashboardMetadataCodes.Measures.RiskScore,
                "Average Risk Score",
                "Risk",
                "avg",
                "0-1",
                false,
                new[] { "kpi", "bar", "line", "area", "scatter", "heatmap", "table" },
                "Average quality risk score."),

            Measure(
                DashboardMetadataCodes.Measures.ProcessStepDuration,
                "Process Step Duration",
                "Operations",
                "avg",
                "minutes",
                false,
                new[] { "kpi", "bar", "line", "area", "heatmap", "table" },
                "Average process step duration."),

            Measure(
                DashboardMetadataCodes.Measures.DataQualityIssueCount,
                "Data Quality Issue Count",
                "Data Quality",
                "count",
                "issues",
                false,
                new[] { "kpi", "bar", "line", "area", "pie", "donut", "table" },
                "Number of detected data-quality issues.")
        };

        static DashboardMeasureMetadataDto Measure(
            string code,
            string label,
            string category,
            string aggregation,
            string? unit,
            bool requiresParameterCode,
            IReadOnlyList<string> compatibleCharts,
            string description)
        {
            return new DashboardMeasureMetadataDto(
                code,
                label,
                category,
                aggregation,
                unit,
                requiresParameterCode,
                compatibleCharts,
                description);
        }
    }

    private static IReadOnlyList<DashboardFilterMetadataDto> BuildFilters()
    {
        return new[]
        {
            Filter("siteId", "Site", "Plant", "guid", "single", false, "sites", "Limit analysis to one site."),
            Filter("areaId", "Area", "Plant", "guid", "single", false, "areas", "Limit analysis to one area."),
            Filter("equipmentId", "Equipment", "Plant", "guid", "single", false, "equipment", "Limit analysis to one equipment asset."),
            Filter("materialCode", "Material Code", "Material", "string", "contains", false, null, "Search by material or batch code."),
            Filter("sourceSystem", "Source System", "Integration", "string", "single", false, "sourceSystems", "Limit analysis to one source system."),
            Filter("defectType", "Defect Type", "Quality", "string", "single", false, "defects", "Limit analysis to one defect type."),
            Filter("riskClass", "Risk Class", "Risk", "string", "single", false, "riskClasses", "Limit analysis to one risk class."),
            Filter("shiftCode", "Shift / Crew", "Operations", "string", "single", false, "shifts", "Limit analysis to one shift or crew."),
            Filter("parameterCode", "Parameter", "Process", "string", "single", false, "parameters", "Select parameter for parameter-based widgets."),
            Filter("fromUtc", "From UTC", "Time", "datetime", "range-start", false, null, "Start of analysis window."),
            Filter("toUtc", "To UTC", "Time", "datetime", "range-end", false, null, "End of analysis window.")
        };

        static DashboardFilterMetadataDto Filter(
            string code,
            string label,
            string category,
            string dataType,
            string operatorMode,
            bool isRequired,
            string? sourceCatalog,
            string description)
        {
            return new DashboardFilterMetadataDto(
                code,
                label,
                category,
                dataType,
                operatorMode,
                isRequired,
                sourceCatalog,
                description);
        }
    }

    private static IReadOnlyList<DashboardPurposeMetadataDto> BuildPurposes()
    {
        return new[]
        {
            Purpose(
                DashboardMetadataCodes.Purposes.Quality,
                "Quality",
                "Analyze defect count, defect rate and quality distribution.",
                new[] { "day", "defectType", "equipment", "shiftCode" },
                new[] { "defectCount", "defectRate", "materialCount" },
                new[] { "kpi", "bar", "line", "donut", "heatmap", "table" }),

            Purpose(
                DashboardMetadataCodes.Purposes.Productivity,
                "Productivity",
                "Analyze material volume, process duration and production trends.",
                new[] { "day", "equipment", "shiftCode", "materialUnitType" },
                new[] { "materialCount", "processStepDuration" },
                new[] { "kpi", "bar", "line", "area", "table" }),

            Purpose(
                DashboardMetadataCodes.Purposes.Downtime,
                "Downtime",
                "Analyze downtime duration by time, equipment, source or shift.",
                new[] { "day", "equipment", "shiftCode", "sourceSystem" },
                new[] { "downtimeMinutes" },
                new[] { "kpi", "bar", "line", "heatmap", "table" }),

            Purpose(
                DashboardMetadataCodes.Purposes.Risk,
                "Risk",
                "Analyze risk score, risk class distribution and high-risk patterns.",
                new[] { "riskClass", "day", "equipment", "productFamily" },
                new[] { "riskScore", "materialCount" },
                new[] { "kpi", "bar", "line", "donut", "scatter", "table" }),

            Purpose(
                DashboardMetadataCodes.Purposes.MaterialInvestigation,
                "Material Investigation",
                "Analyze one material or batch by process, risk, quality and source context.",
                new[] { "equipment", "parameterCode", "defectType", "sourceSystem" },
                new[] { "avgParameterValue", "defectCount", "riskScore" },
                new[] { "bar", "line", "scatter", "table" }),

            Purpose(
                DashboardMetadataCodes.Purposes.DataQuality,
                "Data Quality",
                "Analyze missing, inconsistent, duplicate or suspicious source data.",
                new[] { "sourceSystem", "day", "equipment" },
                new[] { "dataQualityIssueCount" },
                new[] { "kpi", "bar", "line", "donut", "table" })
        };

        static DashboardPurposeMetadataDto Purpose(
            string code,
            string label,
            string description,
            IReadOnlyList<string> recommendedDimensions,
            IReadOnlyList<string> recommendedMeasures,
            IReadOnlyList<string> recommendedChartTypes)
        {
            return new DashboardPurposeMetadataDto(
                code,
                label,
                description,
                recommendedDimensions,
                recommendedMeasures,
                recommendedChartTypes);
        }
    }

    private static IReadOnlyList<DashboardCompatibilityRuleDto> BuildCompatibilityRules(
        IReadOnlyList<DashboardDimensionMetadataDto> dimensions,
        IReadOnlyList<DashboardMeasureMetadataDto> measures)
    {
        var rules = new List<DashboardCompatibilityRuleDto>();

        foreach (var dimension in dimensions)
        {
            foreach (var measure in measures)
            {
                var allowedCharts = dimension.CompatibleChartTypes
                    .Intersect(measure.CompatibleChartTypes, StringComparer.OrdinalIgnoreCase)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (allowedCharts.Count == 0)
                    continue;

                var requiresParameter = dimension.RequiresParameterCode || measure.RequiresParameterCode;

                rules.Add(new DashboardCompatibilityRuleDto(
                    dimension.Code,
                    measure.Code,
                    allowedCharts,
                    requiresParameter,
                    requiresParameter ? "This combination requires a selected parameter code." : null));
            }
        }

        return rules;
    }
}