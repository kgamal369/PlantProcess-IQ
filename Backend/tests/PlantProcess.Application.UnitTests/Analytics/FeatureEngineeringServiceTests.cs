using System.Reflection;
using FluentAssertions;
using PlantProcess.Application.Analytics.Services;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class FeatureEngineeringServiceTests
{
    [Fact]
    public void CalculateMinutes_should_return_precise_positive_duration()
    {
        var method = typeof(FeatureEngineeringService).GetMethod(
            "CalculateMinutes",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("duration calculation must stay deterministic");

        var start = new DateTime(2026, 05, 01, 10, 00, 00, DateTimeKind.Utc);
        var end = new DateTime(2026, 05, 01, 10, 12, 30, DateTimeKind.Utc);

        var result = (decimal)method!.Invoke(null, new object?[] { start, end })!;

        result.Should().Be(12.5m);
    }

    [Fact]
    public void CalculateMinutes_should_return_zero_for_invalid_or_missing_window()
    {
        var method = typeof(FeatureEngineeringService).GetMethod(
            "CalculateMinutes",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        var start = new DateTime(2026, 05, 01, 10, 00, 00, DateTimeKind.Utc);
        var end = new DateTime(2026, 05, 01, 09, 59, 59, DateTimeKind.Utc);

        var reversed = (decimal)method!.Invoke(null, new object?[] { start, end })!;
        var missing = (decimal)method.Invoke(null, new object?[] { null, end })!;

        reversed.Should().Be(0m);
        missing.Should().Be(0m);
    }

    [Fact]
    public void CalculateStdDev_should_match_sample_standard_deviation()
    {
        var method = typeof(FeatureEngineeringService).GetMethod(
            "CalculateStdDev",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("standard deviation drives anomaly feature calculations");

        var result = (decimal?)method!.Invoke(null, new object[] { new List<decimal> { 10m, 12m, 14m } });

        result.Should().Be(2m);
    }

    [Fact]
    public void CurrentFeatureVersion_should_mark_rule_ready_vectors()
    {
        FeatureEngineeringService.CurrentFeatureVersion
            .Should()
            .Contain("rule-ready");
    }
}
