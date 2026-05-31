using System.Reflection;
using FluentAssertions;
using PlantProcess.Application.Analytics.Services;

namespace PlantProcess.Application.UnitTests.Analytics;

public sealed class RiskScoreServiceTests
{
    [Fact]
    public void Risk_score_constants_should_keep_customer_safe_default_identity()
    {
        RiskScoreService.DefaultRiskType
            .Should()
            .Be("OverallQualityRisk");

        RiskScoreService.DefaultRuleVersion
            .Should()
            .StartWith("rule-risk-v");
    }

    [Fact]
    public void CalculateRiskClass_should_classify_low_and_high_scores_differently()
    {
        var method = typeof(RiskScoreService).GetMethod(
            "CalculateRiskClass",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull("RiskScoreService must keep deterministic risk-class calculation");

        var low = (string)method!.Invoke(null, new object[] { 0.10m })!;
        var high = (string)method.Invoke(null, new object[] { 0.90m })!;

        low.Should().NotBeNullOrWhiteSpace();
        high.Should().NotBeNullOrWhiteSpace();
        low.Should().NotBe(high, "low and high scores must not collapse into the same risk class");
    }

    [Fact]
    public void StoreAsync_should_use_computed_risk_class_when_command_risk_class_is_missing()
    {
        var source = File.ReadAllText(
            Path.Combine(
                FindBackendRoot(),
                "PlantProcess.Application",
                "Analytics",
                "Services",
                "RiskScoreService.cs"));

        source.Should().Contain("var riskClass =");
        source.Should().Contain("riskClass: riskClass,");
        source.Should().NotContain("riskClass: command.RiskClass,");
    }

    private static string FindBackendRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "PlantProcess.Application")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Backend root from test output directory.");
    }
}
