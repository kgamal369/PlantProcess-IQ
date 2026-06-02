// P4 typed client for the advanced-analysis endpoints.
// Adjust BASE if your app injects the API origin differently (see src/api/http/apiClient.ts).
export interface AdvancedFindingDto {
  findingId: string;
  method: string;
  effectSize: number | null;
  qValue: number | null;
  sampleSize: number;
  stabilityLower: number | null;
  stabilityUpper: number | null;
  stabilityConsistency: number | null;
  isStable: boolean | null;
  survivesStratification: boolean;
  significant?: boolean;
  provenanceHandle?: string | null;
  isRenderable: boolean;
  evidence?: string | null;
  honestyCaveat: string;
}
export interface AdvancedResultsEnvelope { runId: string | null; honestyCaveat: string; results: AdvancedFindingDto[]; }
export interface ReadinessDimensionDto { name: string; state: string; reason: string; }
export interface AnalysisReadinessDto {
  overall: string; canRun: boolean; dimensions: ReadinessDimensionDto[];
  outcomeKey: string; grain: string; windowDays: number; independentHeats: number; outcomeEvents: number;
}

const BASE = "/api/analytics/advanced";

async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url, { credentials: "include", headers: { Accept: "application/json" } });
  if (!res.ok) throw new Error(`Request failed (${res.status})`);
  return (await res.json()) as T;
}

export const getAdvancedResults = (outcomeKey: string, runId?: string) =>
  getJson<AdvancedResultsEnvelope>(`${BASE}/results?` + new URLSearchParams({ outcomeKey, ...(runId ? { runId } : {}) }));

export const getAnalysisReadiness = (outcomeKey: string, grain = "coil", windowDays = 30) =>
  getJson<AnalysisReadinessDto>(`${BASE}/readiness?` + new URLSearchParams({ outcomeKey, grain, windowDays: String(windowDays) }));

export const runCorrelation = async (outcomeKey: string, grain = "coil", windowDays = 30) => {
  const res = await fetch(`/api/ml/foundation/compute/correlation`, {
    method: "POST", credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ outcomeKey, grain, windowDays }),
  });
  if (!res.ok) throw new Error(`Compute failed (${res.status})`);
  return res.json();
};
