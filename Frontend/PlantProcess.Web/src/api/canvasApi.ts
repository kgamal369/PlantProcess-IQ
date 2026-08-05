import { apiClient } from "./http";

export type StagedColumn = {
  name: string; sqlType: string; isKeyCandidate: boolean;
  // T-034. Optional because a client built against the pre-T-034 response is
  // still a valid client; the tree states "unknown" rather than inventing a
  // value when either is absent.
  isNullable?: boolean;
};
export type StagedDataset = {
  table: string; source: string; columns: StagedColumn[];
  // null means the table has never been analysed, which is NOT the same claim
  // as zero rows. The tree says so in those words.
  approxRowCount?: number | null;
};
export type JoinSpec = { leftTable: string; leftColumn: string; rightTable: string; rightColumn: string };
// M1-16. Mirrors FilterSpec / DerivedSpec on the server. Op values must stay in
// step with the whitelists in BuildSafeSelect - the interface never offers an
// operator the generator would refuse.
export type FilterSpec = { table: string; column: string; op: string; value: string | null };
export type DerivedSpec = {
  alias: string; leftTable: string; leftColumn: string; op: string;
  rightTable: string | null; rightColumn: string | null; constant: string | null;
};
// T-033 item 1. The projection contract, mirroring SelectSpec on the server.
// There is no alias: naming an output column is a Rename and belongs to a later
// grammar expansion. Absent (undefined) means NO Select block and keeps
// SELECT *; an EMPTY array means a Select block with nothing chosen, which the
// server refuses by name rather than defaulting.
export type SelectSpec = { table: string; column: string };
export type MapperGraph = {
  name: string; targetEntity: string; tables: string[]; joins: JoinSpec[];
  filters?: FilterSpec[]; derived?: DerivedSpec[]; selects?: SelectSpec[];
};
export type DryRunResult = { dryRunId: string; status: string; rowCount: number; columns: string[]; rows: unknown[][]; message?: string; sql?: string };

const BASE = "/api/prep/visual-mapper";

export const listStagedDatasets = () => apiClient.get<StagedDataset[]>(`${BASE}/datasets`);
export const createSession = (name: string) => apiClient.post<{ sessionId: string }>(`${BASE}/sessions`, { name });
export const saveGraph = (sessionId: string, graph: MapperGraph) => apiClient.post<{ ok: boolean }>(`${BASE}/sessions/${sessionId}/graph`, graph);
export const runDryRun = (sessionId: string) => apiClient.post<DryRunResult>(`${BASE}/sessions/${sessionId}/dry-run`, {});

// M1-19. SQL authoring. Both calls go through public.ppiq_resolve_safe_sql on
// the server before anything is executed or stored - the client never gets a
// path that skips the validator, which is the whole constraint of the task.
export type RunSqlResult = {
  status: string; rowCount: number; columns: string[]; rows: unknown[][];
  message: string; errorCode: string | null; sql: string | null; appliedRowLimit: number;
};
export type SaveSqlVersionResult = {
  saved: boolean; versionNumber: number; id: string | null; message: string; errorCode: string | null;
};
export const runAuthoredSql = (sql: string, rowLimit = 100) =>
  apiClient.post<RunSqlResult>("/api/prep/sql/run", { sql, rowLimit });
export const saveSqlVersion = (body: {
  code: string; displayName: string; canonicalEntity?: string | null;
  sql: string; forkedFromGraph: unknown;
}) => apiClient.post<SaveSqlVersionResult>("/api/prep/sql/versions", body);
export const publishVersion = (sessionId: string) => apiClient.post<{ versionId: string; versionNumber: number }>(`${BASE}/sessions/${sessionId}/publish`, {});