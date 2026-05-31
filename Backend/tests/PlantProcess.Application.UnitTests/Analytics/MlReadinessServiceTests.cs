using System.Reflection;
using FluentAssertions;
using PlantProcess.Application.Analytics.Services;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class MlReadinessServiceTests
{
    [Theory]
    [InlineData(100, 100, true, "Ready")]
    [InlineData(99, 100, false, "NotReady")]
    public void Metric_should_pass_only_when_current_value_reaches_required_threshold(
        decimal current,
        decimal required,
        bool expectedReady,
        string expectedStatus)
    {
        var method = typeof(MlReadinessService).GetMethod(
            "Metric",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("readiness lower-bound gates must stay deterministic");

        var metric = method!.Invoke(null, new object[]
        {
            "sample-count",
            "Sample count",
            current,
            required,
            "rows",
            "Need enough samples"
        });

        metric.Should().NotBeNull();
        Get<bool>(metric!, "IsReady").Should().Be(expectedReady);
        Get<string>(metric!, "Status").Should().Be(expectedStatus);
    }

    [Theory]
    [InlineData(5, 10, true, "Ready")]
    [InlineData(11, 10, false, "NotReady")]
    public void MetricMax_should_pass_only_when_current_value_is_within_maximum_allowed(
        decimal current,
        decimal maximumAllowed,
        bool expectedReady,
        string expectedStatus)
    {
        var method = typeof(MlReadinessService).GetMethod(
            "MetricMax",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("readiness upper-bound gates must stay deterministic");

        var metric = method!.Invoke(null, new object[]
        {
            "missing-rate",
            "Missing rate",
            current,
            maximumAllowed,
            "%",
            "Missing values must be controlled"
        });

        metric.Should().NotBeNull();
        Get<bool>(metric!, "IsReady").Should().Be(expectedReady);
        Get<string>(metric!, "Status").Should().Be(expectedStatus);
    }

    private static T Get<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        property.Should().NotBeNull($"metric property {propertyName} must exist");
        return (T)property!.GetValue(instance)!;
    }
}
