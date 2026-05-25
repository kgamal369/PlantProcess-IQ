import { apiClient } from "./http";

function getJson<TResponse>(path: string): Promise<TResponse> {
  return apiClient.get<TResponse>(path);
}

function postJson<TResponse = unknown, TBody = unknown>(
  path: string,
  body: TBody
): Promise<TResponse> {
  return apiClient.post<TResponse>(path, body);
}
export type CrossSourceJoinSide = {
  sourceDatasetDefinitionId?: string | null;
  sourceSchemaName?: string | null;
  sourceObjectName: string;
  joinField: string;
};

export type CrossSourceJoinPreviewRequest = {
  left: CrossSourceJoinSide;
  right: CrossSourceJoinSide;
  joinType?: "Inner" | "Left" | null;
  maxRows?: number | null;
  timeoutSeconds?: number | null;
};

export type CrossSourceJoinPreviewResponse = {
  isSuccess: boolean;
  message: string;
  sqlText: string;
  rowCount: number;
  durationMs: number;
  columns: Array<{ columnName: string; dataType: string; ordinal: number }>;
  rows: Array<Record<string, unknown>>;
};

export type SaveCrossSourceJoinViewRequest = {
  schemaViewCode: string;
  schemaViewName: string;
  join: CrossSourceJoinPreviewRequest;
  description?: string | null;
  isSynthetic: boolean;
};

export type KpiParameterBindingRow = {
  id: string;
  kpiCode: string;
  kpiName: string;
  parameterDefinitionId: string;
  parameterCode: string;
  parameterName: string;
  aggregationMethod: string;
  unitOfMeasure?: string | null;
  filterJson: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
};

export type OperationProgressRow = {
  id: string;
  operationCode: string;
  operationType: string;
  operationName: string;
  status: string;
  percentComplete: number;
  currentStep?: string | null;
  totalSteps?: number | null;
  completedSteps?: number | null;
  message?: string | null;
  startedAtUtc: string;
  completedAtUtc?: string | null;
  failedAtUtc?: string | null;
  failureReason?: string | null;
  correlationId?: string | null;
  requestedBy?: string | null;
  metadataJson: string;
};

export type InspectionJobRow = {
  id: string;
  inspectionJobCode: string;
  inspectionJobName: string;
  inspectionType: string;
  parameterCode?: string | null;
  defectType?: string | null;
  isEnabled: boolean;
  honestState: string;
  scheduleExpression: string;
  lastRunAtUtc?: string | null;
  lastRunStatus?: string | null;
  createdAtUtc: string;
};

export type RuleCorrelationResponse = {
  generatedAtUtc: string;
  parameterCode: string;
  defectType: string;
  fromUtc: string;
  toUtc: string;
  ruleStrength: number;
  interpretation: string;
  buckets: Array<{
    bucketNumber: number;
    materialCount: number;
    defectCount: number;
    defectRatePct: number;
    minValue?: number | null;
    maxValue?: number | null;
    avgValue?: number | null;
  }>;
};

export type MlLifecycleRow = {
  id: string;
  mlJobCode: string;
  mlJobName: string;
  state: string;
  stateReason: string;
  readinessScore?: number | null;
  labelCount?: number | null;
  featureCount?: number | null;
  lastEvaluatedAtUtc?: string | null;
  nextRecommendedAction?: string | null;
  noProductionPrediction: boolean;
  metadataJson: string;
};

export type DemoLanguageAuditResponse = {
  generatedAtUtc: string;
  isClean: boolean;
  message: string;
  findings: Array<{
    ruleCode: string;
    forbiddenPhrase: string;
    saferReplacement: string;
    severity: string;
    rationale: string;
  }>;
};

export const phase2WorkflowApi = {
  previewCrossSourceJoin: (request: CrossSourceJoinPreviewRequest) =>
    postJson<CrossSourceJoinPreviewResponse>(
      "/admin/phase2/cross-source/preview-join",
      request
    ),

  saveCrossSourceJoinView: (request: SaveCrossSourceJoinViewRequest) =>
    postJson("/admin/phase2/cross-source/save-join-view", request),

  getKpiParameterBindings: () =>
    getJson<{ generatedAtUtc: string; rows: KpiParameterBindingRow[] }>(
      "/admin/phase2/kpi-parameter-bindings"
    ),

  createKpiParameterBinding: (request: unknown) =>
    postJson<KpiParameterBindingRow>("/admin/phase2/kpi-parameter-bindings", request),

  runJobNow: (jobDefinitionId: string) =>
    postJson(`/admin/phase2/jobs/${jobDefinitionId}/run-now`, {}),

  retryJob: (jobDefinitionId: string) =>
    postJson(`/admin/phase2/jobs/${jobDefinitionId}/retry`, {}),

  enableJob: (jobDefinitionId: string) =>
    postJson(`/admin/phase2/jobs/${jobDefinitionId}/enable`, {}),

  disableJob: (jobDefinitionId: string) =>
    postJson(`/admin/phase2/jobs/${jobDefinitionId}/disable`, {}),

  getRecentOperationProgress: () =>
    getJson<{ generatedAtUtc: string; rows: OperationProgressRow[] }>(
      "/admin/phase2/operations/progress/recent"
    ),

  getInspectionJobs: () =>
    getJson<{ generatedAtUtc: string; rows: InspectionJobRow[] }>(
      "/analytics/phase2/inspection-jobs"
    ),

  saveInspectionJobFromCorrelation: (request: unknown) =>
    postJson<InspectionJobRow>(
      "/analytics/phase2/inspection-jobs/save-from-correlation",
      request
    ),

  runRuleCorrelation: (request: unknown) =>
    postJson<RuleCorrelationResponse>("/analytics/phase2/rule-correlation/run", request),

  getMlLifecycle: () =>
    getJson<{ generatedAtUtc: string; rows: MlLifecycleRow[] }>(
      "/analytics/phase2/ml-lifecycle"
    ),

  evaluateMlLifecycle: (request: unknown) =>
    postJson<MlLifecycleRow>("/analytics/phase2/ml-lifecycle/evaluate", request),

  auditDemoLanguage: (text: string) =>
    postJson<DemoLanguageAuditResponse>("/admin/phase2/pilot/demo-language/audit", {
      text,
    }),

  getTenantIsolationDecision: () =>
    getJson("/admin/phase2/pilot/tenant-isolation-decision"),

  queryAuditLog: (query = "") =>
    getJson(`/admin/phase2/pilot/audit-log${query}`),

  getDeploymentChecklist: () =>
    getJson("/admin/phase2/pilot/deployment-checklist"),
};