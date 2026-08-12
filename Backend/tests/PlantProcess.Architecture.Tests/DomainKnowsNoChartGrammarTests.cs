using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-046 PACK 4B1. THE DOMAIN MUST NOT LEARN THE GRAMMAR.
///
/// Making the dimension optional invites the obvious shortcut:
///
///     if (chartType == "kpi") { dimension optional; }
///
/// That would put a second copy of the chart grammar inside the entity, which
/// is exactly the duplication Packs 1 to 3A spent their effort removing. The
/// entity answers "can this be stored"; the validator answers "does this mean
/// anything".
/// </summary>
public sealed class DomainKnowsNoChartGrammarTests
{
    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "could not locate the repository root");
        return dir!.FullName;
    }

    private static string EntityCode()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "Backend", "PlantProcess.Domain", "Entities", "Dashboarding",
            "DashboardWidgetDefinition.cs");

        Assert.True(File.Exists(path), "the widget definition entity is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", string.Empty);
    }

    [Fact]
    public void The_entity_names_no_chart_type()
    {
        var code = EntityCode();

        foreach (var chart in new[]
        {
            "kpi", "bar", "line", "area", "pie", "donut", "scatter", "heatmap",
            "pareto", "table", "gauge", "waterfall", "histogram", "boxPlot",
            "combo", "stackedColumn", "pivotTable",
        })
        {
            Assert.False(
                Regex.IsMatch(code, "\"" + Regex.Escape(chart) + "\"", RegexOptions.IgnoreCase),
                "the entity names the chart type '" + chart + "', which makes it a second chart grammar");
        }
    }

    /// <summary>
    /// It must not reach for the grammar either. Importing it would be the same
    /// duplication with an extra step.
    /// </summary>
    [Fact]
    public void The_entity_does_not_consult_the_grammar_or_the_registries()
    {
        var code = EntityCode();

        foreach (var authority in new[]
        {
            "DashboardChartGrammar", "DashboardDimensionRegistry",
            "DashboardWidgetQuerySafetyRegistry", "DashboardMetadataCodes",
        })
        {
            Assert.DoesNotContain(authority, code, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// And the universal guard must stay gone. A blank dimension is a legal
    /// shape for a chart type that does not group.
    /// </summary>
    [Fact]
    public void The_entity_no_longer_demands_a_dimension()
    {
        var code = EntityCode();

        Assert.DoesNotContain("Dimension code is required.", code, StringComparison.Ordinal);

        // The measure requirement is deliberately untouched by this pack.
        Assert.Contains("Measure code is required.", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// The rule did not vanish - it moved to the layer that owns it.
    /// </summary>
    [Fact]
    public void The_validator_still_decides_whether_a_chart_needs_a_dimension()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Widgets",
            "DashboardWidgetValidationService.cs");

        var code = File.ReadAllText(path);

        Assert.Contains("ChartRequiresDimension", code, StringComparison.Ordinal);
        Assert.Contains("Dimension code is required for this chart type.", code, StringComparison.Ordinal);
    }
}