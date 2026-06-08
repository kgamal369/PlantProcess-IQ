// P4 advanced-analysis client — on apiClient (base URL + auth handled centrally).
import { apiClient } from "./http";

export interface AdvancedFindingDto {
  findingId: string; method: string;
  effectSize: number | null; qValue: number | null; sampleSize: number;
  stabilityLower: number | null; stabilityUpper: number | null; stabilityConsistency: number | null;
  isStable: boolean | null; survivesStratification: boolean; significant?: boolean;
  provenanceHandle?: string | null; isRenderable: boolean; evidence?: string | null; honestyCaveat: string;
}
export interface ReadinessDimensionDto { name: string; state: string; reason: string; }
export interface AnalysisReadinessDto {
  overall: string; canRun: boolean; dimensions: ReadinessDimensionDto[];
  outcomeKey: string; grain: string; windowDays: number; independentHeats: number; outcomeEvents: number;
}

export type AdvancedReadinessGateState = "Ready" | "Partial" | "Blocked";

export interface AdvancedReadinessGateDto {
  gateCode: string;
  title: string;
  state: AdvancedReadinessGateState;
  reason: string;
  evidence: string;
  isBlocking: boolean;
}

export interface AdvancedReadinessGateSummaryDto {
  state: AdvancedReadinessGateState;
  canRun: boolean;
  outcomeKey: string;
  grain: string;
  windowDays: number;
  independentHeats: number;
  outcomeEvents: number;
  readyCount: number;
  partialCount: number;
  blockedCount: number;
  message: string;
  gates: AdvancedReadinessGateDto[];
}

const BASE = "/analytics/advanced";
const qs = (o: Record<string, unknown>) =>
  Object.entries(o).filter(([, v]) => v !== undefined && v !== null && v !== "")
    .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`).join("&");

export async function getAdvancedResults(outcomeKey: string, runId?: string): Promise<AdvancedFindingDto[]> {
  const res = await apiClient.get<unknown>(`${BASE}/results?${qs({ outcomeKey, runId })}`);
  if (Array.isArray(res)) return res as AdvancedFindingDto[];
  const r = res as { results?: AdvancedFindingDto[]; findings?: AdvancedFindingDto[] };
  return r?.results ?? r?.findings ?? [];
}
export const getAnalysisReadiness = (outcomeKey: string, grain = "coil", windowDays = 30) =>
  apiClient.get<AnalysisReadinessDto>(`${BASE}/readiness?${qs({ outcomeKey, grain, windowDays })}`);

export const getAnalysisReadinessGates = (outcomeKey: string, grain = "coil", windowDays = 30) =>
  apiClient.get<AdvancedReadinessGateSummaryDto>(`${BASE}/readiness/gates?${qs({ outcomeKey, grain, windowDays })}`);
export const runCorrelation = (outcomeKey: string, grain = "coil", windowDays = 30) =>
  apiClient.post("/ml/foundation/compute/correlation", { outcomeKey, grain, windowDays });
