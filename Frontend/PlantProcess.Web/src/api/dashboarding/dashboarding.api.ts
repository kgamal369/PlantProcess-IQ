import { apiClient } from "../http";
export type * from "../legacy/plantProcessApi";
import { plantProcessApi as legacyApi } from "../legacy/plantProcessApi";

type LegacyFunction = (...args: any[]) => unknown;
const legacy = legacyApi as unknown as Record<string, LegacyFunction>;

function call<T>(name: string, ...args: any[]): Promise<T> {
  const fn = legacy[name];

  if (typeof fn !== "function") {
    return Promise.reject(new Error(`Legacy API function not found: ${name}`));
  }

  return Promise.resolve(fn(...args) as T);
}

export const dashboardingApi = {
  getDashboardWorkspace: (...args: any[]) => call("getDashboardWorkspace", ...args),
  getDashboardReferenceData: (...args: any[]) => call("getDashboardReferenceData", ...args),
  getDashboardMetadata: (...args: any[]) => call("getDashboardMetadata", ...args),
  getDashboardDefinitions: (...args: any[]) => call("getDashboardDefinitions", ...args),
  getDashboardDefinition: (...args: any[]) => call("getDashboardDefinition", ...args),
  createDashboardDefinition: (...args: any[]) => call("createDashboardDefinition", ...args),
  updateDashboardDefinition: (...args: any[]) => call("updateDashboardDefinition", ...args),
  deleteDashboardDefinition: (...args: any[]) => call("deleteDashboardDefinition", ...args),
  queryDashboardWidget: (...args: any[]) => call("queryDashboardWidget", ...args),
  createDashboardWidgetDefinition: (...args: any[]) => call("createDashboardWidgetDefinition", ...args),
  updateDashboardWidgetDefinition: (...args: any[]) => call("updateDashboardWidgetDefinition", ...args),
  deleteDashboardWidgetDefinition: (...args: any[]) => call("deleteDashboardWidgetDefinition", ...args),

  updateDashboardLayout: (dashboardDefinitionId: string, layoutJson: string) =>
    apiClient.patch<{
      dashboardDefinitionId: string;
      layoutPersisted: boolean;
      updatedAtUtc: string;
    }>(`/analytics/dashboard/definitions/${dashboardDefinitionId}/layout`, {
      layoutJson,
    }),
};