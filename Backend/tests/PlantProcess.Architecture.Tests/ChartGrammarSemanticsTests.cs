using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Widgets;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-046. THE FROZEN ACCEPTANCE, AS EXECUTABLE RULES.
///
/// "Can the renderer draw it?" is not the question. "Does this chart type make
/// analytical sense for this data shape?" is.
///
/// Every pairing named in the task text appears here, positive and negative,
/// because a compatibility model with no negative tests only proves that it
/// says yes.
/// </summary>
public sealed class ChartGrammarSemanticsTests
{
    private static ChartDataShape Temporal() =>
        new(AxisRole.Temporal, HasSecondCategoricalAxis: false, HasMeasure: true, MeasureIsDistribution: false, EffectiveCategoryCount: null);

    private static ChartDataShape Categorical(int? categories = null) =>
        new(AxisRole.Categorical, HasSecondCategoricalAxis: false, HasMeasure: true, MeasureIsDistribution: false, EffectiveCategoryCount: categories);

    private static ChartDataShape TwoCategoricalAxes() =>
        new(AxisRole.Categorical, HasSecondCategoricalAxis: true, HasMeasure: true, MeasureIsDistribution: false, EffectiveCategoryCount: null);

    private static ChartDataShape NumericByNumeric() =>
        new(AxisRole.Numeric, HasSecondCategoricalAxis: false, HasMeasure: true, MeasureIsDistribution: false, EffectiveCategoryCount: null);

    private static ChartDataShape Distribution() =>
        new(AxisRole.Categorical, HasSecondCategoricalAxis: false, HasMeasure: true, MeasureIsDistribution: true, EffectiveCategoryCount: null);

    private static void AssertCompatible(string chart, ChartDataShape shape)
    {
        var verdict = DashboardChartGrammar.Evaluate(chart, shape);
        Assert.True(verdict.IsCompatible, chart + " was refused: " + verdict.Reason);
    }

    private static void AssertRefused(string chart, ChartDataShape shape)
    {
        var verdict = DashboardChartGrammar.Evaluate(chart, shape);
        Assert.False(verdict.IsCompatible, chart + " was offered for a shape it does not suit");
        Assert.False(string.IsNullOrWhiteSpace(verdict.Reason), chart + " was refused without a reason a human can act on");
    }

    // ---------------------------------------------------------------- registry

