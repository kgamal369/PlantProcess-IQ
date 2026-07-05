// PlantProcess IQ Phase 5B-1 domain type module.
// Generated from productCoreApiClient.implementation.ts exported DTO/filter/read-model declarations.
// Runtime API behavior remains in productCoreApiClient.implementation.ts.

import type { AdminMetricCard } from "./analytics-quality-types";
import type { PlannedProvider } from "./license-commercial-types";

export interface AdminLatestImportBatch {
  id: string;
  importBatchCode: string;
  importType: string;
  status: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  rowCount: number | null;
  errorMessage: string | null;
}

export interface AdminOverview {
  generatedAtUtc: string;
  status: string;
  cards: AdminMetricCard[];
  latestImportBatch: AdminLatestImportBatch | null;
}

export interface TwoStageImportStage {
  stageNo: number;
  stageCode: string;
  stageName: string;
  purpose: string;
  currentImplementation: string;
  refreshOwner: string;
  currentCount: number;
  status: string;
}

export interface TwoStageImportModel {
  generatedAtUtc: string;
  modelName: string;
  summary: string;
  stages: TwoStageImportStage[];
  metrics: AdminMetricCard[];
}

export interface DbConfigurationSourceSystem {
  id: string;
  sourceSystemCode: string;
  sourceSystemName: string;
  sourceSystemType: string;
  description: string | null;
  isReadOnlySource: boolean;
  isActive: boolean;
  importBatchCount: number;
  completedBatchCount: number;
  runningBatchCount: number;
  failedBatchCount: number;
  lastImportAtUtc: string | null;
}

export interface DbConfigurationSummary {
  generatedAtUtc: string;
  message: string;
  plannedProviderTypes: PlannedProvider[];
  sourceSystems: DbConfigurationSourceSystem[];
}

export interface SourceObjectCoverage {
  sourceObjectName: string;
  totalRows: number;
  pendingRows: number;
  mappedRows: number;
  failedRows: number;
  skippedRows: number;
}

export interface AdminStatusCount {
  status: string;
  count: number;
}

export interface SchemaMappingSummary {
  id: string;
  mappingCode: string;
  mappingName: string;
  sourceObjectName: string;
  targetEntityName: string;
  mappingVersion: string;
  isActive: boolean;
  description: string | null;
}

export interface SchemaConfigurationSummary {
  generatedAtUtc: string;
  message: string;
  mappingCount: number;
  activeMappingCount: number;
  sourceObjects: SourceObjectCoverage[];
  targetCoverage: AdminStatusCount[];
  mappings: SchemaMappingSummary[];
}

export interface AdminJobMonitorRow {
  id: string;
  jobCode: string;
  jobName: string;
  jobType: string;
  sourceSystemCode: string;
  sourceSystemName: string;
  status: string;
  statusClass: "success" | "running" | "danger" | "warning" | "neutral" | "info" | string;
  lastRunAtUtc: string | null;
  lastDurationMs: number | null;
  nextRunAtUtc: string | null;
  rowCount: number | null;
  errorMessage: string | null;
  isConfigured: boolean;
  isRealRuntimeJob: boolean;
}

export interface AdminJobsMonitor {
  generatedAtUtc: string;
  summary: AdminStatusCount[];
  jobs: AdminJobMonitorRow[];
}

