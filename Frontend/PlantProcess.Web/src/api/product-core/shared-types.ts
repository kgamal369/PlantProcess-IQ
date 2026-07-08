// PlantProcess IQ Phase 5B-1 domain type module.
// Generated from productCoreApiClient.implementation.ts exported DTO/filter/read-model declarations.
// Runtime API behavior remains in productCoreApiClient.implementation.ts.


// PlantProcess IQ Phase 5B-1
// Exported product-core API DTO/filter/read-model types extracted from productCoreApiClient.implementation.ts.
// Runtime behavior stays in the implementation file; this module is type-only.
export type SortDirection = "asc" | "desc";

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  sortBy?: string;
  sortDirection?: SortDirection;
}

export interface ReferenceItem {
  id: string;
  code: string;
  name: string;
  group?: string;
  count: number;
}

// ============================================================
// Phase 3 Connector Foundation DTOs
// ============================================================

export interface ProviderTypeRecord {
  providerType: string;
  displayName: string;
  description: string;
  isAvailableNow: boolean;
  requiresSecretReference: boolean;
  supportsSchemaDiscovery: boolean;
  supportsSnapshotImport: boolean;
  supportsIncrementalImport: boolean;
}

export interface ConnectionProfileRecord {
  id: string;
  sourceSystemDefinitionId: string;
  sourceSystemCode: string;
  sourceSystemName: string;
  connectionProfileCode: string;
  connectionProfileName: string;
  providerType: string;
  connectionMode: string;
  hostName: string | null;
  port: number | null;
  databaseName: string | null;
  schemaName: string | null;
  fileRootPath: string | null;
  apiBaseUrl: string | null;
  secretReference: string | null;
  connectionOptionsJson: string;
  isActive: boolean;
  readOnlyEnforced: boolean;
  description: string | null;
  lastTestedAtUtc: string | null;
  lastTestStatus: string | null;
  lastTestMessage: string | null;
  isSynthetic: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateConnectionProfileRequest {
  sourceSystemDefinitionId: string;
  connectionProfileCode: string;
  connectionProfileName: string;
  providerType: string;
  connectionMode?: string | null;
  hostName?: string | null;
  port?: number | null;
  databaseName?: string | null;
  schemaName?: string | null;
  fileRootPath?: string | null;
  apiBaseUrl?: string | null;
  secretReference?: string | null;
  connectionOptionsJson?: string | null;
  readOnlyEnforced?: boolean | null;
  description?: string | null;
  isSynthetic: boolean;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
}

export interface CsvPreviewRequest {
  csvText: string;
  delimiter?: string | null;
  hasHeader?: boolean | null;
  maxRows?: number | null;
}

export interface CsvPreviewResult {
  delimiter: string;
  hasHeader: boolean;
  headers: string[];
  rows: Record<string, string | null>[];
}

// ---- M1-03/M1-04 Surface-1 discovery types ----
export interface SourceTableRecord {
  schemaName: string;
  tableName: string;
  kind: string;
}
export interface SourceColumnRecord {
  columnName: string;
  dataType: string;
  ordinal: number;
  isNullable: boolean;
  isPrimaryKeyCandidate: boolean;
  isTimestampCandidate: boolean;
}
export interface RegisterSourceTableRequest {
  schemaName: string;
  tableName: string;
  primaryKeyColumns: string[];
  watermarkColumn?: string | null;
  selectedColumns?: string[] | null;
  rowFilter?: string | null;
}
export interface RegisterSourceTableResult {
  schemaName: string;
  tableName: string;
  registeredColumnCount: number;
  watermarkResolved: boolean;
  message: string;
}

// ---- M1-05 Surface-3 analysis-job definition types ----
export interface AnalysisDefectTypeOption {
  eventType: string;
  eventCount: number;
}
export interface AnalysisParameterOption {
  parameterCode: string;
  parameterName: string;
  observationCount: number;
}
export interface AnalysisEngineOutcomeOption {
  outcomeKey: string;
  displayName: string;
  outcomeType: string;
  grain: string;
}
export interface AnalysisEngineJobOption {
  jobCode: string;
  jobName: string;
  outcomeFamily: string;
  isEnabled: boolean;
}
export interface AnalysisDataWindowInfo {
  minObservedAtUtc: string | null;
  maxObservedAtUtc: string | null;
  observationCount: number;
}
export interface AnalysisJobDefinitionOptions {
  generatedAtUtc: string;
  defectTypes: AnalysisDefectTypeOption[];
  parameters: AnalysisParameterOption[];
  engineOutcomes: AnalysisEngineOutcomeOption[];
  engineJobs: AnalysisEngineJobOption[];
  dataWindow: AnalysisDataWindowInfo;
  populationFilterNote: string;
}
export interface AnalysisJobDefinitionRow {
  id: string;
  code: string;
  name: string;
  inspectionType: string;
  parameterCode: string | null;
  defectType: string | null;
  ruleJson: string;
  scheduleExpression: string;
  isEnabled: boolean;
  honestState: string;
  sourceCorrelationRunId: string | null;
  lastRunAtUtc: string | null;
  lastRunStatus: string | null;
  description: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}
export interface AnalysisJobListResponse {
  generatedAtUtc: string;
  rows: AnalysisJobDefinitionRow[];
}
export interface CreateAnalysisJobDefinitionRequest {
  code: string;
  name: string;
  defectType: string;
  parameterCode?: string | null;
  windowDays?: number | null;
  populationFilters?: Record<string, string> | null;
  engineOutcomeKey?: string | null;
  engineJobCode?: string | null;
  grain?: string | null;
  scheduleExpression?: string | null;
  isEnabled?: boolean | null;
  description?: string | null;
}
export interface UpdateAnalysisJobDefinitionRequest {
  name: string;
  defectType: string;
  parameterCode?: string | null;
  windowDays?: number | null;
  populationFilters?: Record<string, string> | null;
  engineOutcomeKey?: string | null;
  engineJobCode?: string | null;
  grain?: string | null;
  scheduleExpression?: string | null;
  isEnabled?: boolean | null;
  description?: string | null;
}
export interface RunAnalysisJobRequest {
  windowDaysOverride?: number | null;
}
export interface AnalysisJobRunResponse {
  generatedAtUtc: string;
  code: string;
  definitionStatus: string;
  windowDays: number;
  readinessStatus: string;
  readinessReason: string;
  learningJobCode: string;
  learningRunId: string | null;
  learningStatus: string;
  learningResultCount: number;
  computeEngineKey: string;
  computeRunId: string | null;
  computeStatus: string;
  computeMessage: string;
  computeResultCount: number;
  engineOutcomeKey: string;
  populationFilterNote: string;
  honestPositioning: string;
}
export interface AnalysisJobResultRow {
  id: string;
  compute_run_id: string;
  feature_key: string;
  feature_grain: string;
  outcome_key: string;
  outcome_type: string;
  method: string;
  coefficient: number | null;
  effect_size: number | null;
  effect_size_type: string | null;
  p_value: number | null;
  q_value: number | null;
  ci_low: number | null;
  ci_high: number | null;
  sample_size: number;
  effective_n: number;
  stratum: string | null;
  stability_score: number | null;
  is_stable: boolean | null;
  created_at_utc: string;
}
export interface AnalysisJobResultsResponse {
  generatedAtUtc: string;
  code: string;
  computeRunId: string | null;
  count: number;
  results: AnalysisJobResultRow[];
  message?: string;
  honestPositioning: string;
}
export interface RuleCorrelationRunRequest {
  parameterCode: string;
  defectType: string;
  siteId?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}
export interface RuleCorrelationBucketRow {
  bucketNumber: number;
  materialCount: number;
  defectCount: number;
  defectRatePct: number;
  minValue: number | null;
  maxValue: number | null;
  avgValue: number | null;
}
export interface RuleCorrelationRunResponse {
  generatedAtUtc: string;
  parameterCode: string;
  defectType: string;
  fromUtc: string;
  toUtc: string;
  ruleStrength: number;
  interpretation: string;
  buckets: RuleCorrelationBucketRow[];
}
