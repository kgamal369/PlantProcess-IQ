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
// T-243. THE AUTHORED BOARD. Everything below is what a person arranged, as opposed
// to what it compiled to. Positions are integers on purpose: the content is hashed, so
// sub-pixel drift from a drag would otherwise read as a new decision.
export type AuthoredNode = {
  id: string; kind: string; position: { x: number; y: number };
  data: Record<string, unknown>;
};
export type AuthoredEdge = {
  source: string; target: string; sourceHandle: string | null; targetHandle: string | null;
};
export type AuthoredBoard = {
  purpose: string; nodes: AuthoredNode[]; edges: AuthoredEdge[];
};

export type MapperGraph = {
  name: string; targetEntity: string; tables: string[]; joins: JoinSpec[];
  filters?: FilterSpec[]; derived?: DerivedSpec[]; selects?: SelectSpec[];
  // Rides with the graph because the session draft is one blob. The server lifts it
  // out at publish so the stored graph stays the clean execution shape.
  board?: AuthoredBoard;
};
export type DryRunResult = {
  dryRunId: string; status: string; rowCount: number;
  columns: string[]; rows: unknown[][]; message?: string; sql?: string;
  // T-035. All optional: a client built before the estimate landed is still a
  // valid client, and the log states only what it was actually given.
  // previewTruncated means the preview STOPPED at its limit, so rowCount is a
  // cap and not a total. plannerCost and estimatedRows come from EXPLAIN and
  // are ESTIMATES - not a runtime, not a price.
  previewTruncated?: boolean; plannerCost?: number | null; estimatedRows?: number | null;
};

const BASE = "/api/prep/visual-mapper";

export const listStagedDatasets = () => apiClient.get<StagedDataset[]>(`${BASE}/datasets`);
export const createSession = (name: string) => apiClient.post<{ sessionId: string }>(`${BASE}/sessions`, { name });
export const saveGraph = (sessionId: string, graph: MapperGraph) => apiClient.post<{ ok: boolean }>(`${BASE}/sessions/${sessionId}/graph`, graph);
export const runDryRun = (sessionId: string) => apiClient.post<DryRunResult>(`${BASE}/sessions/${sessionId}/dry-run`, {});

// M1-19. SQL authoring. Both calls go through public.ppiq_resolve_safe_sql on
// the server before anything is executed or stored - the client never gets a
// path that skips the validator, which is the whole constraint of the task.
export type AuthoredColumn = { name: string; databaseType: string };
export type RunSqlResult = {
  status: string; rowCount: number; columns: string[]; rows: unknown[][];
  message: string; errorCode: string | null; sql: string | null; appliedRowLimit: number;
  // T-036. The database's own type per column, from the reader's metadata.
  // Optional: the refusal and failure paths have no columns to describe, and a
  // client built before this landed is still a valid client. The browser NEVER
  // infers a type from a sample value.
  columnDetails?: AuthoredColumn[] | null;
};
export type SaveSqlVersionResult = {
  saved: boolean; versionNumber: number; id: string | null; message: string; errorCode: string | null;
};
export const runAuthoredSql = (sql: string, rowLimit = 100) =>
  apiClient.post<RunSqlResult>("/api/prep/sql/run", { sql, rowLimit });
// T-253. THE GOVERNED OUTPUT TARGET.
//
// outputTarget is the canonical identity a definition writes to and it is REQUIRED.
// canonicalEntity survives only as the legacy execution-projection handle; it is not
// an alternative way to say the same thing, and the shell no longer sends it.
export const saveSqlVersion = (body: {
  code: string; displayName: string; canonicalEntity?: string | null;
  outputTarget: string;
  sql: string; forkedFromGraph: unknown;
}) => apiClient.post<SaveSqlVersionResult>("/api/prep/sql/versions", body);

export type OutputTargetsResult = {
  source: string; note?: string; targets: string[];
};

// The names come from the mapped model plus the Domain projection-target marker. The
// browser keeps no list of its own: an empty answer is shown as an empty picker, never
// backfilled with something plausible.
export const listOutputTargets = () =>
  apiClient.get<OutputTargetsResult>("/api/prep/authoring/output-targets");
export const publishVersion = (sessionId: string) =>
  apiClient.post<{ versionId: string; versionNumber: number; definitionId?: string; definitionCode?: string }>(`${BASE}/sessions/${sessionId}/publish`, {});

// T-244. The canonical identity of a Canvas definition, read back by its
// tenant-scoped code. Representation is what the server stored: a SQL
// definition comes back as SQL, never as a graph the browser did not author.
export type CanvasDefinitionResponse = {
  definitionId: string; versionId: string; definitionCode: string; versionNumber: number;
  status: string; definitionHash: string; representation: "graph" | "sql";
  graph?: MapperGraph | null; sql?: string | null; forkedFromGraph?: MapperGraph | null;
  // Null for a version saved before boards were persisted. Such a version reopens as
  // the query it always was; the surface says so instead of inventing a layout.
  board?: AuthoredBoard | null;
  // T-253. Null for a legacy SQL definition, which genuinely declared none. The surface
  // that reopens one must ask rather than fill it in. T-243 owns that reopen surface.
  outputTarget?: string | null;
};
export const reopenDefinition = (code: string, version?: number) =>
  apiClient.get<CanvasDefinitionResponse>(
    version === undefined
      ? `/api/prep/definitions/${encodeURIComponent(code)}`
      : `/api/prep/definitions/${encodeURIComponent(code)}/versions/${version}`);

// T-243. The versions that exist, newest first. Reopen already took a number; nothing
// said which numbers were real, so history could be requested but not offered.
export type CanvasVersionSummary = {
  versionNumber: number; status: string; definitionHash: string;
  createdAtUtc: string; isCurrent: boolean;
};
export const listDefinitionVersions = (code: string) =>
  apiClient.get<{ definitionCode: string; versions: CanvasVersionSummary[] }>(
    `/api/prep/definitions/${encodeURIComponent(code)}/versions`);