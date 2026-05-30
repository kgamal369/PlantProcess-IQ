import { getJson, postJson } from "@/api/legacy/legacyApiHardening";

export type TwoStageImportOverview = {
  isReady: boolean;
  generatedAtUtc: string;
  message: string;
  sourceTables: TwoStageSourceTable[];
  recentRuns: TwoStageRun[];
  jobs: TwoStageJob[];
  summary: Array<{ metric: string; value: string }>;
};

export type TwoStageSourceTable = {
  id: string;
  sourceSystemCode: string;
  sourceSchemaName: string;
  sourceTableName: string;
  dumpSchemaName: string;
  dumpTableName: string;
  primaryKeyColumns: string[] | string | null;
  lastIndexColumn: string;
  lastIndexValueText: string | null;
  lastIndexValueType: string | null;
  sourceColumnCount: number;
  sourceShapeHash: string | null;
  stage1Status: string;
  stage2Status: string;
  lastStage1InsertedRows: number;
  lastStage2CanonicalRows: number;
  lastSyncedAtUtc: string | null;
  lastError: string | null;
  importCycleMinutes: number;
  hmiRefreshSeconds: number;
  isActive: boolean;
};

export type TwoStageRun = {
  id: string;
  registryId: string | null;
  runKind: string;
  runStatus: string;
  sourceSchemaName: string | null;
  sourceTableName: string | null;
  dumpSchemaName: string | null;
  dumpTableName: string | null;
  startedAtUtc: string;
  completedAtUtc: string | null;
  durationMs: number | null;
  insertedRows: number;
  canonicalRows: number;
  lastIndexBefore: string | null;
  lastIndexAfter: string | null;
  message: string | null;
  failureReason: string | null;
};

export type TwoStageJob = {
  id: string;
  jobCode: string;
  jobName: string;
  jobType: string;
  jobCategory: string | null;
  stageKey: string | null;
  scheduleExpression: string;
  isEnabled: boolean;
  lastRunStatus: string;
  lastRunStartedAtUtc: string | null;
  lastRunCompletedAtUtc: string | null;
  lastRunDurationMs: number | null;
  lastFailureReason: string | null;
  lastSuccessRowCount: number | null;
  lastFailedRowCount: number | null;
  consecutiveFailureCount: number;
  lastTimeoutSeconds: number | null;
};

export type RunTwoStageImportRequest = {
  registryId?: string | null;
  requestedBy?: string;
  maxRows?: number;
  timeoutSeconds?: number;
  maxMinutes?: number;
};

export type RunTwoStageImportResponse = {
  generatedAtUtc: string;
  stage: string;
  rows: Array<Record<string, unknown>>;
};

export const twoStageImportApi = {
  getOverview: () =>
    getJson<TwoStageImportOverview>("/admin/two-stage-import/overview"),

  getRuns: (take = 50) =>
    getJson<TwoStageRun[]>(`/admin/two-stage-import/runs?take=${take}`),

  runStage1: (request: RunTwoStageImportRequest = {}) =>
    postJson<RunTwoStageImportResponse>("/admin/two-stage-import/stage1/run", request),

  runStage2: (request: RunTwoStageImportRequest = {}) =>
    postJson<RunTwoStageImportResponse>("/admin/two-stage-import/stage2/run", request),

  runFullCycle: (request: RunTwoStageImportRequest = {}) =>
    postJson<RunTwoStageImportResponse>("/admin/two-stage-import/run-full-cycle", request),

  provisionBaseline: () =>
    postJson<{ generatedAtUtc: string; message: string }>(
      "/admin/two-stage-import/provision-baseline",
      {}
    ),
};