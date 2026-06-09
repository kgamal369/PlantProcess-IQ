using PlantProcess.Application.Security.Rbac;
using Xunit;

namespace PlantProcess.Application.UnitTests.Security;

public sealed class Phase9RoleAccessMatrixTests
{
    [Fact]
    public void T051_Every_Formal_Role_Has_A_Matrix_Row()
    {
        var rows = FormalRoleAccessMatrix.Matrix();

        foreach (var role in Enum.GetValues<FormalPlantRole>())
        {
            Assert.Contains(rows, x => x.Role == role);
        }
    }

    [Fact]
    public void T051_Executive_Can_View_Decision_Dashboard_But_Not_Admin_Config()
    {
        var dashboard = FormalRoleAccessMatrix.Resolve(
            FormalPlantRole.Executive,
            CommercialTier.Enterprise,
            PlantCapability.ExecutiveDashboardView);

        var admin = FormalRoleAccessMatrix.Resolve(
            FormalPlantRole.Executive,
            CommercialTier.Enterprise,
            PlantCapability.AdminUserManagement);

        Assert.True(dashboard.Allowed);
        Assert.False(admin.Allowed);
        Assert.Contains("does not grant", admin.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void T051_Maintenance_Engineer_Cannot_Access_Developer_Diagnostics()
    {
        var decision = FormalRoleAccessMatrix.Resolve(
            FormalPlantRole.MaintenanceEngineer,
            CommercialTier.Developer,
            PlantCapability.DeveloperDiagnostics);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void T051_Tier_Can_Block_A_Role_Capability()
    {
        var decision = FormalRoleAccessMatrix.Resolve(
            FormalPlantRole.ProcessEngineer,
            CommercialTier.Starter,
            PlantCapability.EngineeringInvestigationView);

        Assert.False(decision.Allowed);
        Assert.Contains("Commercial tier", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CEO", FormalPlantRole.ChiefExecutiveOfficer)]
    [InlineData("admin", FormalPlantRole.PlantAdmin)]
    [InlineData("maintenance_engineer", FormalPlantRole.MaintenanceEngineer)]
    [InlineData("unknown-role", FormalPlantRole.Viewer)]
    public void T051_Role_Parser_Is_Deterministic(string input, FormalPlantRole expected)
    {
        Assert.Equal(expected, FormalRoleAccessMatrix.ParseRole(input));
    }
}