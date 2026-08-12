using PlantProcess.Domain.Entities.Dashboarding;
using Xunit;

namespace PlantProcess.Domain.Tests.Dashboarding;

/// <summary>
/// T-046 PACK 4B1. THE DOMAIN ACCEPTS A SHAPE. THE GRAMMAR JUDGES ITS MEANING.
///
/// The entity required a dimension on every widget. That invariant predates the
/// T-046 grammar and contradicts it, the chart metadata that declares KPI as
/// SupportsDimension = false, and the shipped presentation data: CF_RATE,
/// RI_TREND, MI_RATE, PA_KAVG and RI_KPI all persist a blank dimension and
/// could not be recreated through the API that is supposed to author them.
///
/// The entity now stores what it is given. Whether a CHART needs a dimension is
/// a question for DashboardWidgetValidationService, and the entity must never
/// learn the answer - a second copy of the grammar is the defect this whole
/// task exists to remove.
///
/// THE MEASURE REQUIREMENT IS DELIBERATELY UNCHANGED. The executable query
/// contract still needs one, and relaxing it here would be a different task
/// wearing this one's clothes.
/// </summary>
public sealed class WidgetDefinitionDimensionInvariantTests
{
    private static DashboardWidgetDefinition Kpi(string dimension = "") =>
        new(
            Guid.NewGuid(), "PROBE_KPI", "A single number", "kpi", "kpi",
            dimension, "materialCount", isSynthetic: true);

    private static DashboardWidgetDefinition Bar() =>
        new(
            Guid.NewGuid(), "PROBE_BAR", "By category", "chart", "bar",
            "materialUnitType", "materialCount", isSynthetic: true);

    [Fact]
    public void A_kpi_with_a_measure_and_no_dimension_can_be_created()
    {
        var widget = Kpi();

        Assert.Equal(string.Empty, widget.DimensionCode);
        Assert.Equal("materialCount", widget.MeasureCode);
    }

    /// <summary>
    /// The persistence representation is unchanged: blank stays the empty
    /// string, never null. Every row in the presentation database already holds
    /// it that way, and changing it would need a migration this pack refuses to
    /// make.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_dimension_is_stored_as_the_empty_string_never_null(string blank)
    {
        Assert.Equal(string.Empty, Kpi(blank).DimensionCode);
    }

    [Fact]
    public void A_dimensioned_widget_is_unchanged()
    {
        var widget = Bar();

        Assert.Equal("materialUnitType", widget.DimensionCode);
        Assert.Equal("bar", widget.ChartType);
    }

    /// <summary>
    /// The round trip T-046 names by hand: an author switches a bar to a KPI,
    /// and the dimension that no longer applies must be able to go away.
    /// </summary>
    [Fact]
    public void An_update_from_bar_to_kpi_can_clear_the_dimension()
    {
        var widget = Bar();

        widget.UpdateDefinition("A single number", "kpi", "kpi", "", "materialCount", null, null, null);

        Assert.Equal(string.Empty, widget.DimensionCode);
        Assert.Equal("kpi", widget.ChartType);
    }

    /// <summary>
    /// And back. The entity does NOT refuse a bar without a dimension - that is
    /// the semantic validator's judgement, and putting it here would create the
    /// second grammar this task removed.
    /// </summary>
    [Fact]
    public void The_entity_does_not_judge_whether_a_chart_needs_a_dimension()
    {
        var widget = Kpi();

        var thrown = Record.Exception(() =>
            widget.UpdateDefinition("By category", "chart", "bar", "", "materialCount", null, null, null));

        Assert.Null(thrown);
    }

    /// <summary>
    /// A measure is still required, in both paths. The executable contract needs
    /// one, and this pack changes exactly one invariant.
    /// </summary>
    [Fact]
    public void A_measure_is_still_required()
    {
        Assert.Throws<ArgumentException>(() => new DashboardWidgetDefinition(
            Guid.NewGuid(), "PROBE", "No measure", "kpi", "kpi", "", "", isSynthetic: true));

        var widget = Bar();
        Assert.Throws<ArgumentException>(() =>
            widget.UpdateDefinition("No measure", "chart", "bar", "materialUnitType", "", null, null, null));
    }

    /// <summary>
    /// Everything the entity guarded before, it still guards. Removing one stale
    /// invariant must not quietly remove its neighbours.
    /// </summary>
    [Fact]
    public void Every_other_guard_still_holds()
    {
        Assert.Throws<ArgumentException>(() => new DashboardWidgetDefinition(
            Guid.Empty, "PROBE", "T", "kpi", "kpi", "", "materialCount", isSynthetic: true));

        Assert.Throws<ArgumentException>(() => new DashboardWidgetDefinition(
            Guid.NewGuid(), " ", "T", "kpi", "kpi", "", "materialCount", isSynthetic: true));

        Assert.Throws<ArgumentException>(() => new DashboardWidgetDefinition(
            Guid.NewGuid(), "PROBE", " ", "kpi", "kpi", "", "materialCount", isSynthetic: true));

        Assert.Throws<ArgumentException>(() => new DashboardWidgetDefinition(
            Guid.NewGuid(), "PROBE", "T", " ", "kpi", "", "materialCount", isSynthetic: true));

        Assert.Throws<ArgumentException>(() => new DashboardWidgetDefinition(
            Guid.NewGuid(), "PROBE", "T", "kpi", " ", "", "materialCount", isSynthetic: true));
    }
}