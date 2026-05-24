import { API_BASE_URL } from "../apiConfig";
import {
  buildQuery,
  deleteJson,
  getJson,
  patchJson,
  postJson,
  putJson,
  requestJson,
  type QueryParams,
} from "./legacyApiHardening";
export type SortDirection = "asc" | "desc";

export interface DashboardFilters {
  siteId?: string;
  areaId?: string;
  equipmentId?: string;
  materialCode?: string;
  materialUnitType?: string;
  sourceSystem?: string;
  defectType?: string;
  riskClass?: string;
  fromUtc?: string;
  toUtc?: string;
  shiftCode?: string;
  parameterCode?: string;
  linkMode?: "SameMaterial" | "DownstreamChildren" | "UpstreamParents" | "FullGenealogy";
  genealogyDepth?: number;
  bins?: number;
  minimumObservationsPerBin?: number;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDirection?: SortDirection;
}

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

export interface DashboardReferenceData {
  generatedAtUtc: string;
  sites: ReferenceItem[];
  areas: ReferenceItem[];
  equipment: ReferenceItem[];
  sourceSystems: ReferenceItem[];
  defects: ReferenceItem[];
  parameters: ReferenceItem[];
  riskClasses: ReferenceItem[];
  shifts: ReferenceItem[];
}

export interface DashboardMaterialRow {
  materialUnitId: string;
  materialCode: string;
  materialUnitType: string;
  productFamily?: string;
  gradeOrRecipe?: string;
  siteId: string;
  siteName?: string;
  productionStartUtc?: string;
  productionEndUtc?: string;
  sourceSystem?: string;
  processStepCount: number;
  parameterObservationCount: number;
  qualityEventCount: number;
  defectEventCount: number;
  latestRiskScore?: number;
  latestRiskClass?: string;
  latestScoredAtUtc?: string;
}

export interface DashboardWorkspace {
  generatedAtUtc: string;
  query: DashboardFilters;
  overview: any;
  quality: any;
  risk: any;
  dataQuality: any;
  materials: PagedResult<DashboardMaterialRow>;
}

export interface GenealogyAwareCorrelationResult {
  generatedAtUtc: string;
  parameterCode: string;
  parameterName: string;
  unitOfMeasure?: string;
  defectType: string;
  linkMode: string;
  genealogyDepth: number;
  baselineDefectRatePercent: number;
  totalObservationCount: number;
  totalMaterialCount: number;
  totalDefectLinkedObservationCount: number;
  bins: GenealogyAwareCorrelationBin[];
  message: string;
}

export interface GenealogyAwareCorrelationBin {
  binNo: number;
  binLabel: string;
  minValue: number;
  maxValue: number;
  observationCount: number;
  materialCount: number;
  defectLinkedObservationCount: number;
  defectRatePercent: number;
  liftVsBaseline?: number | null;
  confidence: string;
}

export interface DashboardDimensionMetadata {
  code: string;
  label: string;
  category: string;
  dataType: string;
  requiresParameterCode: boolean;
  compatibleChartTypes: string[];
  description?: string;
}

export interface DashboardMeasureMetadata {
  code: string;
  label: string;
  category: string;
  aggregation: string;
  unit?: string | null;
  requiresParameterCode: boolean;
  compatibleChartTypes: string[];
  description?: string;
}

export interface DashboardChartTypeMetadata {
  code: string;
  label: string;
  category: string;
  supportsDimension: boolean;
  supportsMeasure: boolean;
  supportsMultipleSeries: boolean;
  supportsParameterSelection: boolean;
  description?: string;
}

export interface DashboardFilterMetadata {
  code: string;
  label: string;
  category: string;
  dataType: string;
  operatorMode: string;
  isRequired: boolean;
  sourceCatalog?: string | null;
  description?: string;
}

export interface DashboardPurposeMetadata {
  code: string;
  label: string;
  description: string;
  recommendedDimensions: string[];
  recommendedMeasures: string[];
  recommendedChartTypes: string[];
}

export interface DashboardCompatibilityRule {
  dimensionCode: string;
  measureCode: string;
  allowedChartTypes: string[];
  requiresParameterCode: boolean;
  warningMessage?: string | null;
}

