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

export const analyticsApi = {
  getGenealogyAwareCorrelation: (...args: any[]) => call("getGenealogyAwareCorrelation", ...args),
  getCorrelationContext: (...args: any[]) => call("getCorrelationContext", ...args),
  calculateRiskScore: (...args: any[]) => call("calculateRiskScore", ...args),
  calculateRiskScoresBatch: (...args: any[]) => call("calculateRiskScoresBatch", ...args),
  getMaterialFeatureVector: (...args: any[]) => call("getMaterialFeatureVector", ...args)
};

