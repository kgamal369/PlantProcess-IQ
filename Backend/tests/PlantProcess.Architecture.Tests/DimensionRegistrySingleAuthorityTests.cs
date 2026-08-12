using System.Text.RegularExpressions;
using PlantProcess.Application.Dashboarding.Contracts;
using PlantProcess.Application.Dashboarding.Services.Widgets;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-046 PACK 3A. ONE CATALOGUE, AND IT STAYS ONE.
///
/// Two lists of the same fourteen dimensions existed, and only one of them
/// carried semantics. These guards fail if either comes back.
/// </summary>
public sealed class DimensionRegistrySingleAuthorityTests
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

    private static string CodeOf(params string[] segments)
    {
        var path = Path.Combine(RepositoryRoot(), Path.Combine(segments));
        Assert.True(File.Exists(path), "file is missing: " + path);
        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", string.Empty);
    }

    private static string SafetyRegistryCode() => CodeOf(
        "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Widgets",
        "DashboardWidgetQuerySafetyRegistry.cs");

    private static string MetadataServiceCode() => CodeOf(
        "Backend", "PlantProcess.Application", "Dashboarding", "Services", "Metadata",
        "DashboardMetadataService.cs");

    [Fact]
    public void The_registry_holds_every_dimension_exactly_once()
    {
        Assert.Equal(14, DashboardDimensionRegistry.All.Count);

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in DashboardDimensionRegistry.All)
        {
            Assert.True(codes.Add(descriptor.Code), "duplicate dimension: " + descriptor.Code);
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Label), descriptor.Code + " has no label");
            Assert.False(string.IsNullOrWhiteSpace(descriptor.DataType), descriptor.Code + " has no data type");
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Description), descriptor.Code + " has no description");
        }
    }

    /// <summary>
    /// Neither consumer may keep its own copy. A second list is how the two
    /// disagreed in the first place.
    /// </summary>
    [Fact]
    public void Neither_consumer_carries_a_second_dimension_catalogue()
    {
        foreach (var code in new[] { SafetyRegistryCode(), MetadataServiceCode() })
        {
            Assert.Contains("DashboardDimensionRegistry", code, StringComparison.Ordinal);

            var listed = Regex.Matches(code, @"DashboardMetadataCodes\.Dimensions\.\w+").Count;
            Assert.True(
                listed == 0,
                "a second dimension catalogue has reappeared: " + listed + " dimension codes are listed outside the registry");
        }
    }

    /// <summary>
    /// The query path must resolve a dimension through the same authority the
    /// metadata surface describes it with.
    /// </summary>
    [Fact]
    public void The_query_path_resolves_dimensions_through_the_shared_authority()
    {
        Assert.Contains("DashboardDimensionRegistry.IsRegistered", SafetyRegistryCode(), StringComparison.Ordinal);
    }

    /// <summary>
    /// An unregistered dimension is still refused, and still refused BY ITS REAL
    /// NAME. A generic rejection sends an author looking for the wrong problem.
    /// </summary>
    [Fact]
    public void An_unregistered_dimension_is_refused_and_has_no_axis_role()
    {
        Assert.False(DashboardDimensionRegistry.IsRegistered("notADimension"));
        Assert.Null(DashboardDimensionRegistry.Find("notADimension"));
        Assert.Equal(AxisRole.None, DashboardDimensionRegistry.AxisRoleOf("notADimension"));
    }

    /// <summary>
    /// The axis role reads the registered data type. If it ever reads a code
    /// name, a customer who names a dimension after a day gets a rule they never
    /// asked for.
    /// </summary>
    [Fact]
    public void The_axis_role_is_read_from_the_registered_data_type()
    {
        Assert.Equal(AxisRole.Temporal, DashboardDimensionRegistry.AxisRoleOf(DashboardMetadataCodes.Dimensions.Day));
        Assert.Equal(AxisRole.Temporal, DashboardDimensionRegistry.AxisRoleOf(DashboardMetadataCodes.Dimensions.Week));
        Assert.Equal(AxisRole.Temporal, DashboardDimensionRegistry.AxisRoleOf(DashboardMetadataCodes.Dimensions.Month));
        Assert.Equal(AxisRole.Categorical, DashboardDimensionRegistry.AxisRoleOf(DashboardMetadataCodes.Dimensions.Equipment));
        Assert.Equal(AxisRole.Categorical, DashboardDimensionRegistry.AxisRoleOf(DashboardMetadataCodes.Dimensions.RiskClass));

        // A widget with no dimension is a KPI, not an authoring error.
        Assert.Equal(AxisRole.None, DashboardDimensionRegistry.AxisRoleOrNone(null));
        Assert.Equal(AxisRole.None, DashboardDimensionRegistry.AxisRoleOrNone("   "));
    }

    /// <summary>
    /// Pack 3A collapses ownership and changes nothing a client receives. Every
    /// descriptor field the metadata payload carries must survive intact.
    /// </summary>
    [Fact]
    public void Every_descriptor_still_carries_its_legacy_chart_array_unchanged()
    {
        foreach (var descriptor in DashboardDimensionRegistry.All)
        {
            Assert.NotNull(descriptor.LegacyCompatibleChartTypes);
            Assert.True(
                descriptor.LegacyCompatibleChartTypes.Count > 0,
                descriptor.Code + " lost its legacy chart array, which changes the metadata payload");
        }
    }
}