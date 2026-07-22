import { apiClient } from "./http";

export type StagedDataset = { table: string; source: string; columns: { name: string; sqlType: string; isKeyCandidate: boolean }[] };
export type JoinSpec = { leftTable: string; leftColumn: string; rightTable: string; rightColumn: string };
export type MapperGraph = { name: string; targetEntity: string; tables: string[]; joins: JoinSpec[] };
export type DryRunResult = { dryRunId: string; status: string; rowCount: number; columns: string[]; rows: unknown[][]; message?: string };

const BASE = "/prep/visual-mapper";

export const listStagedDatasets = () => apiClient.get<StagedDataset[]>(`${BASE}/datasets`);
export const createSession = (name: string) => apiClient.post<{ sessionId: string }>(`${BASE}/sessions`, { name });
export const saveGraph = (sessionId: string, graph: MapperGraph) => apiClient.post<{ ok: boolean }>(`${BASE}/sessions/${sessionId}/graph`, graph);
export const runDryRun = (sessionId: string) => apiClient.post<DryRunResult>(`${BASE}/sessions/${sessionId}/dry-run`, {});
export const publishVersion = (sessionId: string) => apiClient.post<{ versionId: string; versionNumber: number }>(`${BASE}/sessions/${sessionId}/publish`, {});