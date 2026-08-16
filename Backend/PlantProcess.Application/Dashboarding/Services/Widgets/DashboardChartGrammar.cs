using PlantProcess.Application.Dashboarding.Contracts;

namespace PlantProcess.Application.Dashboarding.Services.Widgets;

// ============================================================================
// T-046. THE FINAL CHART GRAMMAR, AND THE SEMANTIC RULE THAT GOVERNS IT.
//
// THE QUESTION THIS FILE ANSWERS IS NOT "can the renderer draw it?" It is
// "does this chart type make analytical sense for this data shape?"
//
// WHAT IT REPLACES. Compatibility was two hand-maintained literal lists - one
// per dimension, one per measure - intersected to produce 154 pairs from 25
// curated arrays. Curation is not a rule: it happens to exclude a pie by date
// today, and one edited literal brings it back. Measured before this task, the
// curated lists ALREADY allowed a heatmap on a single categorical axis and a
// pie over a dimension with one effective category, and they declared 'pareto'
// while listing it in no array at all, so it was unselectable.
//
// SEVENTEEN, NOT TEN. Chapter 4 5.1.5 defines the product grammar as seventeen
// chart types. Declaring only the ten with renderers shows a customer a smaller
// product than the one they receive, so all seventeen are declared here and
// each carries an explicit availability. Implementing a renderer later is a
// change of ONE FIELD in this file: no compatibility rule, no validator and no
// endpoint changes with it.
//
// AVAILABILITY NEVER CREATES COMPATIBILITY. The two are independent and both
// must hold before a type is offered. A renderer that exists cannot make a
// nonsense pairing sensible, and a pairing that makes sense is still not
// offered while its renderer is absent - with a sentence saying which of the
// two reasons applies.
// ============================================================================

/// <summary>
/// Whether the product can draw this chart type today. It says nothing about
/// whether the type suits any particular data.
/// </summary>
public enum ChartAvailability
{
    Implemented,
    NotYetAvailable
}

/// <summary>
/// What the grouping axis MEANS, derived from the registered dimension rather
/// than from a chart's wishes. `None` is a dimensionless widget.
/// </summary>
public enum AxisRole
{
    None,
    Temporal,
    Categorical,
    Numeric
}

/// <summary>
/// The shape of the data a widget would plot. Everything the semantic rules
/// need and nothing else - no widget code, no dashboard code, no page.
///
/// EffectiveCategoryCount is NULL when the cardinality is not yet known. The
/// authoring surface knows a dimension but not how many categories the data
/// actually holds, so a rule that needs cardinality is DEFERRED rather than
/// guessed: it is re-evaluated at query time, before anything renders.
/// </summary>
public sealed record ChartDataShape(
    AxisRole PrimaryAxis,
    bool HasSecondCategoricalAxis,
    bool HasMeasure,
    bool MeasureIsDistribution,
    int? EffectiveCategoryCount);

/// <summary>
/// The verdict, with the sentence a human is shown for a refusal.
///
/// DependsOnQueryState separates two kinds of refusal that must never wear the
/// same explanation. A STRUCTURAL refusal is a property of the binding - a
/// heatmap has one axis, a KPI has a grouping dimension - and no filter change
/// will ever fix it. A QUERY-STATE refusal is a property of the data THIS
/// selection produced, and a different window may well be valid.
///
/// Telling an author their filters caused a structural refusal sends them to
/// adjust the one thing that cannot help.
/// </summary>
public sealed record ChartCompatibility(bool IsCompatible, string? Reason, bool DependsOnQueryState = false)
{
    public static readonly ChartCompatibility Yes = new(true, null);

    /// <summary>A refusal that holds regardless of the data.</summary>
    public static ChartCompatibility No(string reason) => new(false, reason);

    /// <summary>
    /// A refusal caused by what THIS query returned under THIS selection. Only
    /// these may be described as a consequence of the current filters.
    /// </summary>
    public static ChartCompatibility NoForThisQuery(string reason) => new(false, reason, true);
}

public sealed record ChartTypeDefinition(
    string Code,
    string Label,
    string Category,
    ChartAvailability Availability,
    bool SupportsDimension,
    bool SupportsMeasure,
    bool SupportsMultipleSeries,
    bool SupportsParameterSelection,
    string Description);

public static class DashboardChartGrammar
{
    // The share ceiling for a part-of-whole chart. Above this a pie stops being
    // readable and becomes a colour wheel; the number is a presentation limit,
    // not a data claim, which is why it lives here and not in the engine.
    public const int MaxCategoriesForShareChart = 12;

    public static readonly IReadOnlyList<ChartTypeDefinition> All = new[]
    {
        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Kpi, "KPI", "Summary",
            ChartAvailability.Implemented, false, true, false, true,
            "A single governed number with no grouping axis."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Bar, "Bar", "Comparison",
            ChartAvailability.Implemented, true, true, false, true,
            "Compares a measure across categories, or across time buckets when order carries meaning."),

