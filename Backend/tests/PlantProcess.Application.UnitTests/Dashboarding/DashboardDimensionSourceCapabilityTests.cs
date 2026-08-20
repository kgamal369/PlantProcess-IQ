using PlantProcess.Application.Dashboarding.Services.Queries;
using Xunit;

namespace PlantProcess.Application.UnitTests.Dashboarding;

/// <summary>
/// DEMO-BI-R1. The unhandled 500 on every workspace opening state.
///
/// MEASURED 19 Aug 2026, not inferred. The API log carried:
///
///   System.InvalidOperationException: The LINQ expression
///   'DbSet&lt;ParameterObservation&gt;() ... .GroupBy(ti1 =&gt; new DashboardGroupKey{
///   Text = new WidgetFact{ MaterialUnitId = ..., ParameterCode = ...,
///   EventTimeUtc = ..., Value = 1 }.RiskClass })' could not be translated.
///
/// WidgetFact uses init-only members precisely so EF can fold
/// "new WidgetFact { EquipmentId = col }.EquipmentId" down to a column. That
/// fold is impossible for a member the initialiser never assigns - there is no
/// column to fold to - so the query reached PostgreSQL untranslatable and threw.
///
/// The parameter-observation source carries no risk class, no shift and no
/// defect type. The associative strip enumerates EVERY dimension against
/// measure observationCount, so those three produced a 500 on the opening state
/// of all 41 workspaces, and the toast the customer saw. They are also exactly
/// the three columns that strip renders as N/A.
/// </summary>
public sealed class DashboardDimensionSourceCapabilityTests
{
    /// <summary>The member set of the projection named in the stack trace.</summary>
    private static readonly string[] ParameterObservationMembers =
    {
        "MaterialUnitId", "SiteId", "AreaId", "EquipmentId", "MaterialCode",
        "MaterialUnitType", "ProductFamily", "GradeOrRecipe", "SourceSystem",
        "ParameterCode", "EventTimeUtc", "Value",
    };

    [Theory]
    [InlineData("riskClass", "RiskClass")]
    [InlineData("shiftCode", "ShiftCode")]
    [InlineData("defectType", "DefectType")]
    [InlineData("equipment", "EquipmentId")]
    [InlineData("site", "SiteId")]
    [InlineData("area", "AreaId")]
    [InlineData("parameterCode", "ParameterCode")]
    [InlineData("day", "EventTimeUtc")]
    [InlineData("week", "EventTimeUtc")]
    [InlineData("month", "EventTimeUtc")]
    public void Each_dimension_names_the_one_member_it_groups_on(string dimensionCode, string expected)
    {
        Assert.Equal(expected, DashboardSourceCapability.RequiredMemberName(dimensionCode));
    }

    [Theory]
    [InlineData("riskClass")]
    [InlineData("shiftCode")]
    [InlineData("defectType")]
    public void A_dimension_the_source_cannot_carry_is_refused(string dimensionCode)
    {
        Assert.False(DashboardSourceCapability.IsCarried(dimensionCode, ParameterObservationMembers));
    }

    [Theory]
    [InlineData("equipment")]
    [InlineData("site")]
    [InlineData("area")]
    [InlineData("sourceSystem")]
    [InlineData("materialUnitType")]
    [InlineData("productFamily")]
    [InlineData("gradeOrRecipe")]
    [InlineData("parameterCode")]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    public void A_dimension_the_source_does_carry_is_allowed(string dimensionCode)
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

    /// <summary>
    /// A shape the walker cannot read reports null, and an unreadable shape is
    /// never second-guessed. Refusing a working widget would be worse than the
    /// defect this guard exists to stop.
    /// </summary>
    [Theory]
    [InlineData("riskClass")]
    [InlineData("equipment")]
    public void An_unreadable_source_shape_stands_the_guard_down(string dimensionCode)
    {
        Assert.True(DashboardSourceCapability.IsCarried(dimensionCode, null));
    }

    [Fact]
    public void An_unknown_dimension_code_requires_no_member_here()
    {
        // Registration is a different authority and refuses separately by name.
        Assert.Null(DashboardSourceCapability.RequiredMemberName("not_a_dimension"));
    }
}
