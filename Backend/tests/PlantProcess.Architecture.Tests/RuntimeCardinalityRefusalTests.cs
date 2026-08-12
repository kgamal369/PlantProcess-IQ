using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Widgets;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-046 PACK 3B. THE CARDINALITY RULE, AS THE RUNTIME APPLIES IT.
///
/// Pack 1 proved the rule in the abstract. These prove the shape the query path
/// actually hands it, including the one thing that makes the rule correct: a
/// row is not a category.
/// </summary>
public sealed class RuntimeCardinalityRefusalTests
{
    private static ChartDataShape Share(int? effective) =>
        new(AxisRole.Categorical, HasSecondCategoricalAxis: false, HasMeasure: true,
            MeasureIsDistribution: false, EffectiveCategoryCount: effective);

    /// <summary>
    /// The MI_SEV case. One real category rendered as a donut is a single slice
    /// at one hundred percent.
    /// </summary>
    [Fact]
    public void One_effective_category_refuses_a_share_chart_with_a_reason()
    {
        var verdict = DashboardChartGrammar.Evaluate(DashboardMetadataCodes.ChartTypes.Donut, Share(1));

        Assert.False(verdict.IsCompatible);
        Assert.Contains("one category", verdict.Reason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Two_effective_categories_are_enough_for_a_share_chart()
    {
        Assert.True(DashboardChartGrammar.Evaluate(DashboardMetadataCodes.ChartTypes.Donut, Share(2)).IsCompatible);
        Assert.True(DashboardChartGrammar.Evaluate(DashboardMetadataCodes.ChartTypes.Pie, Share(3)).IsCompatible);
    }

    /// <summary>
    /// The measured presentation widgets must keep rendering. PO_MIX groups
    /// three material types, RI_TABLE three, EO_MONTH five and PA_TABLE six.
    /// </summary>
    [Fact]
    public void The_measured_presentation_widgets_stay_compatible()
    {
        foreach (var categories in new[] { 3, 3, 5, 6 })
        {
            Assert.True(
                DashboardChartGrammar.Evaluate(DashboardMetadataCodes.ChartTypes.Donut, Share(categories)).IsCompatible,
                "a widget with " + categories + " categories was refused");
        }
    }

    /// <summary>
    /// A chart that does not divide a whole is not governed by this rule. A bar
    /// with one category is sparse, not dishonest.
    /// </summary>
    [Fact]
    public void A_single_category_does_not_refuse_a_bar_or_a_table()
    {
        Assert.True(DashboardChartGrammar.Evaluate(DashboardMetadataCodes.ChartTypes.Bar, Share(1)).IsCompatible);
        Assert.True(DashboardChartGrammar.Evaluate(DashboardMetadataCodes.ChartTypes.Table, Share(1)).IsCompatible);
    }

    /// <summary>
    /// Heatmap stays governed by the two-axes rule. Cardinality must never be
    /// able to conjure a second axis.
    /// </summary>
    [Fact]
    public void Cardinality_never_satisfies_the_heatmap_rule()
    {
        foreach (var categories in new int?[] { 1, 2, 40, null })
        {
            var verdict = DashboardChartGrammar.Evaluate(DashboardMetadataCodes.ChartTypes.Heatmap, Share(categories));
            Assert.False(verdict.IsCompatible, "a heatmap was allowed on one axis with " + categories + " categories");
            Assert.Contains("two meaningful axes", verdict.Reason!, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A cardinality nobody has measured is not a failure. The authoring surface
    /// sees null and must not refuse on it.
    /// </summary>
    [Fact]
    public void An_unmeasured_cardinality_does_not_refuse()
    {
        Assert.True(DashboardChartGrammar.Evaluate(DashboardMetadataCodes.ChartTypes.Donut, Share(null)).IsCompatible);
    }
}