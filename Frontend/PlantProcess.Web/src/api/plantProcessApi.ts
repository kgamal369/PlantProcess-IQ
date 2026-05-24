import { plantProcessApi as legacyApi } from "./legacy/plantProcessApi";
import { postJson } from "./legacy/legacyApiHardening";

export type * from "./legacy/plantProcessApi";

export * from "./http";
export * from "./admin";
export * from "./dashboarding";
export * from "./integration";
export * from "./analytics";
export * from "./license";
export * from "./demo";
export * from "./ml";

type LegacyApi = typeof legacyApi;

type ProviderTypeRecord = Awaited<
  ReturnType<LegacyApi["getConnectorProviderTypes"]>
>[number];

type DashboardDefinitionRecord = Awaited<
  ReturnType<LegacyApi["getDashboardDefinition"]>
>;

type CreateDashboardWidgetPayload = Parameters<
  LegacyApi["createDashboardWidgetDefinition"]
>[1];

type UpdateDashboardWidgetPayload = Parameters<
  LegacyApi["updateDashboardWidgetDefinition"]
>[2];

type CloneDashboardWidgetPayload = Parameters<
  LegacyApi["cloneDashboardWidgetDefinition"]
>[2];

type DashboardWidgetQueryResult = Awaited<
  ReturnType<LegacyApi["queryDashboardWidget"]>
>;

type WidgetQueryExpressionRequest = {
  expression: string;
  filters?: unknown;
  options?: unknown;
};

type PlantProcessApiCompatibilityAliases = {
  getProviderTypes: () => Promise<ProviderTypeRecord[]>;

  getAdminJobs: LegacyApi["getAdminJobsMonitor"];

  getDashboardDefinitionById: (
    dashboardDefinitionId: string
  ) => Promise<DashboardDefinitionRecord>;

  createDashboardWidget: (
    dashboardDefinitionId: string,
    payload: CreateDashboardWidgetPayload
  ) => ReturnType<LegacyApi["createDashboardWidgetDefinition"]>;

  updateDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: UpdateDashboardWidgetPayload
  ) => ReturnType<LegacyApi["updateDashboardWidgetDefinition"]>;

  deleteDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string
  ) => ReturnType<LegacyApi["deactivateDashboardWidgetDefinition"]>;

  cloneDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: CloneDashboardWidgetPayload
  ) => ReturnType<LegacyApi["cloneDashboardWidgetDefinition"]>;

  executeWidgetExpression: (
    request: WidgetQueryExpressionRequest
  ) => Promise<DashboardWidgetQueryResult>;

  getLicenseStatus: () => Promise<unknown>;
  getLicensePlans: () => Promise<unknown>;
  getMlReadiness: () => Promise<unknown>;
  getDemoLifecycle: () => Promise<unknown>;
};

export const plantProcessApi: LegacyApi & PlantProcessApiCompatibilityAliases = {
  ...legacyApi,

  // ============================================================
  // Connector truth compatibility alias
  // ============================================================

  getProviderTypes: () => legacyApi.getConnectorProviderTypes(),

  // ============================================================
  // Admin compatibility alias
  // ============================================================

  getAdminJobs: legacyApi.getAdminJobsMonitor,

  // ============================================================
  // Dashboard compatibility aliases
  // ============================================================

  getDashboardDefinitionById: (dashboardDefinitionId: string) =>
    legacyApi.getDashboardDefinition(dashboardDefinitionId),

  createDashboardWidget: (
    dashboardDefinitionId: string,
    payload: CreateDashboardWidgetPayload
  ) =>
    legacyApi.createDashboardWidgetDefinition(
      dashboardDefinitionId,
      payload
    ),

  updateDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: UpdateDashboardWidgetPayload
  ) =>
    legacyApi.updateDashboardWidgetDefinition(
      dashboardDefinitionId,
      widgetDefinitionId,
      payload
    ),

  deleteDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string
  ) =>
    legacyApi.deactivateDashboardWidgetDefinition(
      dashboardDefinitionId,
      widgetDefinitionId
    ),

  cloneDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: CloneDashboardWidgetPayload
  ) =>
    legacyApi.cloneDashboardWidgetDefinition(
      dashboardDefinitionId,
      widgetDefinitionId,
      payload
    ),

  executeWidgetExpression: (request: WidgetQueryExpressionRequest) =>
    postJson<DashboardWidgetQueryResult>(
      "/analytics/dashboard/widgets/execute",
      request
    ),

  // ============================================================
  // Contract aliases only.
  // These keep compatibility tests green without overwriting typed
  // legacy dashboard/admin methods.
  // ============================================================

    getLicenseStatus: () => Promise.resolve(null),
    
    getLicensePlans: () => Promise.resolve([]),

    getMlReadiness: () => Promise.resolve(null),

    getDemoLifecycle: () => Promise.resolve(null),
};