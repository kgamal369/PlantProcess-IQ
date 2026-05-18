import { plantProcessApi as legacyApi } from "../legacy/plantProcessApi";
export type * from "../legacy/plantProcessApi";

type LegacyFunction = (...args: any[]) => unknown;
const legacy = legacyApi as unknown as Record<string, LegacyFunction>;

function call<T>(name: string, ...args: any[]): Promise<T> {
  const fn = legacy[name];

  if (typeof fn !== "function") {
    return Promise.reject(new Error(`Legacy API function not found: ${name}`));
  }

  return Promise.resolve(fn(...args) as T);
}

export const adminApi = {
  getAdminOverview: () => call("getAdminOverview"),
  getAdminTwoStageImportModel: () => call("getAdminTwoStageImportModel"),
  getDbConfigurationSummary: () => call("getDbConfigurationSummary"),
  getSchemaConfigurationSummary: () => call("getSchemaConfigurationSummary"),
  getAdminJobsMonitor: () => call("getAdminJobsMonitor"),
  getJobsMonitor: () => call("getJobsMonitor")
};

