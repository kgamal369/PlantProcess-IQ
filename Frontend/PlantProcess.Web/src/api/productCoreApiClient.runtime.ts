import { API_BASE_URL } from "./apiConfig";
import {
  buildQuery,
  deleteJson,
  getJson,
  patchJson,
  postJson,
  putJson,
  requestJson,
  type QueryParams,
} from "./productApiHardening";
// PPIQ_PHASE5B_PRODUCT_CORE_TYPES_SPLIT
// Exported DTO/filter/read-model types moved to ./product-core/types.
export type {
  AdminJobMonitorRow,
  AdminJobsMonitor,
  AdminLatestImportBatch,
  AdminMetricCard,
  AdminOverview,
  AdminStatusCount,
  ConnectionProfileRecord,
  CreateConnectionProfileRequest,
  CreateDashboardWidgetDefinitionPayload,
  CreateKpiDefinitionRequest,
  CreateSchemaViewDefinitionRequest,
  CreateSourceDatasetDefinitionRequest,
  CsvImportSnapshotRequest,
  CsvImportSnapshotResult,
  CsvPreviewRequest,
  CsvPreviewResult,
  CsvSchemaDiscoveryRequest,
  CsvSchemaDiscoveryResult,
  DashboardChartTypeMetadata,
  DashboardCompatibilityRule,
  DashboardDefinitionRecord,
  DashboardDimensionMetadata,
  DashboardFilterMetadata,
  DashboardFilters,
  DashboardMaterialRow,
  DashboardMeasureMetadata,
  DashboardMetadata,
  DashboardPurposeMetadata,
  DashboardQuerySafetyLimits,
  DashboardReferenceData,
  DashboardWidgetColumn,
  DashboardWidgetDefinitionRecord,
  DashboardWidgetFilters,
  DashboardWidgetQuery,
  DashboardWidgetQueryOptions,
  DashboardWidgetQueryResult,
  DashboardWidgetResolved,
  DashboardWorkspace,
  DbConfigurationSourceSystem,
  DbConfigurationSummary,
  GenealogyAwareCorrelationBin,
  GenealogyAwareCorrelationResult,
  JobActionResponse,
  JobRunHistoryRecord,
  KpiDefinitionRecord,
  MaterialInvestigationRequestOptions,
  PagedResult,
  PlannedProvider,
  ProviderTypeRecord,
  ReferenceItem,
  SchemaConfigurationSummary,
  SchemaMappingSummary,
  SchemaViewDefinitionRecord,
  SchemaViewPreviewColumn,
  SchemaViewPreviewRequest,
  SchemaViewPreviewResult,
  SortDirection,
  SourceDatasetDefinitionRecord,
  SourceFieldDefinitionRecord,
  SourceObjectCoverage,
  TwoStageImportModel,
  TwoStageImportStage,
  UpdateConnectionImportScheduleRequest,
  UpdateMappingRefreshScheduleRequest,
  UpdateSchemaViewDefinitionRequest,
  WidgetQueryExpressionRequest,
  WidgetQueryExpressionResult,
} from "./product-core/types";

import type {
  AdminJobsMonitor,
  AdminOverview,
  ConnectionProfileRecord,
  CreateConnectionProfileRequest,
  CreateDashboardWidgetDefinitionPayload,
  CreateKpiDefinitionRequest,
  CreateSchemaViewDefinitionRequest,
  CreateSourceDatasetDefinitionRequest,
  CsvImportSnapshotRequest,
  CsvImportSnapshotResult,
  CsvPreviewRequest,
  CsvPreviewResult,
  CsvSchemaDiscoveryRequest,
  CsvSchemaDiscoveryResult,
  DashboardDefinitionRecord,
  DashboardFilters,
  DashboardMaterialRow,
  DashboardMetadata,
  DashboardReferenceData,
  DashboardWidgetQuery,
  DashboardWidgetQueryResult,
  DashboardWorkspace,
  DbConfigurationSummary,
  GenealogyAwareCorrelationResult,
  JobActionResponse,
  JobRunHistoryRecord,
  KpiDefinitionRecord,
  MaterialInvestigationRequestOptions,
  PagedResult,
  ProviderTypeRecord,
  SchemaConfigurationSummary,
  SchemaViewDefinitionRecord,
  SchemaViewPreviewRequest,
  SchemaViewPreviewResult,
  SourceDatasetDefinitionRecord,
  TwoStageImportModel,
  UpdateConnectionImportScheduleRequest,
  UpdateMappingRefreshScheduleRequest,
  UpdateSchemaViewDefinitionRequest,
} from "./product-core/types";


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


export const productApi = {
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

  updateDashboardDefinition: (
    dashboardDefinitionId: string,
    payload: {
      name: string;
      description?: string | null;
      layoutJson?: string | null;
      isActive?: boolean | null;
      isDefault?: boolean | null;
    }
  ) =>
    putJson<DashboardDefinitionRecord>(
      `/analytics/dashboard/definitions/${dashboardDefinitionId}`,
      payload
    ),

  deleteDashboardDefinition: (dashboardDefinitionId: string) =>
    requestJson<any>(
      `/analytics/dashboard/definitions/${dashboardDefinitionId}`,
      {
        method: "DELETE",
      }
    ),

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


