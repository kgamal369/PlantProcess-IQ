using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-046 PACK 2. THE METADATA SURFACE MUST SPEAK THE GRAMMAR.
///
/// A semantic rule that exists but is not what the authoring surface reads is
/// decoration. These guards read DashboardMetadataService as CODE, comments
/// stripped, and fail if the curated arrays return or if a refusal loses its
/// reason.
/// </summary>
public sealed class ChartGrammarMetadataWiringTests
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

    private static string MetadataServiceCode()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Metadata",
            "DashboardMetadataService.cs");

        Assert.True(File.Exists(path), "the metadata service is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", string.Empty);
    }

    /// <summary>
    /// The catalogue must come from one place. A hand-written list is how the
    /// tenth entry diverges from the eleventh.
    /// </summary>
    [Fact]
    public void The_chart_catalogue_is_read_from_the_grammar()
    {
        var code = MetadataServiceCode();

        Assert.Contains("DashboardChartGrammar.All", code, StringComparison.Ordinal);

        // A single construction site, inside the projection over the grammar.
        var constructions = Regex.Matches(code, @"new DashboardChartTypeMetadataDto\(").Count;
        Assert.True(
            constructions == 1,
            "the chart catalogue is built at " + constructions + " sites; it must be projected from the grammar exactly once");
    }

    /// <summary>
    /// The curated arrays are what allowed a heatmap on one axis and a share
    /// chart over a single category. They must not come back for chart choice.
    /// </summary>
    [Fact]
    public void Compatibility_is_evaluated_not_intersected()
    {
        var code = MetadataServiceCode();

        Assert.Contains("DashboardChartGrammar.Evaluate", code, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "dimension.CompatibleChartTypes" + Environment.NewLine,
            code,
            StringComparison.Ordinal);
        Assert.False(
            code.Contains(".Intersect(measure.CompatibleChartTypes", StringComparison.Ordinal),
            "chart compatibility is being intersected from curated arrays again");
    }

    /// <summary>
    /// Every refusal reaches the author with a sentence. Without this the
    /// surface can silently omit a type, which is the behaviour that hid the
    /// unselectable Pareto for as long as it existed.
    /// </summary>
    [Fact]
    public void Every_refusal_reaches_the_client_with_its_reason()
    {
        var code = MetadataServiceCode();

        Assert.Contains("DashboardChartRefusalDto", code, StringComparison.Ordinal);
        Assert.Contains("NotYetAvailableReason", code, StringComparison.Ordinal);
        Assert.Contains("verdict.Reason", code, StringComparison.Ordinal);
    }

    /// <summary>
    /// The axis role must be read from the registered dimension, never inferred
    /// from a code that happens to look like a date. A customer names their own
    /// dimensions.
    /// </summary>
    private static string DimensionRegistryCode()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Widgets",
            "DashboardDimensionRegistry.cs");

        Assert.True(File.Exists(path), "the dimension registry is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", string.Empty);
    }

    /// <summary>
    /// REWRITTEN BY PACK 3A, AND THE HISTORY MATTERS. This guard originally
    /// asserted that "dimension.DataType" appears in the metadata service - it
    /// pinned WHERE the rule lived rather than WHAT the rule is. Moving the
    /// implementation into the shared registry, which is the whole point of
    /// 3A, failed it. A guard that breaks when the code is improved is testing
    /// the wrong thing.
    ///
    /// It now asserts the rule in whichever file owns it: the metadata surface
    /// ASKS rather than decides, the one implementation reads a registered data
    /// type, and NEITHER file resolves a role from a dimension code name.
    /// </summary>
    [Fact]
    public void The_axis_role_is_read_from_the_registry_not_from_a_code_name()
    {
        var metadata = MetadataServiceCode();
        var registry = DimensionRegistryCode();

        Assert.Contains("DashboardDimensionRegistry.AxisRoleOf", metadata, StringComparison.Ordinal);
        Assert.Contains("DataType", registry, StringComparison.Ordinal);
        Assert.Contains("AxisRole.Temporal", registry, StringComparison.Ordinal);

        foreach (var literal in new[] { "\"day\"", "\"week\"", "\"month\"", "\"riskClass\"", "\"defectType\"" })
        {
            foreach (var code in new[] { metadata, registry })
            {
                Assert.False(
                    Regex.IsMatch(code, @"AxisRole(Of|OrNone)[\s\S]{0,600}" + Regex.Escape(literal)),
                    "the axis role is being decided by the dimension code " + literal + " rather than by its registered data type");
            }
        }
    }
}