export interface DashboardQuerySafetyLimits {
  defaultMaxRows: number;
  absoluteMaxRows: number;
  defaultRawRowLimit: number;
  absoluteRawRowLimit: number;
  defaultLookbackDays: number;
  absoluteLookbackDays: number;
}

export interface DashboardMetadata {
  generatedAtUtc: string;
  dimensions: DashboardDimensionMetadata[];
  measures: DashboardMeasureMetadata[];
  chartTypes: DashboardChartTypeMetadata[];
  filters: DashboardFilterMetadata[];
  purposes: DashboardPurposeMetadata[];
  compatibilityRules: DashboardCompatibilityRule[];
  safetyLimits: DashboardQuerySafetyLimits;
}

export interface DashboardWidgetFilters {
  siteId?: string | null;
  areaId?: string | null;
  equipmentId?: string | null;
  materialCode?: string | null;
  materialUnitType?: string | null;
  sourceSystem?: string | null;
  defectType?: string | null;
  riskClass?: string | null;
  shiftCode?: string | null;
  parameterCode?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface DashboardWidgetQueryOptions {
  maxRows?: number;
  rawRowLimit?: number;
  sortDirection?: SortDirection;
  includeWarnings?: boolean;
}

export interface DashboardWidgetQuery {
  widgetType?: string;
  chartType?: string;
  dimensionCode?: string | null;
  measureCode?: string | null;
  parameterCode?: string | null;
  filters?: DashboardWidgetFilters | null;
  options?: DashboardWidgetQueryOptions | null;
}

export interface DashboardWidgetResolved {
  widgetType: string;
  chartType: string;
  dimensionCode?: string | null;
  measureCode: string;
  parameterCode?: string | null;
  maxRows: number;
  rawRowLimit: number;
  sortDirection: SortDirection;
  fromUtc?: string | null;
  toUtc?: string | null;
}

export interface DashboardWidgetColumn {
  code: string;
  label: string;
  dataType: string;
}

export interface DashboardWidgetQueryResult {
  generatedAtUtc: string;
  widget: DashboardWidgetResolved;
  columns: DashboardWidgetColumn[];
  rows: Record<string, unknown>[];
  warnings: string[];
}

export interface DashboardDefinitionRecord {
  id: string;
  userId?: string | null;
  dashboardCode: string;
  name: string;
  description?: string | null;
  layoutJson: string;
  isDefault: boolean;
  isSystemTemplate: boolean;
  isActive: boolean;
  isSynthetic: boolean;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
  widgets: DashboardWidgetDefinitionRecord[];
}

export interface DashboardWidgetDefinitionRecord {
  id: string;
  dashboardDefinitionId: string;
  widgetCode: string;
  widgetTitle: string;
  widgetType: string;
  chartType: string;
  dimensionCode: string;
  measureCode: string;
  parameterCode?: string | null;
  filterJson: string;
  layoutJson: string;
  displayOptionsJson: string;
  sortOrder: number;
  isActive: boolean;
  isSynthetic: boolean;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
}

export interface CreateDashboardWidgetDefinitionPayload {
  widgetCode: string;
  widgetTitle: string;
  widgetType: string;
  chartType: string;
  dimensionCode: string;
  measureCode: string;
  parameterCode?: string | null;
  filterJson?: string | null;
  layoutJson?: string | null;
  displayOptionsJson?: string | null;
  sortOrder?: number | null;
  isSynthetic: boolean;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
}
// ============================================================
// Phase 2 Admin Area Foundation DTOs
// ============================================================

export interface AdminMetricCard {
  label: string;
  value: number;
  note: string;
  group: string;
}

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

export interface PlannedProvider {
  providerType: string;
  description: string;
  roadmapStatus: string;
  recommendedForFirstDemo: boolean;
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

export interface CsvPreviewRequest {
  csvText: string;
  delimiter?: string | null;
  hasHeader?: boolean | null;
  maxRows?: number | null;
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

export interface CsvPreviewResult {
  delimiter: string;
  hasHeader: boolean;
  headers: string[];
  rows: Record<string, string | null>[];
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

export interface KpiDefinitionRecord {
  id: string;
  schemaViewDefinitionId: string | null;
  kpiCode: string;
  kpiName: string;
  kpiCategory: string;
  valueExpression: string;
  unit: string | null;
  dimensionExpression: string | null;
  filterExpression: string | null;
  aggregationType: string;
  kpiOptionsJson: string;
  isActive: boolean;
  description: string | null;
  isSynthetic: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface CreateKpiDefinitionRequest {
  schemaViewDefinitionId?: string | null;
  kpiCode: string;
  kpiName: string;
  kpiCategory: string;
  valueExpression: string;
  unit?: string | null;
  dimensionExpression?: string | null;
  filterExpression?: string | null;
  aggregationType?: string | null;
  kpiOptionsJson?: string | null;
  description?: string | null;
  isSynthetic: boolean;
  sourceSystem?: string | null;
  sourceRecordId?: string | null;
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

export interface MaterialInvestigationRequestOptions {
  maxDepth?: number;
  parameterPage?: number;
  parameterPageSize?: number;
}


function dashboardQuery(filters: DashboardFilters): QueryParams {
  return {
    siteId: filters.siteId,
    areaId: filters.areaId,
    equipmentId: filters.equipmentId,
    materialCode: filters.materialCode,
    sourceSystem: filters.sourceSystem,
    defectType: filters.defectType,
    riskClass: filters.riskClass,
    fromUtc: filters.fromUtc,
    toUtc: filters.toUtc,
    shiftCode: filters.shiftCode,
    page: filters.page ?? 1,
    pageSize: filters.pageSize ?? 25,
    sortBy: filters.sortBy,
    sortDirection: filters.sortDirection,
  };
}

function dashboardBody(filters: DashboardFilters) {
  return {
    siteId: filters.siteId || null,
    areaId: filters.areaId || null,
    equipmentId: filters.equipmentId || null,
    materialCode: filters.materialCode || null,
    sourceSystem: filters.sourceSystem || null,
    defectType: filters.defectType || null,
    riskClass: filters.riskClass || null,
    fromUtc: filters.fromUtc || null,
    toUtc: filters.toUtc || null,
    shiftCode: filters.shiftCode || null,
    page: filters.page ?? 1,
    pageSize: filters.pageSize ?? 25,
    sortBy: filters.sortBy || null,
    sortDirection: filters.sortDirection || "desc",
  };
}
function createClientCorrelationId(): string {
  if (
    typeof crypto !== "undefined" &&
    typeof crypto.randomUUID === "function"
  ) {
    return crypto.randomUUID();
  }

  return `client-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}


export const plantProcessApi = {
  apiBaseUrl: API_BASE_URL,

  getAdminOverview: () =>
    getJson<AdminOverview>("/admin/overview"),

  getAdminTwoStageImportModel: () =>
    getJson<TwoStageImportModel>("/admin/two-stage-import-model"),

  getAdminDbConfigurationSummary: () =>
    getJson<DbConfigurationSummary>("/admin/db-configuration/summary"),

  getAdminSchemaConfigurationSummary: () =>
    getJson<SchemaConfigurationSummary>("/admin/schema-configuration/summary"),

  getAdminJobsMonitor: () =>
    getJson<AdminJobsMonitor>("/admin/jobs-monitor"),
  
  runJobNow: (jobId: string, requestedBy = "Admin UI") =>
    postJson<JobActionResponse>(`/admin/jobs/${jobId}/run-now`, {
      requestedBy,
      correlationId: createClientCorrelationId(),
    }),

  pauseJob: (jobId: string) =>
    postJson<JobActionResponse>(`/admin/jobs/${jobId}/pause`, {}),

  resumeJob: (jobId: string) =>
    postJson<JobActionResponse>(`/admin/jobs/${jobId}/resume`, {}),

  getJobHistory: (jobId: string, take = 20) =>
    getJson<JobRunHistoryRecord[]>(`/admin/jobs/${jobId}/history`, {
      take,
    }),

  updateConnectionImportSchedule: (
    connectionProfileId: string,
    request: UpdateConnectionImportScheduleRequest
  ) =>
    patchJson<any>(
      `/admin/jobs/connection-profiles/${connectionProfileId}/schedule`,
      request
    ),

  updateMappingRefreshSchedule: (
    mappingDefinitionId: string,
    request: UpdateMappingRefreshScheduleRequest
  ) =>
    patchJson<any>(
      `/admin/jobs/mappings/${mappingDefinitionId}/schedule`,
      request
    ),

  getValidationReport: () => getJson<any>("/validation/sync-report"),

  getDashboardReferenceData: (filters: DashboardFilters = {}) =>
    getJson<DashboardReferenceData>("/analytics/dashboard/reference-data", {
      siteId: filters.siteId,
    }),

  getDashboardMetadata: () =>
    getJson<DashboardMetadata>("/analytics/dashboard/metadata"),

  queryDashboardWidget: (query: DashboardWidgetQuery) =>
    postJson<DashboardWidgetQueryResult>("/analytics/dashboard/widgets/query", query),
  
  getDashboardWorkspace: (filters: DashboardFilters = {}) =>
    postJson<DashboardWorkspace>("/analytics/dashboard/workspace", dashboardBody(filters)),

  getDashboardOverview: (filters: DashboardFilters = {}) =>
    getJson<any>("/analytics/dashboard/overview", dashboardQuery(filters)),

  getQualityDashboard: (filters: DashboardFilters = {}) =>
    getJson<any>("/analytics/dashboard/quality", dashboardQuery(filters)),

  getRiskDashboard: (filters: DashboardFilters = {}) =>
    getJson<any>("/analytics/dashboard/risk", {
      ...dashboardQuery(filters),
      highRiskTake: filters.pageSize ?? 25,
    }),

  getDataQualityDashboard: (filters: DashboardFilters = {}) =>
    getJson<any>("/analytics/dashboard/data-quality", dashboardQuery(filters)),

  searchDashboardMaterials: (filters: DashboardFilters = {}) =>
    getJson<PagedResult<DashboardMaterialRow>>(
      "/analytics/dashboard/materials",
      dashboardQuery(filters)
    ),

  refreshDashboardReadModels: () =>
    postJson<any>("/analytics/dashboard/read-models/refresh"),

  getMaterialSample: (take = 20) =>
    getJson<any[]>("/dev/material-sample", { take }),

  getMaterialFeatures: (materialUnitId: string) =>
    getJson<any>(`/analytics/features/${materialUnitId}`),

  calculateRisk: (materialUnitId: string) =>
    postJson<any>(`/risk-scores/materials/${materialUnitId}/calculate`, {
      riskType: "QualityRisk",
    }),

  getMaterialInvestigation: (materialUnitId: string, options: MaterialInvestigationRequestOptions = {}) =>
  getJson<any>(
    `/materials/${materialUnitId}/investigation-full${buildQuery({
      maxDepth: options.maxDepth ?? 5,
      parameterPage: options.parameterPage ?? 1,
      parameterPageSize: options.parameterPageSize ?? 500,
    })}`
  ),

  getInvestigationPdfUrl: (materialUnitId: string) =>
    `${API_BASE_URL}/reports/materials/${materialUnitId}/investigation/pdf`,

  getGenealogyAwareCorrelation: (filters: DashboardFilters) =>
    getJson<GenealogyAwareCorrelationResult>(
      "/analytics/correlations/parameter-defect/genealogy-aware",
      {
        parameterCode: filters.parameterCode || "CastingSpeed",
        defectType: filters.defectType || "SurfaceCrack",
        siteId: filters.siteId,
        fromUtc: filters.fromUtc,
        toUtc: filters.toUtc,
        bins: filters.bins ?? 8,
        minimumObservationsPerBin: filters.minimumObservationsPerBin ?? 3,
        linkMode: filters.linkMode || "DownstreamChildren",
        genealogyDepth: filters.genealogyDepth ?? 3,
      }
    ),

    persistCorrelationRun: (
    filters: DashboardFilters,
    result: GenealogyAwareCorrelationResult
  ) =>
    postJson<any>("/analytics/correlations/runs", {
      correlationType: "GenealogyAwareParameterDefectBinning",
      subjectCode: result.parameterCode,
      outcomeCode: result.defectType,
      score:
        result.bins.length === 0
          ? null
          : Math.max(...result.bins.map((x) => x.liftVsBaseline ?? 0)),
      filtersJson: JSON.stringify(filters),
      resultJson: JSON.stringify(result),
      notes:
        "Persisted from React correlation workspace. This is suspected-contributor evidence, not validated root cause.",
      isSynthetic: true,
      sourceRecordId: null,
    }),

  getCorrelationRuns: (page = 1, pageSize = 25) =>
    getJson<any>("/analytics/correlations/runs", { page, pageSize }),

  getDashboardDefinitions: (includeInactive = false, includeSystemTemplates = true) =>
    getJson<DashboardDefinitionRecord[]>("/analytics/dashboard/definitions", {
      includeInactive,
      includeSystemTemplates,
    }),

  getDashboardDefinition: (dashboardDefinitionId: string) =>
    getJson<DashboardDefinitionRecord>(
      `/analytics/dashboard/definitions/${dashboardDefinitionId}`
    ),

      getConnectorProviderTypes: () =>
    getJson<ProviderTypeRecord[]>("/admin/connectors/provider-types"),

  getConnectionProfiles: (includeInactive = true) =>
    getJson<ConnectionProfileRecord[]>(
      `/admin/connectors/connection-profiles?includeInactive=${includeInactive}`
    ),

  createConnectionProfile: (request: CreateConnectionProfileRequest) =>
    postJson<ConnectionProfileRecord>(
      "/admin/connectors/connection-profiles",
      request
    ),

    updateConnectionProfile: (
    id: string,
    request: {
      connectionProfileName: string;
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
    }
  ) =>
    putJson<ConnectionProfileRecord>(
      `/admin/connectors/connection-profiles/${id}`,
      request
    ),
    
  testConnectionProfile: (id: string) =>
    postJson<ConnectionProfileRecord>(
      `/admin/connectors/connection-profiles/${id}/test`,
      {}
    ),

  activateConnectionProfile: (id: string) =>
    patchJson<ConnectionProfileRecord>(
      `/admin/connectors/connection-profiles/${id}/activate`,
      {}
    ),

  deactivateConnectionProfile: (id: string) =>
    patchJson<ConnectionProfileRecord>(
      `/admin/connectors/connection-profiles/${id}/deactivate`,
      {}
    ),

  getSourceDatasets: (connectionProfileId?: string, includeInactive = true) => {
    const params = new URLSearchParams();
    params.set("includeInactive", String(includeInactive));

    if (connectionProfileId) {
      params.set("connectionProfileId", connectionProfileId);
    }

    return getJson<SourceDatasetDefinitionRecord[]>(
      `/admin/connectors/datasets?${params.toString()}`
    );
  },

  createSourceDataset: (request: CreateSourceDatasetDefinitionRequest) =>
    postJson<SourceDatasetDefinitionRecord>(
      "/admin/connectors/datasets",
      request
    ),

  discoverCsvSchema: (datasetId: string, request: CsvSchemaDiscoveryRequest) =>
    postJson<CsvSchemaDiscoveryResult>(
      `/admin/connectors/datasets/${datasetId}/discover-csv-schema`,
      request
    ),

  previewCsv: (datasetId: string, request: CsvPreviewRequest) =>
    postJson<CsvPreviewResult>(
      `/admin/connectors/datasets/${datasetId}/preview-csv`,
      request
    ),

  importCsvSnapshot: (datasetId: string, request: CsvImportSnapshotRequest) =>
    postJson<CsvImportSnapshotResult>(
      `/admin/connectors/datasets/${datasetId}/import-csv-snapshot`,
      request
    ),

  createDashboardDefinition: (payload: {
    dashboardCode: string;
    name: string;
    description?: string | null;
    layoutJson?: string | null;
    isDefault: boolean;
    isSystemTemplate: boolean;
    isSynthetic: boolean;
    sourceSystem?: string | null;
    sourceRecordId?: string | null;
  }) => postJson<{ id: string }>("/analytics/dashboard/definitions", payload),

  updateDashboardLayout: (dashboardDefinitionId: string, layoutJson: string) =>
    requestJson<any>(
      `/analytics/dashboard/definitions/${dashboardDefinitionId}/layout`,
      {
        method: "PATCH",
        body: JSON.stringify({ layoutJson }),
      }
    ),

  createDashboardWidgetDefinition: (
    dashboardDefinitionId: string,
    payload: CreateDashboardWidgetDefinitionPayload
  ) =>
    postJson<{ id: string }>(
      `/analytics/dashboard/definitions/${dashboardDefinitionId}/widgets`,
      payload
    ),

  updateDashboardWidgetDefinition: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: {
      widgetTitle: string;
      widgetType: string;
      chartType: string;
      dimensionCode: string;
      measureCode: string;
      parameterCode?: string | null;
      filterJson?: string | null;
      displayOptionsJson?: string | null;
      isActive?: boolean | null;
    }
  ) =>
    requestJson<any>(
      `/analytics/dashboard/definitions/${dashboardDefinitionId}/widgets/${widgetDefinitionId}`,
      {
        method: "PUT",
        body: JSON.stringify(payload),
      }
    ),

  updateDashboardWidgetLayout: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    layoutJson: string,
    sortOrder?: number
  ) =>
    requestJson<any>(
      `/analytics/dashboard/definitions/${dashboardDefinitionId}/widgets/${widgetDefinitionId}/layout`,
      {
        method: "PATCH",
        body: JSON.stringify({ layoutJson, sortOrder }),
      }
    ),

  cloneDashboardWidgetDefinition: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string,
    payload: { widgetCode?: string | null; widgetTitle?: string | null; sortOrder?: number | null }
  ) =>
    postJson<{ id: string }>(
      `/analytics/dashboard/definitions/${dashboardDefinitionId}/widgets/${widgetDefinitionId}/clone`,
      payload
    ),

  deactivateDashboardWidgetDefinition: (
    dashboardDefinitionId: string,
    widgetDefinitionId: string
  ) =>
    requestJson<any>(
      `/analytics/dashboard/definitions/${dashboardDefinitionId}/widgets/${widgetDefinitionId}`,
      { method: "DELETE" }
    ),

 ensureSystemDashboardTemplates: () =>
  postJson<any>("/analytics/dashboard/definitions/system-templates/ensure"),

repairSystemDashboardTemplates: () =>
  postJson<{ repaired: number; repairedAtUtc: string }>(
    "/analytics/dashboard/definitions/system-templates/repair"
  ),

    getSchemaViews: (includeInactive = true) =>
    getJson<SchemaViewDefinitionRecord[]>(
      `/admin/schema-configuration/views?includeInactive=${includeInactive}`
    ),

  createSchemaView: (request: CreateSchemaViewDefinitionRequest) =>
    postJson<SchemaViewDefinitionRecord>(
      "/admin/schema-configuration/views",
      request
    ),

  updateSchemaView: (id: string, request: UpdateSchemaViewDefinitionRequest) =>
    putJson<SchemaViewDefinitionRecord>(
      `/admin/schema-configuration/views/${id}`,
      request
    ),

  previewSchemaView: (id: string, request: SchemaViewPreviewRequest) =>
    postJson<SchemaViewPreviewResult>(
      `/admin/schema-configuration/views/${id}/preview`,
      request
    ),

  previewAdHocSchemaSql: (request: SchemaViewPreviewRequest) =>
    postJson<SchemaViewPreviewResult>(
      "/admin/schema-configuration/views/preview",
      request
    ),

  approveSchemaView: (id: string) =>
    postJson<SchemaViewDefinitionRecord>(
      `/admin/schema-configuration/views/${id}/approve`,
      {}
    ),

  getKpiDefinitions: (includeInactive = true) =>
    getJson<KpiDefinitionRecord[]>(
      `/admin/schema-configuration/kpis?includeInactive=${includeInactive}`
    ),

  createKpiDefinition: (request: CreateKpiDefinitionRequest) =>
    postJson<KpiDefinitionRecord>(
      "/admin/schema-configuration/kpis",
      request
    ),

    
};