export interface SourceDatasetDefinitionRecord {
  id: string;
  connectionProfileId: string;
  connectionProfileCode: string;
  providerType: string;
  datasetCode: string;
  datasetName: string;
  datasetKind: string;
  sourceObjectName: string;
  sourceSchemaName: string | null;
  primaryTimestampField: string | null;
  incrementalCursorField: string | null;
  lastCursorValue: string | null;
  refreshIntervalSeconds: number;
  datasetOptionsJson: string;
  isActive: boolean;
  description: string | null;
  isSynthetic: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateSourceDatasetDefinitionRequest {
  connectionProfileId: string;
  datasetCode: string;
  datasetName: string;
  datasetKind: string;
  sourceObjectName: string;
  sourceSchemaName?: string | null;
  primaryTimestampField?: string | null;
  incrementalCursorField?: string | null;
  refreshIntervalSeconds?: number | null;
  datasetOptionsJson?: string | null;
  description?: string | null;
  isSynthetic: boolean;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
}

export interface SourceFieldDefinitionRecord {
  id: string;
  sourceDatasetDefinitionId: string;
  fieldName: string;
  displayName: string;
  sourceDataType: string;
  ordinal: number;
  isNullable: boolean;
  maxLength: number | null;
  numericPrecision: number | null;
  numericScale: number | null;
  sampleValue: string | null;
  isPrimaryKeyCandidate: boolean;
  isTimestampCandidate: boolean;
  isActive: boolean;
}

export interface CsvSchemaDiscoveryRequest {
  csvText: string;
  fileName?: string | null;
  delimiter?: string | null;
  hasHeader?: boolean | null;
  maxRowsToAnalyze?: number | null;
  persistFields: boolean;
}

export interface CsvImportSnapshotRequest {
  csvText: string;
  fileName?: string | null;
  delimiter?: string | null;
  hasHeader?: boolean | null;
  importBatchCode?: string | null;
  checksum?: string | null;
  isSynthetic: boolean;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
}

export interface CsvSchemaDiscoveryResult {
  sourceDatasetDefinitionId: string;
  datasetCode: string;
  sourceObjectName: string;
  delimiter: string;
  hasHeader: boolean;
  analyzedRowCount: number;
  fields: SourceFieldDefinitionRecord[];
}

export interface CsvImportSnapshotResult {
  importBatchId: string;
  importBatchCode: string;
  sourceDatasetDefinitionId: string;
  connectionProfileId: string;
  sourceSystemDefinitionId: string;
  sourceObjectName: string;
  rowCount: number;
  status: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
}

// ============================================================
// Phase 4 Schema Configuration DTOs
// ============================================================

export interface SchemaViewDefinitionRecord {
  id: string;
  schemaViewCode: string;
  schemaViewName: string;
  viewKind: string;
  primarySourceDatasetDefinitionId: string | null;
  sqlText: string;
  sourceDatasetIdsJson: string;
  outputSchemaJson: string;
  maxPreviewRows: number;
  timeoutSeconds: number;
  isApproved: boolean;
  isActive: boolean;
  lastValidatedAtUtc: string | null;
  lastValidationStatus: string | null;
  lastValidationMessage: string | null;
  description: string | null;
  isSynthetic: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateSchemaViewDefinitionRequest {
  schemaViewCode: string;
  schemaViewName: string;
  viewKind: string;
  primarySourceDatasetDefinitionId?: string | null;
  sqlText: string;
  sourceDatasetIdsJson?: string | null;
  maxPreviewRows?: number | null;
  timeoutSeconds?: number | null;
  description?: string | null;
  isSynthetic: boolean;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
}

export interface UpdateSchemaViewDefinitionRequest {
  schemaViewName: string;
  viewKind: string;
  primarySourceDatasetDefinitionId?: string | null;
  sqlText: string;
  sourceDatasetIdsJson?: string | null;
  maxPreviewRows?: number | null;
  timeoutSeconds?: number | null;
  description?: string | null;
}

export interface SchemaViewPreviewColumn {
  columnName: string;
  dataType: string;
  ordinal: number;
}

export interface SchemaViewPreviewResult {
  isSuccess: boolean;
  message: string;
  rowCount: number;
  durationMs: number;
  columns: SchemaViewPreviewColumn[];
  rows: Record<string, unknown>[];
}

export interface SchemaViewPreviewRequest {
  sqlText?: string | null;
  maxRows?: number | null;
  timeoutSeconds?: number | null;
}

export interface JobRunHistoryRecord {
  id: string;
  jobDefinitionId: string;
  jobCode: string;
  jobName: string;
  jobType: string;
  status: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  durationMs: number | null;
  triggerSource: string;
  triggeredBy: string | null;
  correlationId: string | null;
  failureReason: string | null;
  runMessage: string | null;
  resultSummaryJson: string | null;
}

export interface JobActionResponse {
  jobDefinitionId: string;
  jobCode: string;
  jobName: string;
  jobType: string;
  status: string;
  message: string;
  jobRunHistoryId: string | null;
  actionedAtUtc: string;
}

export interface UpdateConnectionImportScheduleRequest {
  scheduleExpression: string;
  importIntervalMinutes: number;
}

export interface UpdateMappingRefreshScheduleRequest {
  scheduleExpression: string;
  refreshIntervalMinutes: number;
}