    [Fact]
    public void The_registry_declares_exactly_the_seventeen_product_chart_types()
    {
        Assert.Equal(17, DashboardChartGrammar.All.Count);

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in DashboardChartGrammar.All)
        {
            Assert.True(codes.Add(definition.Code), "duplicate chart type code: " + definition.Code);
            Assert.False(string.IsNullOrWhiteSpace(definition.Label), definition.Code + " has no label");
            Assert.False(string.IsNullOrWhiteSpace(definition.Description), definition.Code + " has no description");
        }
    }

    /// <summary>
    /// Availability and compatibility are independent. A type that is not yet
    /// drawable must still answer the semantic question correctly, or flipping
    /// its availability later would ship an untested rule.
    /// </summary>
    [Fact]
    public void Availability_never_decides_compatibility()
    {
        var histogram = DashboardChartGrammar.Find(DashboardMetadataCodes.ChartTypes.Histogram);
        Assert.NotNull(histogram);
        Assert.Equal(ChartAvailability.NotYetAvailable, histogram!.Availability);

        // Not yet drawable, and still semantically right for a distribution.
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Histogram, Distribution());

        // Drawable today, and still semantically wrong for a time axis.
        Assert.True(DashboardChartGrammar.IsImplemented(DashboardMetadataCodes.ChartTypes.Pie));
        AssertRefused(DashboardMetadataCodes.ChartTypes.Pie, Temporal());
    }

    // ------------------------------------------------------- positive pairings

    [Fact]
    public void Temporal_and_numeric_offers_line_area_and_bar()
    {
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Line, Temporal());
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Area, Temporal());
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Bar, Temporal());
    }

    [Fact]
    public void Categorical_and_numeric_offers_bar_and_pareto()
    {
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Bar, Categorical());
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Pareto, Categorical());
    }

    [Fact]
    public void Two_meaningful_categorical_axes_and_a_measure_offers_heatmap()
    {
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Heatmap, TwoCategoricalAxes());
    }

    [Fact]
    public void Numeric_by_numeric_offers_scatter()
    {
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Scatter, NumericByNumeric());
    }

    [Fact]
    public void A_numeric_distribution_offers_histogram_and_box_plot()
    {
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Histogram, Distribution());
        AssertCompatible(DashboardMetadataCodes.ChartTypes.BoxPlot, Distribution());
    }

    [Fact]
    public void A_low_cardinality_categorical_share_offers_pie_and_donut()
    {
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Pie, Categorical(4));
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Donut, Categorical(4));
    }

    // ------------------------------------------------------- negative pairings

    [Fact]
    public void A_date_axis_is_refused_by_pie_and_donut()
    {
        AssertRefused(DashboardMetadataCodes.ChartTypes.Pie, Temporal());
        AssertRefused(DashboardMetadataCodes.ChartTypes.Donut, Temporal());
    }

    [Fact]
    public void One_categorical_axis_is_refused_by_heatmap()
    {
        AssertRefused(DashboardMetadataCodes.ChartTypes.Heatmap, Categorical());
    }

    /// <summary>
    /// This is the T-045 MI_SEV defect as a rule. That widget was defectCount by
    /// materialUnitType drawn as a donut, and the data held one category, so a
    /// customer saw a single slice at one hundred percent. It was retired by
    /// hand; nothing stopped the next one being authored.
    /// </summary>
    [Fact]
    public void One_effective_category_is_refused_by_pie_and_donut()
    {
        AssertRefused(DashboardMetadataCodes.ChartTypes.Pie, Categorical(1));
        AssertRefused(DashboardMetadataCodes.ChartTypes.Donut, Categorical(1));
    }

    [Fact]
    public void A_categorical_axis_is_refused_by_scatter()
    {
        AssertRefused(DashboardMetadataCodes.ChartTypes.Scatter, Categorical());
    }

    [Fact]
    public void A_grouping_dimension_is_refused_by_kpi_and_gauge()
    {
        AssertRefused(DashboardMetadataCodes.ChartTypes.Kpi, Categorical());
        AssertRefused(DashboardMetadataCodes.ChartTypes.Gauge, Temporal());
    }

    [Fact]
    public void An_unordered_category_is_refused_by_line_and_area()
    {
        AssertRefused(DashboardMetadataCodes.ChartTypes.Line, Categorical());
        AssertRefused(DashboardMetadataCodes.ChartTypes.Area, Categorical());
    }

    /// <summary>
    /// A cardinality nobody has measured yet must not be treated as a failure.
    /// The authoring surface knows the dimension, not the data, so the rule is
    /// deferred to query time rather than guessed.
    /// </summary>
    [Fact]
    public void An_unknown_cardinality_does_not_refuse_a_share_chart()
    {
        AssertCompatible(DashboardMetadataCodes.ChartTypes.Pie, Categorical(categories: null));
    }

    /// <summary>
    /// Every refusal must be a sentence, not a code. Measured before this task:
    /// the previous model gave no reason at all, because it never refused - it
    /// simply omitted a type from a curated list.
    /// </summary>
    [Fact]
    public void Every_refusal_carries_a_human_sentence()
    {
        var shapes = new[] { Temporal(), Categorical(), Categorical(1), NumericByNumeric(), Distribution(), TwoCategoricalAxes() };

        foreach (var definition in DashboardChartGrammar.All)
        {
            foreach (var shape in shapes)
            {
                var verdict = DashboardChartGrammar.Evaluate(definition.Code, shape);
                if (verdict.IsCompatible)
                    continue;

                Assert.False(string.IsNullOrWhiteSpace(verdict.Reason), definition.Code + " refused without a reason");
                Assert.True(verdict.Reason!.Length > 20, definition.Code + " refused with a reason too short to act on: " + verdict.Reason);
            }
        }
    }
}