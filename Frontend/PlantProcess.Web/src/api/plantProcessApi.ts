const API_BASE_URL =
  (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, "") ||
  "http://localhost:5063";

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
};