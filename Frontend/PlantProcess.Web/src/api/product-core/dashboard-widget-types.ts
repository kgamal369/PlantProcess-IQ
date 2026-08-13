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

/**
 * T-046. `availability` is what the PRODUCT has; compatibility is what this
 * DATA supports. They are separate facts and a surface that receives only the
 * second cannot explain the difference to an author.
 *
 * "implemented" means the renderer exists today. "not-yet-available" means the
 * type is part of the seventeen-type product grammar and cannot be drawn yet -
 * which is a different sentence from "this chart makes no sense here".
 */
export type DashboardChartAvailability = "implemented" | "not-yet-available";

// T-046. THE SWITCHER'S THREE STATES.
//
//   available    - the server allows this type for this binding and the
//                  renderer exists. Selectable.
//   unavailable  - the product does not draw this type yet. A build fact.
//                  Nothing about the author's dimension or measure is wrong.
//   incompatible - the renderer exists, and the SERVER refused this type for
//                  this binding. The sentence shown is the server's.
//
// The two refusals must never wear the same explanation. Telling an author
// their binding is wrong when the truth is that we have not built the chart
// sends them to change the one thing that cannot help.
export type ChartOptionState = "available" | "unavailable" | "incompatible";

export interface ChartSwitcherOption {
  code: string;
  label: string;
  state: ChartOptionState;
  reason: string | null;
}

// Typed against the published union, so a backend change to the availability
// vocabulary fails the compiler here rather than silently marking every type
// unavailable at runtime.
const IMPLEMENTED: DashboardChartAvailability = "implemented";

// T-046. THE ONLY PLACE SWITCHER OPTIONS ARE DECIDED, AND IT DECIDES NOTHING.
//
// This is a projection of the transport contract, not a compatibility rule.
// It holds no chart code, no measure code and no dimension code: every verdict
// is read from what the server published. Adding a chart type to the product
// grammar changes one backend list and nothing here.
//
// PRECEDENCE. Availability outranks compatibility. If the renderer does not
// exist the binding question is moot, and reporting a structural refusal for a
// chart we simply have not built is a false statement about the author's work.
//
// FAIL CLOSED. With no metadata, or no rule for this binding, the answer is an
// empty list and the card shows no switcher. A guessed switcher is worse than
// no switcher, because it looks like it is working.
export function resolveChartSwitcherOptions(
  chartTypes: DashboardChartTypeMetadata[] | null | undefined,
  rule: DashboardCompatibilityRule | null | undefined,
  activeChartType: string | null | undefined
): ChartSwitcherOption[] {
  if (!chartTypes || chartTypes.length === 0) { return []; }
  if (!rule) { return []; }

  const allowed = new Set<string>(rule.allowedChartTypes ?? []);
  const refused = new Map<string, string>();
  for (const entry of rule.refusedChartTypes ?? []) {
    refused.set(entry.chartTypeCode, entry.reason);
  }

  const options: ChartSwitcherOption[] = [];

  for (const type of chartTypes) {
    // A type the server named in neither list carries no verdict for this
    // binding and is omitted. The type the widget is CURRENTLY drawn as is
    // always listed, so a widget can never render as something its own
    // switcher does not admit exists.
    const named = allowed.has(type.code) || refused.has(type.code);
    if (!named && type.code !== activeChartType) { continue; }

    let state: ChartOptionState;
    let reason: string | null = null;

    if (type.availability !== IMPLEMENTED) {
      state = "unavailable";
    } else if (refused.has(type.code)) {
      state = "incompatible";
      reason = refused.get(type.code) ?? null;
    } else if (allowed.has(type.code)) {
      state = "available";
    } else {
      state = "incompatible";
      reason = null;
    }

    options.push({ code: type.code, label: type.label, state: state, reason: reason });
  }

  return options;
}

export interface DashboardChartTypeMetadata {
  code: string;
  label: string;
  category: string;
  supportsDimension: boolean;
  supportsMeasure: boolean;
  supportsMultipleSeries: boolean;
  supportsParameterSelection: boolean;
  availability: DashboardChartAvailability;
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

/**
 * T-046. A type that is NOT offered arrives with the sentence saying why.
 *
 * Before this the client received only `allowedChartTypes`, so a type that was
 * absent was indistinguishable from a type that had never existed - which is
 * how an unselectable Pareto survived for as long as it did. The switcher can
 * now show an author what it will not offer and what would have to change.
 */
export interface DashboardChartRefusal {
  chartTypeCode: string;
  reason: string;
}

export interface DashboardCompatibilityRule {
  dimensionCode: string;
  measureCode: string;
  allowedChartTypes: string[];
  refusedChartTypes: DashboardChartRefusal[];
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
