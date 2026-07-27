// PlantProcess IQ Phase 5B-1 domain type module.
// Generated from productCoreApiClient.implementation.ts exported DTO/filter/read-model declarations.
// Runtime API behavior remains in productCoreApiClient.implementation.ts.

import type { PagedResult, ReferenceItem, SortDirection } from "./shared-types";

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
  queryExpression?: string | null;
  expressionEnabled?: boolean;
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
  queryExpression?: string | null;
}

export type WidgetQueryExpressionRequest = {
  expression: string;
  filters?: DashboardWidgetFilters | null;
  options?: DashboardWidgetQueryOptions | null;
};

export type WidgetQueryExpressionResult = DashboardWidgetQueryResult;
