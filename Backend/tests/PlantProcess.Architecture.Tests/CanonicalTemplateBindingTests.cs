using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

// Canonical template binding gate.
// A seeded template must not advertise a pairing the executor cannot group by.
// defectRate projects no Equipment and no DefectType member, and a donut of one
// effective category is refused by the chart grammar.
[Trait("Gate", "CanonicalTemplateBinding")]
public sealed class CanonicalTemplateBindingTests
{
    private const string AuthorityPath =
        "Backend/PlantProcess.Application/Dashboarding/Services/Dashboards/DashboardDefinitionService.cs";

    [Theory]
    [InlineData("CORR_DEFECT_RATE_BY_EQUIPMENT")]
    [InlineData("CORR_DEFECT_RATE_BY_TYPE")]
    public void Retired_widgets_are_not_seeded(string widgetCode)
    {
        Assert.DoesNotContain(widgetCode, Source(), StringComparison.Ordinal);
    }

    [Fact]
    public void Risk_by_class_declares_a_chart_that_survives_one_category()
    {
        var match = Regex.Match(
            Source(),
            @"TemplateWidget\(\s*""RISK_BY_CLASS""\s*,\s*""[^""]*""\s*,\s*""([a-z]+)""",
            RegexOptions.Singleline);

        Assert.True(match.Success, "RISK_BY_CLASS is no longer seeded by the canonical authority.");

        Assert.Equal("bar", match.Groups[1].Value);
    }

    [Fact]
    public void No_seeded_widget_pairs_defect_rate_with_an_uncarried_dimension()
    {
        var offenders = Regex.Matches(
                Source(),
                @"TemplateWidget\([^)]*Dimensions\.(Equipment|DefectType)[^)]*Measures\.DefectRate[^)]*\)",
                RegexOptions.Singleline)
            .Select(m => m.Value)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "defectRate is a per-material indicator and its source assigns neither Equipment nor DefectType. " +
            "Seeding that pairing advertises a grouping the executor must refuse:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void No_seeded_widget_uses_a_parameter_dependent_measure()
    {
        var source = Source();

        foreach (var measure in new[] { "AvgParameterValue", "MaxParameterValue", "MinParameterValue" })
        {
            Assert.DoesNotContain("Measures." + measure, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Every_seeded_dashboard_belongs_to_the_canonical_family()
    {
        var codes = Regex.Matches(Source(), @"dashboardCode:\s*""([A-Z0-9_]+)""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        var offenders = codes.Where(c => !c.StartsWith("SYSTEM_", StringComparison.Ordinal)).ToList();

        Assert.True(
            offenders.Count == 0,
            "A seeded dashboard sits outside the canonical family:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static string Source()
    {
        var full = Path.Combine(FindRepositoryRoot(), AuthorityPath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(full), "Missing: " + AuthorityPath);

        var text = File.ReadAllText(full);
        var withoutBlocks = Regex.Replace(text, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"//[^\r\n]*", string.Empty);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                Directory.Exists(Path.Combine(directory.FullName, "Backend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }
}