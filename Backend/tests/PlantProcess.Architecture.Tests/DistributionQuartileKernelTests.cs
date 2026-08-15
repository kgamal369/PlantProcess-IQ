using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Queries;
using PlantProcess.Application.Dashboarding.Services.Widgets;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-047 Pack B. KNOWN ANSWERS FOR THE QUARTILE KERNEL.
///
/// Quartiles have at least nine defensible definitions that disagree on small
/// samples. These fixtures pin ours to R-7 - Excel PERCENTILE.INC and NumPy's
/// default - so a customer comparing our median with their spreadsheet gets an
/// answer we can explain rather than a discrepancy nobody can account for.
///
/// Every expected value below is computed by hand in the comment beside it.
/// </summary>
public class DistributionQuartileKernelTests
{
    [Fact]
    public void An_odd_population_lands_on_actual_observations()
    {
        // 1 2 3 4 5, n=5. h = (n-1)p. q1: h=1 -> x[1]=2. med: h=2 -> 3. q3: h=3 -> 4.
        var summary = DistributionQuartiles.Summarise(new[] { 5m, 3m, 1m, 4m, 2m });

        Assert.Equal(1m, summary.Minimum);
        Assert.Equal(2m, summary.Q1);
        Assert.Equal(3m, summary.Median);
        Assert.Equal(4m, summary.Q3);
        Assert.Equal(5m, summary.Maximum);
    }

    [Fact]
    public void An_even_population_interpolates_between_observations()
    {
        // 1 2 3 4, n=4. q1: h=0.75 -> 1 + 0.75(2-1) = 1.75.
        //                med: h=1.5 -> 2 + 0.5(3-2) = 2.5.
        //                q3: h=2.25 -> 3 + 0.25(4-3) = 3.25.
        var summary = DistributionQuartiles.Summarise(new[] { 4m, 1m, 3m, 2m });

        Assert.Equal(1.75m, summary.Q1);
        Assert.Equal(2.5m, summary.Median);
        Assert.Equal(3.25m, summary.Q3);
    }

    [Fact]
    public void A_single_observation_is_its_own_every_quantile()
    {
        var summary = DistributionQuartiles.Summarise(new[] { 7m });

        Assert.Equal(7m, summary.Minimum);
        Assert.Equal(7m, summary.Q1);
        Assert.Equal(7m, summary.Median);
        Assert.Equal(7m, summary.Q3);
        Assert.Equal(7m, summary.Maximum);
    }

    [Fact]
    public void The_result_does_not_depend_on_input_order()
    {
        var ascending = DistributionQuartiles.Summarise(new[] { 1m, 2m, 3m, 4m, 5m, 6m, 7m });
        var shuffled = DistributionQuartiles.Summarise(new[] { 4m, 7m, 1m, 6m, 3m, 5m, 2m });

        Assert.Equal(ascending, shuffled);
    }

    [Fact]
    public void An_empty_population_is_refused_rather_than_answered()
    {
        Assert.Throws<ArgumentException>(() => DistributionQuartiles.Percentile(new decimal[0], 0.5m));
    }

    [Fact]
    public void The_thin_group_threshold_is_declared_not_inlined()
    {
        Assert.True(DistributionQuartiles.MinimumObservationsPerGroup >= 5);
    }

    // ------------------------------------------------------------- registry

    [Fact]
    public void The_spread_measure_passes_every_registration_gate()
    {
        var measure = DashboardMetadataCodes.Measures.ParameterValueSpread;

        Assert.True(DashboardWidgetQuerySafetyRegistry.IsSupportedMeasure(measure));
        Assert.True(DashboardWidgetQuerySafetyRegistry.MeasureProvidesOwnColumns(measure));
        Assert.True(DashboardWidgetQuerySafetyRegistry.MeasureRequiresParameterCode(measure));
        Assert.True(DashboardWidgetQuerySafetyRegistry.IsSupportedChartType(
            DashboardMetadataCodes.ChartTypes.BoxPlot));
    }

    [Fact]
    public void Box_plot_is_implemented_and_still_refused_for_a_non_distribution_shape()
    {
        Assert.True(DashboardChartGrammar.IsImplemented(DashboardMetadataCodes.ChartTypes.BoxPlot));

        var shape = new ChartDataShape(
            PrimaryAxis: AxisRole.Categorical,
            HasSecondCategoricalAxis: false,
            HasMeasure: true,
            MeasureIsDistribution: false,
            EffectiveCategoryCount: 5);

        var verdict = DashboardChartGrammar.Evaluate(DashboardMetadataCodes.ChartTypes.BoxPlot, shape);

        Assert.False(verdict.IsCompatible);
        Assert.False(verdict.DependsOnQueryState);
    }
}