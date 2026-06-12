using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Application.Security.Rbac;

/// <summary>
/// PPIQ_REALIZATION_T051_FORMAL_ROLE_ACCESS_MATRIX.
/// Single source of truth for persona roles, capabilities and effective access.
/// This replaces scattered informal string checks with a formal matrix.
/// </summary>
public enum FormalPlantRole
{
    Executive,
    ChiefExecutiveOfficer,
    ProcessEngineer,
    MaintenanceEngineer,
    Operator,
    Viewer,
    PlantAdmin,
    Developer
}

/// <summary>
/// PPIQ-T08: NOT a license tier. CommercialTier is the RBAC packaging axis
/// (Starter/Professional/Enterprise/Developer) used by the formal role-access matrix.
/// The product license model is LicenseTier (Light/Pro/ProPlus/Enterprise) - do not merge.
/// </summary>
public enum CommercialTier
{
    Starter,
    Professional,
    Enterprise,
    Developer
}

public enum PlantCapability
{
    ExecutiveDashboardView,
    RoiKpiView,
    DowntimeSummaryView,
    QualitySummaryView,
    EngineeringInvestigationView,
    MaterialGenealogyView,
    RecommendationReview,
    AssistantChat,
    AssistantConfiguration,
    ConnectorConfiguration,
    MappingConfiguration,
    DataQualityView,
    AdminUserManagement,
    DeveloperDiagnostics,
    DeploymentAdministration
}

public sealed record RoleAccessDecision(
    FormalPlantRole Role,
    CommercialTier Tier,
    PlantCapability Capability,
    bool Allowed,
    string Reason,
    IReadOnlyList<PlantCapability> EffectiveCapabilities);

public sealed record RoleAccessMatrixRow(
    FormalPlantRole Role,
    string Persona,
    IReadOnlyList<PlantCapability> AllowedCapabilities,
    IReadOnlyList<string> HiddenRoutePrefixes,
    IReadOnlyList<string> DisabledWithReasonRoutePrefixes);

public static class FormalRoleAccessMatrix
{
    private static readonly IReadOnlyDictionary<FormalPlantRole, PlantCapability[]> RoleCapabilities =
        new Dictionary<FormalPlantRole, PlantCapability[]>
        {
            [FormalPlantRole.Executive] =
            [
                PlantCapability.ExecutiveDashboardView,
                PlantCapability.RoiKpiView,
                PlantCapability.DowntimeSummaryView,
                PlantCapability.QualitySummaryView,
                PlantCapability.RecommendationReview,
                PlantCapability.AssistantChat
            ],
            [FormalPlantRole.ChiefExecutiveOfficer] =
            [
                PlantCapability.ExecutiveDashboardView,
                PlantCapability.RoiKpiView,
                PlantCapability.DowntimeSummaryView,
                PlantCapability.QualitySummaryView,
                PlantCapability.RecommendationReview,
                PlantCapability.AssistantChat
            ],
            [FormalPlantRole.ProcessEngineer] =
            [
                PlantCapability.EngineeringInvestigationView,
                PlantCapability.MaterialGenealogyView,
                PlantCapability.QualitySummaryView,
                PlantCapability.RecommendationReview,
                PlantCapability.AssistantChat,
                PlantCapability.DataQualityView
            ],
            [FormalPlantRole.MaintenanceEngineer] =
            [
                PlantCapability.DowntimeSummaryView,
                PlantCapability.EngineeringInvestigationView,
                PlantCapability.AssistantChat,
                PlantCapability.DataQualityView
            ],
            [FormalPlantRole.Operator] =
            [
                PlantCapability.QualitySummaryView,
                PlantCapability.MaterialGenealogyView,
                PlantCapability.AssistantChat
            ],
            [FormalPlantRole.Viewer] =
            [
                PlantCapability.QualitySummaryView,
                PlantCapability.ExecutiveDashboardView
            ],
            [FormalPlantRole.PlantAdmin] =
            [
                PlantCapability.ExecutiveDashboardView,
                PlantCapability.RoiKpiView,
                PlantCapability.DowntimeSummaryView,
                PlantCapability.QualitySummaryView,
                PlantCapability.EngineeringInvestigationView,
                PlantCapability.MaterialGenealogyView,
                PlantCapability.RecommendationReview,
                PlantCapability.AssistantChat,
                PlantCapability.AssistantConfiguration,
                PlantCapability.ConnectorConfiguration,
                PlantCapability.MappingConfiguration,
                PlantCapability.DataQualityView,
                PlantCapability.AdminUserManagement,
                PlantCapability.DeploymentAdministration
            ],
            [FormalPlantRole.Developer] =
            [
                PlantCapability.ExecutiveDashboardView,
                PlantCapability.RoiKpiView,
                PlantCapability.DowntimeSummaryView,
                PlantCapability.QualitySummaryView,
                PlantCapability.EngineeringInvestigationView,
                PlantCapability.MaterialGenealogyView,
                PlantCapability.RecommendationReview,
                PlantCapability.AssistantChat,
                PlantCapability.AssistantConfiguration,
                PlantCapability.ConnectorConfiguration,
                PlantCapability.MappingConfiguration,
                PlantCapability.DataQualityView,
                PlantCapability.AdminUserManagement,
                PlantCapability.DeveloperDiagnostics,
                PlantCapability.DeploymentAdministration
            ]
        };