        // T-047 Pack D. Implemented alongside StackedSeriesChart and the two
        // multi-series sources. Generic authoring still refuses it, because
        // HasSecondCategoricalAxis is false on a one-dimension binding.
        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.StackedColumn, "Stacked Column", "Comparison",
            ChartAvailability.Implemented, true, true, true, true,
            "Compares a total across categories while showing its composition."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Line, "Line", "Trend",
            ChartAvailability.Implemented, true, true, true, true,
            "Follows a measure through time. The axis must be ordered, or the line asserts a progression that does not exist."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Area, "Area", "Trend",
            ChartAvailability.Implemented, true, true, true, true,
            "A trend where the area under the line carries the cumulative reading."),

        // T-046-R1. Implemented alongside PairedSeriesChart. The Evaluate arm
        // is deliberately UNCHANGED: Combo still requires a temporal axis for
        // generic authoring, because a single categorical binding cannot
        // supply two independent series. The paired equipment comparison
        // reaches the renderer through a native source, which returns before
        // the shape gate - exactly as Histogram, BoxPlot and StackedColumn do.
        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Combo, "Combo", "Trend",
            ChartAvailability.Implemented, true, true, true, true,
            "Two measures on one temporal axis with independent scales."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Pie, "Pie", "Share",
            ChartAvailability.Implemented, true, true, false, false,
            "Parts of one whole across a small number of categories."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Donut, "Donut", "Share",
            ChartAvailability.Implemented, true, true, false, false,
            "Parts of one whole, with the centre free for the total."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Scatter, "Scatter", "Relationship",
            ChartAvailability.Implemented, true, true, false, true,
            "One numeric quantity against another, to show a relationship rather than a ranking."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Heatmap, "Heatmap", "Relationship",
            ChartAvailability.Implemented, true, true, false, true,
            "An intensity read across TWO meaningful axes. With one axis it is a bar chart wearing colour."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Pareto, "Pareto", "Comparison",
            ChartAvailability.Implemented, true, true, false, false,
            "Categories ranked by contribution with a cumulative line, to separate the few that matter."),

        // T-047 Pack B. Implemented alongside BoxPlotChart and the
        // parameterValueSpread source, never before. Evaluate still gates it on
        // MeasureIsDistribution, so generic one-dimension authoring continues
        // to refuse it with the existing sentence.
        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.BoxPlot, "Box Plot", "Distribution",
            ChartAvailability.Implemented, true, true, false, true,
            "The spread of a numeric quantity, and its outliers, across groups."),

        // T-047 Pack A. Implemented in the same pack as HistogramChart and the
        // two distribution sources that feed it, never before.
        //
        // THIS DOES NOT MAKE HISTOGRAM OFFERABLE IN GENERIC AUTHORING. Evaluate
        // still gates it on MeasureIsDistribution, which a one-dimension
        // fact-shaped binding does not satisfy, so the authoring switcher
        // refuses it with the existing sentence. Availability and generic
        // binding compatibility are separate contracts and stay separate.
        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Histogram, "Histogram", "Distribution",
            ChartAvailability.Implemented, true, true, false, true,
            "How often a numeric quantity falls in each interval."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Gauge, "Gauge", "Summary",
            ChartAvailability.NotYetAvailable, false, true, false, true,
            "One number against a governed target or limit."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Waterfall, "Waterfall", "Comparison",
            ChartAvailability.NotYetAvailable, true, true, false, false,
            "How ordered contributions move a total from one value to another."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.Table, "Table", "Detail",
            ChartAvailability.Implemented, true, true, true, true,
            "The rows themselves. It asserts no shape, so it fits any data."),

        new ChartTypeDefinition(DashboardMetadataCodes.ChartTypes.PivotTable, "Pivot Table", "Detail",
            ChartAvailability.NotYetAvailable, true, true, true, true,
            "Rows and columns crossed, with the measure at the intersection.")
    };

    public static ChartTypeDefinition? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        foreach (var definition in All)
        {
            if (string.Equals(definition.Code, code.Trim(), StringComparison.OrdinalIgnoreCase))
                return definition;
        }

        return null;
    }

    public static bool IsImplemented(string? code)
    {
        return Find(code)?.Availability == ChartAvailability.Implemented;
    }

    /// <summary>
    /// THE SEMANTIC RULE. It reads only the data shape, so it holds for a
    /// customer in oil, water, pharma, paper, cement or food exactly as it does
    /// for the dataset in front of it.
    ///
    /// A refusal always carries one sentence a human can act on. "Incompatible"
    /// with no reason teaches an author nothing and they try again.
    /// </summary>
    public static ChartCompatibility Evaluate(string chartCode, ChartDataShape shape)
    {
        var definition = Find(chartCode);
        if (definition is null)
            return ChartCompatibility.No("This chart type is not part of the product grammar.");

        if (definition.SupportsMeasure && !shape.HasMeasure)
            return ChartCompatibility.No("This chart type needs a measure to plot.");

        switch (definition.Code)
        {
            case DashboardMetadataCodes.ChartTypes.Kpi:
            case DashboardMetadataCodes.ChartTypes.Gauge:
                return shape.PrimaryAxis == AxisRole.None
                    ? ChartCompatibility.Yes
                    : ChartCompatibility.No("A single-number widget cannot carry a grouping dimension. Remove the dimension, or choose a chart that shows one.");

            case DashboardMetadataCodes.ChartTypes.Line:
            case DashboardMetadataCodes.ChartTypes.Area:
            case DashboardMetadataCodes.ChartTypes.Combo:
                return shape.PrimaryAxis == AxisRole.Temporal
                    ? ChartCompatibility.Yes
                    : ChartCompatibility.No("A trend needs an ordered time axis. Joining unordered categories with a line asserts a progression that does not exist.");

            case DashboardMetadataCodes.ChartTypes.Bar:
                if (shape.PrimaryAxis == AxisRole.Temporal || shape.PrimaryAxis == AxisRole.Categorical)
                    return ChartCompatibility.Yes;
                return ChartCompatibility.No("A bar needs a category or a time bucket on its axis.");

            // T-047 Pack D. SEPARATED FROM BAR, DELIBERATELY.
            //
            // Sharing the bar rule made a stack compatible with any single
            // grouping, and a stack of one series IS a bar - drawn with the
            // legend and the reading of a composition that does not exist.
            // The requirement mirrors Heatmap's: a second axis, or nothing to
            // stack.
            case DashboardMetadataCodes.ChartTypes.StackedColumn:
                if (!shape.HasSecondCategoricalAxis)
                    return ChartCompatibility.No("A stack needs a second grouping to divide each column by. With one grouping every column is a single block, which is a bar chart with a legend.");
                if (shape.PrimaryAxis == AxisRole.Temporal || shape.PrimaryAxis == AxisRole.Categorical)
                    return ChartCompatibility.Yes;
                return ChartCompatibility.No("A stack needs a category or a time bucket on its axis.");

            case DashboardMetadataCodes.ChartTypes.Pareto:
            case DashboardMetadataCodes.ChartTypes.Waterfall:
                return shape.PrimaryAxis == AxisRole.Categorical
                    ? ChartCompatibility.Yes
                    : ChartCompatibility.No("Ranking contributions needs a categorical axis. Time buckets are already ordered, so ranking them hides the order that matters.");

            case DashboardMetadataCodes.ChartTypes.Pie:
            case DashboardMetadataCodes.ChartTypes.Donut:
                if (shape.PrimaryAxis == AxisRole.Temporal)
                    return ChartCompatibility.No("A share chart divides one whole. Time buckets are a sequence, not parts of a whole, so use a trend or a bar.");
                if (shape.PrimaryAxis != AxisRole.Categorical)
                    return ChartCompatibility.No("A share chart needs a categorical axis to divide.");
                if (shape.EffectiveCategoryCount.HasValue && shape.EffectiveCategoryCount.Value < 2)
                    return ChartCompatibility.NoForThisQuery("This grouping produces one category, so the chart would be a single slice at one hundred percent. Choose a dimension with more than one value.");
                if (shape.EffectiveCategoryCount.HasValue && shape.EffectiveCategoryCount.Value > MaxCategoriesForShareChart)
                    return ChartCompatibility.NoForThisQuery("This grouping produces more categories than a share chart can be read at. Use a ranked bar or a Pareto.");
                return ChartCompatibility.Yes;

            case DashboardMetadataCodes.ChartTypes.Scatter:
                return shape.PrimaryAxis == AxisRole.Numeric
                    ? ChartCompatibility.Yes
                    : ChartCompatibility.No("A scatter plots one numeric quantity against another. With a category on the axis there is no second quantity to relate.");

            case DashboardMetadataCodes.ChartTypes.Heatmap:
                if (!shape.HasSecondCategoricalAxis)
                    return ChartCompatibility.No("A heatmap needs two meaningful axes and an intensity. With one axis it is a bar chart wearing colour.");
                return shape.PrimaryAxis == AxisRole.Categorical || shape.PrimaryAxis == AxisRole.Temporal
                    ? ChartCompatibility.Yes
                    : ChartCompatibility.No("A heatmap needs two meaningful axes and an intensity.");

            case DashboardMetadataCodes.ChartTypes.Histogram:
            case DashboardMetadataCodes.ChartTypes.BoxPlot:
                return shape.MeasureIsDistribution
                    ? ChartCompatibility.Yes
                    : ChartCompatibility.No("A distribution chart needs the underlying numeric values, not a single aggregated result per group.");

            case DashboardMetadataCodes.ChartTypes.Table:
            case DashboardMetadataCodes.ChartTypes.PivotTable:
                return ChartCompatibility.Yes;

            default:
                return ChartCompatibility.No("This chart type has no semantic rule, so it is not offered.");
        }
    }

    /// <summary>
    /// The sentence for a type that IS semantically right but cannot be drawn
    /// yet. Kept separate from Evaluate on purpose: conflating the two would let
    /// a missing renderer read as a modelling mistake, and an author would go
    /// looking for a data problem that is not there.
    /// </summary>
    public static string NotYetAvailableReason(ChartTypeDefinition definition)
    {
        return definition.Label + " suits this data but is not available in this release.";
    }
}