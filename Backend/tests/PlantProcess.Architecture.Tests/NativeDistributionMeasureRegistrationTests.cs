using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Widgets;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-047 Pack A. THE REGISTRATION CONVENTION FOR A NATIVE MEASURE.
///
/// A Class-2 measure must pass four gates: supported, executable by name,
/// declaring its own columns, and - for a chart-bearing widget - an accepted
/// chart code. Missing any one produces a widget that fails somewhere other
/// than where the cause is, which is how the histogram chart code was found
/// still absent from the pre-T-046 ten.
///
/// The last test encodes a convention rather than asserting a value: native
/// measures are NOT published in the authoring catalogue, because their shapes
/// are not authorable through a one-dimension binding. It is written against
/// the three that shipped before this pack, so if the reading is wrong it
/// fails on them and not on a guess about the two added here.
/// </summary>
public class NativeDistributionMeasureRegistrationTests
{
    public static TheoryData<string> NativeDistributionMeasures => new()
    {
        DashboardMetadataCodes.Measures.ParameterValueDistribution,
        DashboardMetadataCodes.Measures.RiskScoreDistribution
    };

    [Theory]
    [MemberData(nameof(NativeDistributionMeasures))]
    public void Distribution_measure_is_supported(string measureCode)
    {
        Assert.True(DashboardWidgetQuerySafetyRegistry.IsSupportedMeasure(measureCode));
    }

    [Theory]
    [MemberData(nameof(NativeDistributionMeasures))]
    public void Distribution_measure_declares_its_own_columns(string measureCode)
    {
        Assert.True(DashboardWidgetQuerySafetyRegistry.MeasureProvidesOwnColumns(measureCode));
    }

    [Fact]
    public void Parameter_distribution_requires_a_parameter_and_risk_distribution_does_not()
    {
        Assert.True(DashboardWidgetQuerySafetyRegistry.MeasureRequiresParameterCode(
            DashboardMetadataCodes.Measures.ParameterValueDistribution));

        Assert.False(DashboardWidgetQuerySafetyRegistry.MeasureRequiresParameterCode(
            DashboardMetadataCodes.Measures.RiskScoreDistribution));
    }

    [Fact]
    public void Histogram_is_an_accepted_chart_code()
    {
        Assert.True(DashboardWidgetQuerySafetyRegistry.IsSupportedChartType(
            DashboardMetadataCodes.ChartTypes.Histogram));
    }

    [Fact]
    public void Histogram_is_implemented_now_that_a_renderer_exists()
    {
        Assert.True(DashboardChartGrammar.IsImplemented(DashboardMetadataCodes.ChartTypes.Histogram));
    }

    [Fact]
    public void Implemented_histogram_is_still_refused_for_a_non_distribution_shape()
    {
        // Availability and generic-binding compatibility are separate contracts.
        // A one-dimension fact-shaped binding must still be refused, with a
        // sentence about the data shape and not about the renderer.
        var shape = new ChartDataShape(
            PrimaryAxis: AxisRole.Categorical,
            HasSecondCategoricalAxis: false,
            HasMeasure: true,
            MeasureIsDistribution: false,
            EffectiveCategoryCount: 5);

        var verdict = DashboardChartGrammar.Evaluate(
            DashboardMetadataCodes.ChartTypes.Histogram, shape);

        Assert.False(verdict.IsCompatible);
        Assert.False(verdict.DependsOnQueryState);
    }
}