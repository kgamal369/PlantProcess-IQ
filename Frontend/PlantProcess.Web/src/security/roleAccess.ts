// ============================================================
// FILE: src/security/roleAccess.ts
//
// THIS MAP IS DESCRIPTIVE, NOT ENFORCING.
//
// Its only consumers are PersonaAccessMatrixPage (a read-only display) and this
// module's own tests. No router guard and no layout calls it. Authorization is
// enforced server-side by AccessControlMiddleware
// (Backend/PlantProcess.Api/Security/PlantAccessControl.cs), which denies any
// path not present in its matrix.
//
// Keep the route keys in step with App.tsx. When a route is renamed, a stale key
// here does not lock anyone out - it silently disappears from the matrix a
// customer is reading. That is the failure mode to guard against.
// ============================================================

export type FormalPlantRole =
  | "Executive"
  | "ChiefExecutiveOfficer"
  | "ProcessEngineer"
  | "MaintenanceEngineer"
  | "Operator"
  | "Viewer"
  | "PlantAdmin"
  | "Developer";

export type CommercialTier = "Starter" | "Professional" | "Enterprise" | "Developer";

export type PlantCapability =
  | "ExecutiveDashboardView"
  | "RoiKpiView"
  | "DowntimeSummaryView"
  | "QualitySummaryView"
  | "EngineeringInvestigationView"
  | "MaterialGenealogyView"
  | "RecommendationReview"
  | "AssistantChat"
  | "AssistantConfiguration"
  | "ConnectorConfiguration"
  | "MappingConfiguration"
  | "DataQualityView"
  | "AdminUserManagement"
  | "DeveloperDiagnostics"
  | "DeploymentAdministration";

export type RouteDecision = {
  path: string;
  allowed: boolean;
  disabled: boolean;
  reason: string;
  capability: PlantCapability | null;
};

const roleCapabilities: Record<FormalPlantRole, PlantCapability[]> = {
  Executive: ["ExecutiveDashboardView", "RoiKpiView", "DowntimeSummaryView", "QualitySummaryView", "RecommendationReview", "AssistantChat"],
  ChiefExecutiveOfficer: ["ExecutiveDashboardView", "RoiKpiView", "DowntimeSummaryView", "QualitySummaryView", "RecommendationReview", "AssistantChat"],
  ProcessEngineer: ["EngineeringInvestigationView", "MaterialGenealogyView", "QualitySummaryView", "RecommendationReview", "AssistantChat", "DataQualityView"],
  MaintenanceEngineer: ["DowntimeSummaryView", "EngineeringInvestigationView", "AssistantChat", "DataQualityView"],
  Operator: ["QualitySummaryView", "MaterialGenealogyView", "AssistantChat"],
  Viewer: ["QualitySummaryView", "ExecutiveDashboardView"],
  PlantAdmin: [
    "ExecutiveDashboardView", "RoiKpiView", "DowntimeSummaryView", "QualitySummaryView",
    "EngineeringInvestigationView", "MaterialGenealogyView", "RecommendationReview", "AssistantChat",
    "AssistantConfiguration", "ConnectorConfiguration", "MappingConfiguration", "DataQualityView",
    "AdminUserManagement", "DeploymentAdministration"
  ],
  Developer: [
    "ExecutiveDashboardView", "RoiKpiView", "DowntimeSummaryView", "QualitySummaryView",
    "EngineeringInvestigationView", "MaterialGenealogyView", "RecommendationReview", "AssistantChat",
    "AssistantConfiguration", "ConnectorConfiguration", "MappingConfiguration", "DataQualityView",
    "AdminUserManagement", "DeveloperDiagnostics", "DeploymentAdministration"
  ],
};

