using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Dashboarding.Interfaces;
using PlantProcess.Application.Dashboarding.Services.Widgets;

namespace PlantProcess.Application.Dashboarding.Services.Metadata;

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
        var widgetKinds = BuildWidgetKinds();

        var metadata = new DashboardMetadataDto(
            GeneratedAtUtc: DateTime.UtcNow,
            Dimensions: dimensions,
            Measures: measures,
            ChartTypes: chartTypes,
            Filters: filters,
            Purposes: purposes,
            CompatibilityRules: compatibilityRules,
            WidgetKinds: widgetKinds,
            SafetyLimits: DashboardWidgetQuerySafetyRegistry.BuildLimitsDto());

        return Task.FromResult(ApplicationResult<DashboardMetadataDto>.Success(metadata));
    }

    /// PPIQ T-041. Served from the ONE metadata endpoint the client already
    /// calls, so the Page Builder stops compiling its own allowed list.
    private static IReadOnlyList<DashboardWidgetKindMetadataDto> BuildWidgetKinds()
    {
        return new[]
        {
            new DashboardWidgetKindMetadataDto(
                DashboardMetadataCodes.WidgetKinds.Chart,
                "Chart",
                UsesChartType: true,
                UsesQuery: true,
                "Plots a query against a chart type chosen from the chart catalogue."),

            new DashboardWidgetKindMetadataDto(
                DashboardMetadataCodes.WidgetKinds.Table,
                "Table",
                UsesChartType: false,
                UsesQuery: true,
                "Shows the returned rows as rows, when the reader needs the values rather than the shape."),

            new DashboardWidgetKindMetadataDto(
                DashboardMetadataCodes.WidgetKinds.Kpi,
                "KPI",
                UsesChartType: false,
                UsesQuery: true,
                "One measured number, with the population it was measured over."),

            new DashboardWidgetKindMetadataDto(
                DashboardMetadataCodes.WidgetKinds.CalculatedLabel,
                "Calculated label",
                UsesChartType: false,
                UsesQuery: true,
                "A sentence whose values come from a query, so a caption cannot drift from the data."),

            new DashboardWidgetKindMetadataDto(
                DashboardMetadataCodes.WidgetKinds.Filter,
                "Filter",
                UsesChartType: false,
                UsesQuery: true,
                "Narrows the page. Its variant - list, dropdown, date range, numeric range, search or button group - is chosen inside the kind."),

            new DashboardWidgetKindMetadataDto(
                DashboardMetadataCodes.WidgetKinds.Container,
                "Container",
                UsesChartType: false,
                UsesQuery: false,
                "Groups other widgets so a page can be read in sections rather than as a wall."),

            new DashboardWidgetKindMetadataDto(
                DashboardMetadataCodes.WidgetKinds.Text,
                "Text",
                UsesChartType: false,
                UsesQuery: false,
                "Written context authored by a person. It states no measured value, so it can never disagree with one."),
        };
    }

    /// <summary>
    /// T-046. The chart catalogue is now READ FROM THE GRAMMAR, not written out
    /// here. Ten types were listed by hand; the product grammar is seventeen,
    /// and a hand-written list is how the tenth diverges from the eleventh.
    ///
    /// Availability travels with each type, so the authoring surface can show
    /// what the product HAS while offering only what it can draw today.
    /// Implementing a renderer later changes one field in the grammar and
    /// nothing here.
    /// </summary>
    private static IReadOnlyList<DashboardChartTypeMetadataDto> BuildChartTypes()
    {
        return DashboardChartGrammar.All
            .Select(definition => new DashboardChartTypeMetadataDto(
                definition.Code,
                definition.Label,
                definition.Category,
                definition.SupportsDimension,
                definition.SupportsMeasure,
                definition.SupportsMultipleSeries,
                definition.SupportsParameterSelection,
                definition.Availability == ChartAvailability.Implemented
                    ? AvailabilityImplemented
                    : AvailabilityNotYetAvailable,
                definition.Description))
            .ToList();
    }

    private const string AvailabilityImplemented = "implemented";
    private const string AvailabilityNotYetAvailable = "not-yet-available";

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

    /// <summary>
    /// T-046. COMPATIBILITY IS DERIVED, NOT CURATED.
    ///
    /// This previously intersected two hand-maintained arrays - one on each
    /// dimension, one on each measure - to produce 154 pairs from 25 curated
    /// lists. Curation is not a rule. Measured before this task, those lists
    /// offered a heatmap on a SINGLE categorical axis and a share chart over a
    /// dimension whose data holds one category, and they declared 'pareto'
    /// while listing it in no array at all, so nothing could select it.
    ///
    /// Every pair is now evaluated against the semantic grammar, and every type
    /// that is NOT offered carries the sentence saying why. An author who is
    /// told only that something is unavailable tries it again.
    ///
    /// THE SHAPE THIS BUILDER CAN DESCRIBE IS LIMITED BY THE QUERY CONTRACT,
    /// and that is reported rather than papered over. A widget query carries ONE
    /// dimension and ONE measure, so it can never present two meaningful axes or
    /// a numeric-by-numeric pair. Heatmap and Scatter therefore have renderers
    /// and no compatible binding until the query contract can express their
    /// shape. Inventing a second axis here to keep them selectable would be the
    /// same defect this task exists to remove.
    /// </summary>
    private static IReadOnlyList<DashboardCompatibilityRuleDto> BuildCompatibilityRules(
        IReadOnlyList<DashboardDimensionMetadataDto> dimensions,
        IReadOnlyList<DashboardMeasureMetadataDto> measures)
    {
        var rules = new List<DashboardCompatibilityRuleDto>();

        foreach (var dimension in dimensions)
        {
            foreach (var measure in measures)
            {
                var shape = new ChartDataShape(
                    PrimaryAxis: AxisRoleOf(dimension),
                    HasSecondCategoricalAxis: false,
                    HasMeasure: true,
                    MeasureIsDistribution: false,
                    EffectiveCategoryCount: null);

                var allowed = new List<string>();
                var refused = new List<DashboardChartRefusalDto>();

                foreach (var definition in DashboardChartGrammar.All)
                {
                    var verdict = DashboardChartGrammar.Evaluate(definition.Code, shape);

                    if (!verdict.IsCompatible)
                    {
                        refused.Add(new DashboardChartRefusalDto(definition.Code, verdict.Reason!));
                        continue;
                    }

                    // Semantically right and not drawable yet. Reported as its
                    // own reason, never merged with a modelling refusal: an
                    // author sent looking for a data problem that is not there
                    // loses more time than the missing renderer costs.
                    if (definition.Availability != ChartAvailability.Implemented)
                    {
                        refused.Add(new DashboardChartRefusalDto(
                            definition.Code,
                            DashboardChartGrammar.NotYetAvailableReason(definition)));
                        continue;
                    }

                    allowed.Add(definition.Code);
                }

                var requiresParameter = dimension.RequiresParameterCode || measure.RequiresParameterCode;

                rules.Add(new DashboardCompatibilityRuleDto(
                    dimension.Code,
                    measure.Code,
                    allowed,
                    refused,
                    requiresParameter,
                    requiresParameter ? "This combination requires a selected parameter code." : null));
            }
        }

        return rules;
    }

    /// <summary>
    /// The axis role is read from the REGISTERED dimension, so it stays true for
    /// a customer in any industry. A date bucket is temporal because the
    /// registry says its data type is a date, not because its code looks like a
    /// day.
    /// </summary>
    private static AxisRole AxisRoleOf(DashboardDimensionMetadataDto dimension)
    {
        if (string.Equals(dimension.DataType, "date", StringComparison.OrdinalIgnoreCase))
            return AxisRole.Temporal;

        if (string.Equals(dimension.DataType, "number", StringComparison.OrdinalIgnoreCase))
            return AxisRole.Numeric;

        return AxisRole.Categorical;
    }
}



