using FluentAssertions;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Widgets;

namespace PlantProcess.Application.UnitTests.Dashboarding;

/// <summary>
/// Backlog origin: T-202. Release: M2. Regression cover for the defect the
/// persisted-definition replay gate found against ppiq_app: five product-seeded
/// system-template widgets stored PascalCase measure codes, passed the
/// case-insensitive validator, and were then refused by the case-sensitive
/// execution dispatch as "published but this engine cannot execute it".
/// </summary>
public sealed class MeasureCodeCanonicalisationTests
{
    private static DashboardWidgetQueryDto Query(string? measureCode) =>
        new(
            WidgetType: "chart",
            ChartType: "bar",
            DimensionCode: "equipment",
            MeasureCode: measureCode,
            ParameterCode: null,
            Filters: null,
            Options: null);

    [Theory]
    [InlineData("MaterialCount", "materialCount")]
    [InlineData("RiskScore", "riskScore")]
    [InlineData("DataQualityIssueCount", "dataQualityIssueCount")]
    [InlineData("DEFECTCOUNT", "defectCount")]
    [InlineData("  materialCount  ", "materialCount")]
    [InlineData("materialCount", "materialCount")]
    public void A_persisted_measure_code_resolves_to_the_registry_spelling(
        string persisted,
        string canonical)
    {
        var result = new DashboardWidgetValidationService().Validate(Query(persisted));

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsValid.Should().BeTrue();
        result.Value!.ResolvedWidget!.MeasureCode.Should().Be(canonical);
    }

    [Fact]
    public void Canonicalisation_never_widens_the_supported_set()
    {
        DashboardWidgetQuerySafetyRegistry
            .IsSupportedMeasure("__no_such_measure__")
            .Should().BeFalse();

        DashboardWidgetQuerySafetyRegistry
            .CanonicaliseMeasure("__no_such_measure__")
            .Should().Be("__no_such_measure__");

        var result = new DashboardWidgetValidationService().Validate(Query("__no_such_measure__"));
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Canonicalisation_leaves_absent_codes_alone()
    {
        DashboardWidgetQuerySafetyRegistry.CanonicaliseMeasure(null).Should().BeNull();
        DashboardWidgetQuerySafetyRegistry.CanonicaliseMeasure("   ").Should().Be("   ");
    }

    /// <summary>
    /// The tempting wrong fix is to make the execution dispatch
    /// case-insensitive. That lets a non-canonical code past the guard and into
    /// the measure switch, whose default arm returns an empty array with HTTP
    /// 200 - a widget that silently shows nothing instead of refusing by name.
    /// This test fails if that guard is ever relaxed.
    /// </summary>
    [Fact]
    public void The_execution_dispatch_stays_case_sensitive()
    {
        var source = File.ReadAllText(ExecutionDispatchSourcePath());

        source.Should().Contain(
            "ExecutableMeasures = new(StringComparer.Ordinal)",
            "the dispatch must keep matching ordinally; canonicalisation happens at validation");
    }

    private static string ExecutionDispatchSourcePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null &&
               !Directory.Exists(Path.Combine(dir.FullName, "Backend", "PlantProcess.Application")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
            throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);

        return Path.Combine(
            dir.FullName,
            "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Queries",
            "DashboardWidgetQueryService.cs");
    }
}