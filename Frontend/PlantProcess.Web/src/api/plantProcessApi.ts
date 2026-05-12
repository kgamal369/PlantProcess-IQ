import { API_BASE_URL } from "./apiConfig";

export type SortDirection = "asc" | "desc";

export interface DashboardFilters {
  siteId?: string;
  areaId?: string;
  equipmentId?: string;
  materialCode?: string;
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

type PrimitiveQueryValue = string | number | boolean | null | undefined;
type QueryParams = Record<string, PrimitiveQueryValue>;

function buildQuery(params?: QueryParams): string {
  if (!params) return "";
  const searchParams = new URLSearchParams();

  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === "") return;
    searchParams.set(key, String(value));
  });

  const query = searchParams.toString();
  return query ? `?${query}` : "";
}

async function requestJson<T>(
  path: string,
  options?: RequestInit,
  timeoutMs = 30_000
): Promise<T> {
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), timeoutMs);

  try {
    const response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      signal: controller.signal,
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        ...(options?.headers ?? {}),
      },
    });

    const text = await response.text();

    if (!response.ok) {
      throw new Error(
        text || `PlantProcess API request failed: ${response.status} ${response.statusText}`
      );
    }

    if (!text) return undefined as T;
    return JSON.parse(text) as T;
  } finally {
    window.clearTimeout(timeout);
  }
}

function getJson<T>(path: string, params?: QueryParams): Promise<T> {
  return requestJson<T>(`${path}${buildQuery(params)}`, { method: "GET" });
}

function postJson<T>(path: string, body?: unknown): Promise<T> {
  return requestJson<T>(path, {
    method: "POST",
    body: body === undefined ? undefined : JSON.stringify(body),
  });
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

export const plantProcessApi = {
  apiBaseUrl: API_BASE_URL,

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

  getMaterialInvestigation: (materialUnitId: string) =>
    getJson<any>(`/reports/materials/${materialUnitId}/investigation`),

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
};