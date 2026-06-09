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
  capability: PlantCapability;
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
  "/phase9/executive": "ExecutiveDashboardView",
  "/executive": "ExecutiveDashboardView",
  "/roi": "RoiKpiView",
  "/phase8/suggestions": "RecommendationReview",
  "/phase8/assistant": "AssistantChat",
  "/phase8/assistant-config": "AssistantConfiguration",
  "/admin": "AdminUserManagement",
  "/connectors": "ConnectorConfiguration",
  "/mapping": "MappingConfiguration",
  "/developer": "DeveloperDiagnostics",
  "/data-quality": "DataQualityView",
  "/material-investigation": "EngineeringInvestigationView",
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
  const capability = routeCapabilityMap[path] ?? "QualitySummaryView";
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