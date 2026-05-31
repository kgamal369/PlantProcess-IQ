using System.Reflection;
using FluentAssertions;
using PlantProcess.Application.Analytics.Services;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class QualityLabelBuilderServiceTests
{
    [Theory]
    [InlineData(false, false, false, false, null, "ACCEPTED_OR_NO_QUALITY_EVENT")]
    [InlineData(true, false, false, false, null, "DEFECT_OTHER")]
    [InlineData(true, false, false, false, "Surface crack", "DEFECT_SURFACE_CRACK")]
    [InlineData(true, true, false, false, "Surface crack", "REJECTED")]
    [InlineData(true, false, true, false, "Surface crack", "DOWNGRADED")]
    [InlineData(true, false, false, true, "Surface crack", "REWORKED")]
    public void BuildLabelCode_should_apply_quality_label_precedence_and_normalization(
        bool hasDefect,
        bool isRejected,
        bool isDowngraded,
        bool isReworked,
        string? primaryDefectCategory,
        string expected)
    {
        var method = typeof(QualityLabelBuilderService).GetMethod(
            "BuildLabelCode",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("quality labels must keep deterministic precedence");

        var result = (string)method!.Invoke(null, new object?[]
        {
            hasDefect,
            isRejected,
            isDowngraded,
            isReworked,
            primaryDefectCategory
        })!;

        result.Should().Be(expected);
    }
}