const tierCapabilities: Record<CommercialTier, PlantCapability[]> = {
  Starter: ["ExecutiveDashboardView", "QualitySummaryView", "DowntimeSummaryView"],
  Professional: [
    "ExecutiveDashboardView", "RoiKpiView", "DowntimeSummaryView", "QualitySummaryView",
    "EngineeringInvestigationView", "MaterialGenealogyView", "RecommendationReview", "AssistantChat", "DataQualityView"
  ],
  Enterprise: [
    "ExecutiveDashboardView", "RoiKpiView", "DowntimeSummaryView", "QualitySummaryView",
    "EngineeringInvestigationView", "MaterialGenealogyView", "RecommendationReview", "AssistantChat",
    "AssistantConfiguration", "ConnectorConfiguration", "MappingConfiguration", "DataQualityView",
    "AdminUserManagement", "DeploymentAdministration"
  ],
  Developer: [
    "ExecutiveDashboardView", "RoiKpiView", "DowntimeSummaryView", "QualitySummaryView",
    "EngineeringInvestigationView", "MaterialGenealogyView", "RecommendationReview", "AssistantChat",
    "AssistantConfiguration", "ConnectorConfiguration", "MappingConfiguration", "DataQualityView",
    "AdminUserManagement", "DeveloperDiagnostics", "DeploymentAdministration"
  ],
};

export const routeCapabilityMap: Record<string, PlantCapability> = {
  "/executive": "ExecutiveDashboardView",
  "/roi": "RoiKpiView",
  "/suggestions": "RecommendationReview",
  "/assistant": "AssistantChat",
  "/assistant/configuration": "AssistantConfiguration",
  "/admin": "AdminUserManagement",
  "/data-integration/connections": "ConnectorConfiguration",
  "/data-integration/registry": "MappingConfiguration",
  "/developer": "DeveloperDiagnostics",
  "/data-quality": "DataQualityView",
  "/materials": "EngineeringInvestigationView",
};

export function canAccess(role: FormalPlantRole, tier: CommercialTier, capability: PlantCapability) {
  return roleCapabilities[role].includes(capability) && tierCapabilities[tier].includes(capability);
}

export function personaFor(role: FormalPlantRole) {
  if (role === "Executive" || role === "ChiefExecutiveOfficer") return "executive";
  if (role === "MaintenanceEngineer") return "maintenance";
  if (role === "ProcessEngineer") return "engineering";
  if (role === "PlantAdmin") return "admin";
  if (role === "Developer") return "developer";
  if (role === "Operator") return "operations";
  return "viewer";
}

export function routeDecision(path: string, role: FormalPlantRole, tier: CommercialTier): RouteDecision {
  const capability = routeCapabilityMap[path];

  // DENY BY DEFAULT. This used to fall back to "QualitySummaryView", a capability
  // every role and every tier holds - so an unmapped route was granted to
  // everyone. A permission map whose default is "yes" is not a permission map.
  if (!capability) {
    return {
      path,
      capability: null,
      allowed: false,
      disabled: true,
      reason: "Route is not described in the access matrix; shown as denied by default.",
    };
  }

  const roleAllows = roleCapabilities[role].includes(capability);
  const tierAllows = tierCapabilities[tier].includes(capability);

  if (roleAllows && tierAllows) {
    return {
      path,
      capability,
      allowed: true,
      disabled: false,
      reason: "Allowed by formal role/access matrix.",
    };
  }

  return {
    path,
    capability,
    allowed: false,
    disabled: true,
    reason: !roleAllows
      ? `Role ${role} does not grant ${capability}.`
      : `Tier ${tier} does not grant ${capability}.`,
  };
}

export function visibleNavigation(role: FormalPlantRole, tier: CommercialTier) {
  return Object.keys(routeCapabilityMap).map((path) => routeDecision(path, role, tier));
}

export function executiveDashboardCards() {
  return [
    { code: "value", title: "Value / ROI", value: "EUR 28k-56k", caveat: "Projected value range; not causal attribution." },
    { code: "quality", title: "Quality risk", value: "Ready for review", caveat: "Decision-level summary only." },
    { code: "downtime", title: "Downtime", value: "Monitored", caveat: "Summary-level operational signal." },
    { code: "recommendations", title: "Recommendations", value: "Human approval required", caveat: "No automatic process write-back." },
  ];
}