    private static readonly IReadOnlyDictionary<CommercialTier, PlantCapability[]> TierCapabilities =
        new Dictionary<CommercialTier, PlantCapability[]>
        {
            [CommercialTier.Starter] =
            [
                PlantCapability.ExecutiveDashboardView,
                PlantCapability.QualitySummaryView,
                PlantCapability.DowntimeSummaryView
            ],
            [CommercialTier.Professional] =
            [
                PlantCapability.ExecutiveDashboardView,
                PlantCapability.RoiKpiView,
                PlantCapability.DowntimeSummaryView,
                PlantCapability.QualitySummaryView,
                PlantCapability.EngineeringInvestigationView,
                PlantCapability.MaterialGenealogyView,
                PlantCapability.RecommendationReview,
                PlantCapability.AssistantChat,
                PlantCapability.DataQualityView
            ],
            [CommercialTier.Enterprise] = Enum.GetValues<PlantCapability>()
                .Where(x => x != PlantCapability.DeveloperDiagnostics)
                .ToArray(),
            [CommercialTier.Developer] = Enum.GetValues<PlantCapability>()
        };

    public static IReadOnlyList<RoleAccessMatrixRow> Matrix()
    {
        return Enum.GetValues<FormalPlantRole>()
            .Select(role => new RoleAccessMatrixRow(
                role,
                PersonaFor(role),
                RoleCapabilities[role],
                HiddenRoutesFor(role),
                DisabledRoutesFor(role)))
            .ToArray();
    }

    public static RoleAccessDecision Resolve(
        FormalPlantRole role,
        CommercialTier tier,
        PlantCapability capability)
    {
        var roleCaps = RoleCapabilities[role].ToHashSet();
        var tierCaps = TierCapabilities[tier].ToHashSet();
        var effective = roleCaps.Intersect(tierCaps).OrderBy(x => x.ToString()).ToArray();

        if (!roleCaps.Contains(capability))
        {
            return new RoleAccessDecision(
                role,
                tier,
                capability,
                false,
                $"Role {role} does not grant {capability}.",
                effective);
        }

        if (!tierCaps.Contains(capability))
        {
            return new RoleAccessDecision(
                role,
                tier,
                capability,
                false,
                $"Commercial tier {tier} does not grant {capability}.",
                effective);
        }

        return new RoleAccessDecision(
            role,
            tier,
            capability,
            true,
            "Allowed by formal role/access matrix.",
            effective);
    }

    public static FormalPlantRole ParseRole(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return FormalPlantRole.Viewer;

        var normalized = value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);

        return normalized.ToLowerInvariant() switch
        {
            "ceo" => FormalPlantRole.ChiefExecutiveOfficer,
            "chiefexecutiveofficer" => FormalPlantRole.ChiefExecutiveOfficer,
            "executive" => FormalPlantRole.Executive,
            "processengineer" => FormalPlantRole.ProcessEngineer,
            "maintenanceengineer" => FormalPlantRole.MaintenanceEngineer,
            "operator" => FormalPlantRole.Operator,
            "viewer" => FormalPlantRole.Viewer,
            "plantadmin" => FormalPlantRole.PlantAdmin,
            "admin" => FormalPlantRole.PlantAdmin,
            "developer" => FormalPlantRole.Developer,
            _ => FormalPlantRole.Viewer
        };
    }

    public static CommercialTier ParseTier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return CommercialTier.Enterprise;

        return Enum.TryParse<CommercialTier>(value.Trim(), ignoreCase: true, out var tier)
            ? tier
            : CommercialTier.Enterprise;
    }

    public static PlantCapability ParseCapability(string value)
    {
        if (Enum.TryParse<PlantCapability>(value, ignoreCase: true, out var capability))
            return capability;

        throw new InvalidOperationException($"Unknown capability: {value}");
    }

    public static string PersonaFor(FormalPlantRole role)
        => role switch
        {
            FormalPlantRole.Executive => "executive",
            FormalPlantRole.ChiefExecutiveOfficer => "executive",
            FormalPlantRole.ProcessEngineer => "engineering",
            FormalPlantRole.MaintenanceEngineer => "maintenance",
            FormalPlantRole.Operator => "operations",
            FormalPlantRole.Viewer => "viewer",
            FormalPlantRole.PlantAdmin => "admin",
            FormalPlantRole.Developer => "developer",
            _ => "viewer"
        };

    private static IReadOnlyList<string> HiddenRoutesFor(FormalPlantRole role)
        => role switch
        {
            FormalPlantRole.Executive or FormalPlantRole.ChiefExecutiveOfficer =>
            [
                "/admin",
                "/connectors",
                "/mapping",
                "/developer",
                "/phase8/assistant-config"
            ],
            FormalPlantRole.MaintenanceEngineer =>
            [
                "/admin",
                "/connectors",
                "/mapping",
                "/developer",
                "/phase8/assistant-config"
            ],
            FormalPlantRole.Operator =>
            [
                "/admin",
                "/connectors",
                "/mapping",
                "/developer",
                "/phase8/assistant-config",
                "/investigate/advanced"
            ],
            FormalPlantRole.Viewer =>
            [
                "/admin",
                "/connectors",
                "/mapping",
                "/developer",
                "/phase8/assistant-config",
                "/investigate/advanced",
                "/phase8/suggestions"
            ],
            _ => Array.Empty<string>()
        };

    private static IReadOnlyList<string> DisabledRoutesFor(FormalPlantRole role)
        => role switch
        {
            FormalPlantRole.Executive or FormalPlantRole.ChiefExecutiveOfficer =>
            [
                "/investigate/advanced",
                "/material-investigation"
            ],
            FormalPlantRole.MaintenanceEngineer =>
            [
                "/phase8/suggestions",
                "/roi"
            ],
            FormalPlantRole.Operator =>
            [
                "/phase8/suggestions",
                "/roi"
            ],
            FormalPlantRole.Viewer =>
            [
                "/phase8/suggestions",
                "/roi",
                "/material-investigation"
            ],
            _ => Array.Empty<string>()
        };
}