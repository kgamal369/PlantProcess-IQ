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

export const integrationApi = {
  getConnectionProfiles: (...args: any[]) => call("getConnectionProfiles", ...args),
  createConnectionProfile: (...args: any[]) => call("createConnectionProfile", ...args),
  updateConnectionImportSchedule: (...args: any[]) => call("updateConnectionImportSchedule", ...args),
  getSourceDatasets: (...args: any[]) => call("getSourceDatasets", ...args),
  createSourceDatasetDefinition: (...args: any[]) => call("createSourceDatasetDefinition", ...args),
  getSchemaViewDefinitions: (...args: any[]) => call("getSchemaViewDefinitions", ...args),
  createSchemaViewDefinition: (...args: any[]) => call("createSchemaViewDefinition", ...args),
  updateSchemaViewDefinition: (...args: any[]) => call("updateSchemaViewDefinition", ...args),
  previewSchemaViewDefinition: (...args: any[]) => call("previewSchemaViewDefinition", ...args),
  createKpiDefinition: (...args: any[]) => call("createKpiDefinition", ...args),
  updateMappingRefreshSchedule: (...args: any[]) => call("updateMappingRefreshSchedule", ...args)
};

