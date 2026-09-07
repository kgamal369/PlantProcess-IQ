using PlantProcess.Application.Dashboarding.Services.Queries;
using Xunit;

namespace PlantProcess.Application.UnitTests.Dashboarding;

/// <summary>
/// The executor carries only product-owned structural/provenance/calendar slots
/// plus ONE generic declared-dimension slot. Customer vocabulary is never a
/// WidgetFact member and therefore cannot become a second compiled catalogue.
/// </summary>
public sealed class DashboardDimensionSourceCapabilityTests
{
    private static readonly string[] ParameterObservationMembers =
    {
        "MaterialUnitId", "SiteId", "AreaId", "EquipmentId", "MaterialCode",
        "MaterialUnitType", "SourceSystem", "ParameterCode", "EventTimeUtc",
        "DimensionText", "Value",
    };

    [Theory]
    [InlineData("equipment", "EquipmentId")]
    [InlineData("site", "SiteId")]
    [InlineData("area", "AreaId")]
    [InlineData("sourceSystem", "SourceSystem")]
    [InlineData("materialUnitType", "MaterialUnitType")]
    [InlineData("parameterCode", "ParameterCode")]
    [InlineData("day", "EventTimeUtc")]
    [InlineData("week", "EventTimeUtc")]
    [InlineData("month", "EventTimeUtc")]
    [InlineData("$declared", "DimensionText")]
    public void Each_executable_grammar_dimension_names_one_generic_member(string dimensionCode, string expected)
    {
        Assert.Equal(expected, DashboardSourceCapability.RequiredMemberName(dimensionCode));
    }

    [Theory]
    [InlineData("defectType")]
    [InlineData("riskClass")]
    [InlineData("shiftCode")]
    [InlineData("productFamily")]
    [InlineData("gradeOrRecipe")]
    public void Customer_vocabulary_is_not_a_compiled_fact_member(string dimensionCode)
    {
        Assert.Null(DashboardSourceCapability.RequiredMemberName(dimensionCode));
    }

    [Theory]
    [InlineData("equipment")]
    [InlineData("site")]
    [InlineData("area")]
    [InlineData("sourceSystem")]
    [InlineData("materialUnitType")]
    [InlineData("parameterCode")]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    [InlineData("$declared")]
    public void A_dimension_the_source_carries_is_allowed(string dimensionCode)
    {
        Assert.True(DashboardSourceCapability.IsCarried(dimensionCode, ParameterObservationMembers));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void The_kpi_grouping_needs_no_member_and_is_always_allowed(string? dimensionCode)
    {
        Assert.Null(DashboardSourceCapability.RequiredMemberName(dimensionCode));
        Assert.True(DashboardSourceCapability.IsCarried(dimensionCode, ParameterObservationMembers));
    }

    [Fact]
    public void An_unreadable_source_shape_stands_the_guard_down()
    {
        Assert.True(DashboardSourceCapability.IsCarried("equipment", null));
    }
}
