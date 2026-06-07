
using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class Phase7_ValueRealizationTrackingTests
{
    private static ValueRealizationRequest DemoRequest(decimal actualValue = 80m) => new(
        TrackingCode: "T039-DEMO-EDGE-CRACK-REALIZATION",
        SourceRecommendationId: "rec-edge-crack-001",
        SourceValueImpactId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        BaselineWindow: new ValueRealizationWindow(
            "edge_crack_count",
            new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 5, 31, 0, 0, 0, TimeSpan.Zero),
            100m,
            "defects"),
        ActualWindow: new ValueRealizationWindow(
            "edge_crack_count",
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero),
            actualValue,
            "defects"),
        Direction: ValueMetricDirection.LowerIsBetter,
        ValuePerUnit: new CostBand(100m, 150m, 200m),
        PotentialValue: new CostBand(28_000m, 42_000m, 56_000m),
        InvestmentCost: 1_000m,
        Currency: "EUR");

    [Fact]
    public void T039_Computes_BaselineVsActual_TrackedValue_AndRoi()
    {
        var result = new ValueRealizationService().Calculate(DemoRequest());

        Assert.False(result.IsAbstained);
        Assert.True(result.IsMonotonic);
        Assert.Equal(20m, result.ImprovementUnits);
        Assert.Equal(2_000m, result.RealizedLow);
        Assert.Equal(3_000m, result.RealizedExpected);
        Assert.Equal(4_000m, result.RealizedHigh);
        Assert.Equal(0.0714m, result.CaptureRateMid);
        Assert.Equal(3.0000m, result.RoiMid);
        Assert.Equal("PositiveTrackedValue", result.Status);
        Assert.Contains("Correlation is not causation", result.AttributionCaveat);
    }

    [Fact]
    public void T039_ChangingActualValue_ChangesRealizedValue()
    {
        var service = new ValueRealizationService();

        var first = service.Calculate(DemoRequest(actualValue: 80m));
        var second = service.Calculate(DemoRequest(actualValue: 70m));

        Assert.Equal(3_000m, first.RealizedExpected);
        Assert.Equal(4_500m, second.RealizedExpected);
        Assert.True(second.RealizedExpected > first.RealizedExpected);
    }

    [Fact]
    public void T039_Abstains_WhenMetricWindowsDoNotMatch()
    {
        var request = DemoRequest() with
        {
            ActualWindow = DemoRequest().ActualWindow with { MetricCode = "different_metric" }
        };

        var result = new ValueRealizationService().Calculate(request);

        Assert.True(result.IsAbstained);
        Assert.Contains("baseline_and_actual_metric_must_match", result.AbstainReason);
    }

    [Fact]
    public void T039_Abstains_WhenSourceLinkIsMissing()
    {
        var request = DemoRequest() with
        {
            SourceRecommendationId = null,
            SourceValueImpactId = null
        };

        var result = new ValueRealizationService().Calculate(request);

        Assert.True(result.IsAbstained);
        Assert.Contains("source_recommendation_or_value_impact_link_required", result.AbstainReason);
    }

    [Fact]
    public void T039_WorseActualPerformance_CreatesNegativeTrackedValue()
    {
        var result = new ValueRealizationService().Calculate(DemoRequest(actualValue: 120m));

        Assert.False(result.IsAbstained);
        Assert.Equal(-20m, result.ImprovementUnits);
        Assert.Equal(-4_000m, result.RealizedLow);
        Assert.Equal(-3_000m, result.RealizedExpected);
        Assert.Equal(-2_000m, result.RealizedHigh);
        Assert.Equal("NegativeTrackedValue", result.Status);
        Assert.True(result.IsMonotonic);
    }
}
