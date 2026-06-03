import { productApi as coreProductApi } from "./productCoreApiClient";
import { getJson, postJson } from "./productApiHardening";

export type * from "./productCoreApiClient";

type CoreProductApi = typeof coreProductApi;

type ProviderTypeRecord = Awaited<
  ReturnType<CoreProductApi["getConnectorProviderTypes"]>
>[number];

type DashboardDefinitionRecordAlias = Awaited<
  ReturnType<CoreProductApi["getDashboardDefinition"]>
>;

type CreateDashboardWidgetPayload = Parameters<
  CoreProductApi["createDashboardWidgetDefinition"]
>[1];

type UpdateDashboardWidgetPayload = Parameters<
  CoreProductApi["updateDashboardWidgetDefinition"]
>[2];

type CloneDashboardWidgetPayload = Parameters<
  CoreProductApi["cloneDashboardWidgetDefinition"]
>[2];

type DashboardWidgetQueryResultAlias = Awaited<
  ReturnType<CoreProductApi["queryDashboardWidget"]>
>;

type WidgetQueryExpressionRequest = {
  dashboardDefinitionId?: string;
  widgetDefinitionId?: string;
  expression?: string;
  query?: unknown;
  options?: unknown;
};

type ProductApiCompatibilityAliases = {
  getProviderTypes: () => Promise<ProviderTypeRecord[]>;

  getAdminJobs: CoreProductApi["getAdminJobsMonitor"];

  getDashboardDefinitionById: (
    dashboardDefinitionId: string
  ) => Promise<DashboardDefinitionRecordAlias>;

  createDashboardWidget: (
    dashboardDefinitionId: string,
    payload: CreateDashboardWidgetPayload
  ) => ReturnType<CoreProductApi["createDashboardWidgetDefinition"]>;

  updateDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: UpdateDashboardWidgetPayload
  ) => ReturnType<CoreProductApi["updateDashboardWidgetDefinition"]>;

  deleteDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string
  ) => ReturnType<CoreProductApi["deactivateDashboardWidgetDefinition"]>;

  cloneDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: CloneDashboardWidgetPayload
  ) => ReturnType<CoreProductApi["cloneDashboardWidgetDefinition"]>;

  executeWidgetExpression: (
    request: WidgetQueryExpressionRequest
  ) => Promise<DashboardWidgetQueryResultAlias>;

  getLicenseStatus: () => Promise<unknown>;
  getLicensePlans: () => Promise<unknown>;
  getMlReadiness: () => Promise<unknown>;
  getDemoLifecycle: () => Promise<unknown>;
};

export const productApi: CoreProductApi & ProductApiCompatibilityAliases = {
  ...coreProductApi,

  getProviderTypes: () => coreProductApi.getConnectorProviderTypes(),

  getAdminJobs: coreProductApi.getAdminJobsMonitor,

  getDashboardDefinitionById: (dashboardDefinitionId: string) =>
    coreProductApi.getDashboardDefinition(dashboardDefinitionId),

  createDashboardWidget: (
    dashboardDefinitionId: string,
    payload: CreateDashboardWidgetPayload
  ) =>
    coreProductApi.createDashboardWidgetDefinition(
      dashboardDefinitionId,
      payload
    ),

  updateDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: UpdateDashboardWidgetPayload
  ) =>
    coreProductApi.updateDashboardWidgetDefinition(
      dashboardDefinitionId,
      widgetDefinitionId,
      payload
    ),

  deleteDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string
  ) =>
    coreProductApi.deactivateDashboardWidgetDefinition(
      dashboardDefinitionId,
      widgetDefinitionId
    ),

  cloneDashboardWidget: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: CloneDashboardWidgetPayload
  ) =>
    coreProductApi.cloneDashboardWidgetDefinition(
      dashboardDefinitionId,
      widgetDefinitionId,
      payload
    ),

  executeWidgetExpression: (request: WidgetQueryExpressionRequest) =>
    postJson<DashboardWidgetQueryResultAlias>(
      "/dashboarding/widget-query-expression/execute",
      request
    ),

  getLicenseStatus: () => getJson<unknown>("/license/status"),

  getLicensePlans: () => getJson<unknown>("/license/plans"),

  getMlReadiness: () => getJson<unknown>("/ml/readiness"),

  getDemoLifecycle: () => getJson<unknown>("/demo/lifecycle"),
};

export default productApi;
