using System.Text.RegularExpressions;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Widgets;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-045-R1-D. THE TWO QUANTITIES MUST STAY INDEPENDENT.
///
/// The failure this guards is not a crash. If a later edit derived impact from
/// stoppage - a fixed percentage, a fallback when impact is zero, a ratio in
/// place of a magnitude - every chart would still render, every total would
/// still look plausible, and the one thing the surface exists to show would be
/// gone. A behavioural test cannot catch that on a dataset where the two
/// happen to correlate, so the source is asserted as text.
/// </summary>
public sealed class EquipmentImpactIndependenceTests
{
    private static string SourcesCode()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend")))
            dir = dir.Parent;

        Assert.True(dir is not null, "could not locate the repository root");

        var path = Path.Combine(
            dir!.FullName, "Backend", "PlantProcess.Application", "Dashboarding",
            "Services", "Queries", "WidgetResultSources.cs");

        Assert.True(File.Exists(path), "file is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", string.Empty);
    }

    [Fact]
    public void Each_quantity_is_summed_from_its_own_canonical_field()
    {
        var code = SourcesCode();

        Assert.Contains("g.Sum(x => x.StoppedMinutes)", code, StringComparison.Ordinal);
        Assert.Contains("g.Sum(x => x.ProductionImpactMinutes)", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Neither_quantity_is_ever_computed_from_the_other()
    {
        var code = SourcesCode();

        // Any arithmetic mentioning both names in one expression.
        var derivations = new[]
        {
            @"StoppedMinutes\s*[-+*/]\s*[\w.]*ProductionImpactMinutes",
            @"ProductionImpactMinutes\s*[-+*/]\s*[\w.]*StoppedMinutes",
            @"ProductionImpactMinutes\s*=\s*[\w.]*StoppedMinutes",
            @"StoppedMinutes\s*=\s*[\w.]*ProductionImpactMinutes"
        };

        foreach (var pattern in derivations)
        {
            Assert.False(
                Regex.IsMatch(code, pattern),
                "one downtime quantity is being derived from the other: " + pattern);
        }
    }

    [Fact]
    public void The_measure_passes_every_registration_gate()
    {
        var measure = DashboardMetadataCodes.Measures.EquipmentStoppageAndImpact;

        Assert.True(DashboardWidgetQuerySafetyRegistry.IsSupportedMeasure(measure));
        Assert.True(DashboardWidgetQuerySafetyRegistry.MeasureProvidesOwnColumns(measure));
        Assert.False(DashboardWidgetQuerySafetyRegistry.MeasureRequiresParameterCode(measure));
    }

    [Fact]
    public void The_source_is_named_for_its_question_and_not_for_a_chart()
    {
        var code = SourcesCode();

        foreach (var forbidden in new[] { "ComboSource", "ComboWidgetResultSource", "PairedColumnSource" })
            Assert.DoesNotContain(forbidden, code, StringComparison.Ordinal);
    }
}