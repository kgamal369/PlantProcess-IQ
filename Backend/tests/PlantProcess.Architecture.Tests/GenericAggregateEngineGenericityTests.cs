using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// D1 LAYER A GENERICITY GUARD.
///
/// The engine must never learn a customer. This reads the aggregate foundation
/// as CODE - comments stripped - and fails if any dashboard code, widget code
/// or industry noun appears in it. The charter's genericity rule is only worth
/// something if a future shortcut fails a build rather than a review.
///
/// Comments are stripped deliberately: the file explains what it must not do,
/// and prose describing a forbidden construct must never satisfy or trip a
/// guard about that construct. This project has paid for that lesson repeatedly.
/// </summary>
public sealed class GenericAggregateEngineGenericityTests
{
    private static string EngineSource()
    {
        var path = Path.Combine(
            RepositoryRoot(),
            "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Queries",
            "DashboardAggregateExecutor.cs");

        Assert.True(File.Exists(path), "the aggregate foundation is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", "");
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", "");
    }

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

    [Fact]
    public void The_aggregate_engine_contains_no_dashboard_or_widget_identity()
    {
        var source = EngineSource();

        foreach (var forbidden in new[]
        {
            "PO_KPI", "EO_", "QM_", "PA_", "RI_", "CF_", "MI_",
            "PRODUCTION_OVERVIEW", "QUALITY_MONITORING", "EQUIPMENT_OPERATIONS",
            "dashboardCode", "widgetCode", "DashboardCode", "WidgetCode"
        })
        {
            Assert.False(
                source.Contains(forbidden, StringComparison.Ordinal),
                "the generic aggregate engine names a dashboard or widget identity: " + forbidden);
        }
    }

    [Fact]
    public void The_aggregate_engine_contains_no_industry_vocabulary()
    {
        var source = EngineSource();

        foreach (var forbidden in new[]
        {
            "Coil", "Heat", "Slab", "caster", "Caster", "Fleet", "steel", "Steel",
            "hot strip", "pickling", "furnace"
        })
        {
            Assert.False(
                source.Contains(forbidden, StringComparison.Ordinal),
                "the generic aggregate engine names an industry the customer may not be in: " + forbidden);
        }
    }

    [Fact]
    public void The_aggregate_engine_never_caps_the_population_before_aggregating()
    {
        var source = EngineSource();

        Assert.False(source.Contains(".Take(", StringComparison.Ordinal) &&
                     !source.Contains("Take(resolved.MaxRows)", StringComparison.Ordinal),
            "a Take appears in the aggregate engine that is not the aggregate-group MaxRows");

        Assert.False(
            source.Contains("RawRowLimit", StringComparison.Ordinal),
            "the aggregate engine references a raw-row cap; a cap must never define an aggregate population");
    }
}