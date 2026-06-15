using PlantProcess.Application.Security.Rbac;
using Xunit;

namespace PlantProcess.Application.UnitTests.Security;

/// <summary>PPIQ-503: role-scoped view/edit matrix - page+edit scope per role, tier gating, denial.</summary>
public sealed class Phase5_RoleScopeMatrixTests
{
    private static bool Allowed(FormalPlantRole role, CommercialTier tier, PlantCapability cap)
        => FormalRoleAccessMatrix.Resolve(role, tier, cap).Allowed;

    [Fact]
    public void PPIQ_503_Operator_cannot_open_configuration_or_roi()
    {
        Assert.False(Allowed(FormalPlantRole.Operator, CommercialTier.Enterprise, PlantCapability.ConnectorConfiguration));
        Assert.False(Allowed(FormalPlantRole.Operator, CommercialTier.Enterprise, PlantCapability.RoiKpiView));
    }

    [Fact]
    public void PPIQ_503_Executive_sees_roi_but_not_investigation()
    {
        Assert.True(Allowed(FormalPlantRole.Executive, CommercialTier.Enterprise, PlantCapability.RoiKpiView));
        Assert.False(Allowed(FormalPlantRole.Executive, CommercialTier.Enterprise, PlantCapability.EngineeringInvestigationView));
    }

    [Fact]
    public void PPIQ_503_Engineer_sees_investigation_and_admin_sees_config()
    {
        Assert.True(Allowed(FormalPlantRole.ProcessEngineer, CommercialTier.Professional, PlantCapability.EngineeringInvestigationView));
        Assert.True(Allowed(FormalPlantRole.PlantAdmin, CommercialTier.Enterprise, PlantCapability.ConnectorConfiguration));
    }

    [Fact]
    public void PPIQ_503_Tier_gates_even_when_the_role_grants_it()
    {
        // Executive's role grants ROI, but the Starter tier does not: tier wins, access denied.
        Assert.False(Allowed(FormalPlantRole.Executive, CommercialTier.Starter, PlantCapability.RoiKpiView));
    }

    [Fact]
    public void PPIQ_503_Matrix_is_self_consistent_under_enterprise()
    {
        foreach (var row in FormalRoleAccessMatrix.Matrix())
            foreach (var cap in row.AllowedCapabilities)
                if (cap != PlantCapability.DeveloperDiagnostics)
                    Assert.True(
                        FormalRoleAccessMatrix.Resolve(row.Role, CommercialTier.Enterprise, cap).Allowed,
                        $"{row.Role} must resolve {cap} under Enterprise.");
    }
}