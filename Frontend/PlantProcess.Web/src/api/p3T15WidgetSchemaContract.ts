
export const P3_T15_WIDGET_SCHEMA_DRIFT_ROOT_CAUSE_FIXED =
  "P3_T15_WIDGET_SCHEMA_DRIFT_ROOT_CAUSE_FIXED";

type RawRecord = Record<string, unknown>;

export type DashboardWidgetDefinitionContract = {
  dashboardDefinitionId: string;
  widgetDefinitionId: string;
  widgetCode: string;
  widgetTitle: string;
  widgetType: "chart" | "kpi" | "table" | string;
  chartType: "bar" | "line" | "area" | "pie" | "donut" | "scatter" | "heatmap" | "table" | "kpi" | string;
  dimensionCode: string;
  measureCode: string;
  parameterCode?: string;
  filterJson: string;
  layoutJson: string;
  displayOptionsJson: string;
  sortOrder: number;
  isActive: boolean;
  sourceSystem?: string;
  sourceRecordId?: string;
};

export type DashboardWidgetQueryContract = {
  widgetType: string;
  chartType: string;
  dimensionCode?: string;
  measureCode?: string;
  parameterCode?: string;
  filters: RawRecord;
  options: {
    maxRows: number;
    rawRowLimit: number;
    sortDirection: "asc" | "desc";
    includeWarnings: boolean;
  };
};

export type WidgetSchemaValidationResult = {
  isValid: boolean;
  errors: string[];
  normalized?: DashboardWidgetDefinitionContract;
};

export const requiredWidgetDefinitionKeys = [
  "widgetCode",
  "widgetTitle",
  "widgetType",
  "chartType",
  "dimensionCode",
  "measureCode",
] as const;

export const backendAcceptedChartTypes = [
  "bar",
  "line",
  "area",
  "pie",
  "donut",
  "scatter",
  "heatmap",
  "table",
  "kpi",
] as const;

export const backendWidgetDefinitionContractKeys = [
  "DashboardDefinitionId",
  "WidgetDefinitionId",
  "Id",
  "WidgetCode",
  "WidgetTitle",
  "WidgetType",
  "ChartType",
  "DimensionCode",
  "MeasureCode",
  "ParameterCode",
  "FilterJson",
  "LayoutJson",
  "DisplayOptionsJson",
  "SortOrder",
  "IsActive",
  "SourceSystem",
  "SourceRecordId",
] as const;

function pick(raw: RawRecord, ...keys: string[]): unknown {
  for (const key of keys) {
    if (raw[key] !== undefined && raw[key] !== null) return raw[key];

    const lower = key.charAt(0).toLowerCase() + key.slice(1);
    if (raw[lower] !== undefined && raw[lower] !== null) return raw[lower];

    const upper = key.charAt(0).toUpperCase() + key.slice(1);
    if (raw[upper] !== undefined && raw[upper] !== null) return raw[upper];
  }

  return undefined;
}

function toText(value: unknown, fallback = ""): string {
  if (value === undefined || value === null) return fallback;
  return String(value).trim();
}

function toNumber(value: unknown, fallback = 0): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function toBoolean(value: unknown, fallback = true): boolean {
  if (typeof value === "boolean") return value;
  if (typeof value === "string") {
    if (value.toLowerCase() === "true") return true;
    if (value.toLowerCase() === "false") return false;
  }
  return fallback;
}

export function parseJsonObject(value: unknown): RawRecord {
  if (!value) return {};
  if (typeof value === "object" && !Array.isArray(value)) return value as RawRecord;

  if (typeof value !== "string") return {};

  const trimmed = value.trim();
  if (!trimmed) return {};

  try {
    const parsed = JSON.parse(trimmed);
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      return parsed as RawRecord;
    }
  } catch {
    return {};
  }

  return {};
}

export function normalizeDashboardWidgetDefinition(raw: RawRecord): DashboardWidgetDefinitionContract {
  const dashboardDefinitionId = toText(pick(raw, "DashboardDefinitionId", "dashboardDefinitionId"), "unknown-dashboard");
  const widgetDefinitionId = toText(pick(raw, "WidgetDefinitionId", "widgetDefinitionId", "Id", "id"), "unknown-widget");

  return {
    dashboardDefinitionId,
    widgetDefinitionId,
    widgetCode: toText(pick(raw, "WidgetCode", "widgetCode")),
    widgetTitle: toText(pick(raw, "WidgetTitle", "widgetTitle")),
    widgetType: toText(pick(raw, "WidgetType", "widgetType"), "chart"),
    chartType: toText(pick(raw, "ChartType", "chartType"), "bar"),
    dimensionCode: toText(pick(raw, "DimensionCode", "dimensionCode")),
    measureCode: toText(pick(raw, "MeasureCode", "measureCode")),
    parameterCode: toText(pick(raw, "ParameterCode", "parameterCode"), "") || undefined,
    filterJson: JSON.stringify(parseJsonObject(pick(raw, "FilterJson", "filterJson"))),
    layoutJson: JSON.stringify(parseJsonObject(pick(raw, "LayoutJson", "layoutJson"))),
    displayOptionsJson: JSON.stringify(parseJsonObject(pick(raw, "DisplayOptionsJson", "displayOptionsJson"))),
    sortOrder: toNumber(pick(raw, "SortOrder", "sortOrder"), 0),
    isActive: toBoolean(pick(raw, "IsActive", "isActive"), true),
    sourceSystem: toText(pick(raw, "SourceSystem", "sourceSystem"), "") || undefined,
    sourceRecordId: toText(pick(raw, "SourceRecordId", "sourceRecordId"), "") || undefined,
  };
}

export function validateWidgetDefinitionSchema(raw: RawRecord): WidgetSchemaValidationResult {
  const normalized = normalizeDashboardWidgetDefinition(raw);
  const errors: string[] = [];

  for (const key of requiredWidgetDefinitionKeys) {
    if (!String(normalized[key] ?? "").trim()) {
      errors.push("Missing required widget field: " + key);
    }
  }

  if (!backendAcceptedChartTypes.includes(normalized.chartType.toLowerCase() as any)) {
    errors.push("Unsupported chart type: " + normalized.chartType);
  }

  if (normalized.chartType.toLowerCase() === "heatmap") {
    if (!normalized.dimensionCode) errors.push("Heatmap requires dimensionCode.");
    if (!normalized.measureCode) errors.push("Heatmap requires measureCode.");
  }

  return {
    isValid: errors.length === 0,
    errors,
    normalized,
  };
}

export function buildWidgetQueryFromDefinition(
  definition: DashboardWidgetDefinitionContract,
): DashboardWidgetQueryContract {
  const filters = parseJsonObject(definition.filterJson);
  const options = parseJsonObject(definition.displayOptionsJson);

  const sortDirection =
    String(options.sortDirection ?? options.SortDirection ?? "desc").toLowerCase() === "asc"
      ? "asc"
      : "desc";

  return {
    widgetType: definition.widgetType,
    chartType: definition.chartType,
    dimensionCode: definition.dimensionCode || undefined,
    measureCode: definition.measureCode || undefined,
    parameterCode: definition.parameterCode,
    filters,
    options: {
      maxRows: Math.max(1, Math.min(200, toNumber(options.maxRows ?? options.MaxRows, 50))),
      rawRowLimit: Math.max(1, Math.min(50000, toNumber(options.rawRowLimit ?? options.RawRowLimit, 10000))),
      sortDirection,
      includeWarnings: toBoolean(options.includeWarnings ?? options.IncludeWarnings, true),
    },
  };
}

export type WidgetHeatmapRow = RawRecord;

export type WidgetHeatmapCell = {
  id: string;
  x: string;
  y: string;
  value: number;
  label: string;
  group: string;
  intensity: number;
};

export type HeatmapFilterSortState = {
  search?: string;
  minValue?: number;
  sortBy?: "x" | "y" | "value";
  direction?: "asc" | "desc";
};

export function buildWidgetHeatmapCells(
  rows: WidgetHeatmapRow[],
  xKey: string,
  yKey: string,
  valueKey: string,
): WidgetHeatmapCell[] {
  const values = rows.map((row) => toNumber(row[valueKey], 0));
  const max = Math.max(1, ...values.map((x) => Math.abs(x)));

  return rows.map((row, index) => {
    const x = toText(row[xKey], "unknown-x");
    const y = toText(row[yKey], "unknown-y");
    const value = toNumber(row[valueKey], 0);

    return {
      id: x + "::" + y + "::" + index,
      x,
      y,
      value,
      label: x + " / " + y,
      group: y,
      intensity: Math.min(1, Math.abs(value) / max),
    };
  });
}

export function filterSortHeatmapCells(
  cells: WidgetHeatmapCell[],
  state: HeatmapFilterSortState,
): WidgetHeatmapCell[] {
  const search = String(state.search ?? "").trim().toLowerCase();
  const minValue = typeof state.minValue === "number" ? state.minValue : undefined;
  const sortBy = state.sortBy ?? "value";
  const direction = state.direction ?? "desc";

  let next = cells.slice();

  if (search) {
    next = next.filter((cell) =>
      cell.x.toLowerCase().includes(search) ||
      cell.y.toLowerCase().includes(search) ||
      cell.label.toLowerCase().includes(search)
    );
  }

  if (typeof minValue === "number") {
    next = next.filter((cell) => cell.value >= minValue);
  }

  next.sort((a, b) => {
    const result =
      sortBy === "value"
        ? a.value - b.value
        : String(a[sortBy]).localeCompare(String(b[sortBy]));

    return direction === "asc" ? result : -result;
  });

  return next;
}

export function heatmapSeriesSignature(cells: WidgetHeatmapCell[]): string {
  return cells.map((cell) => cell.id + ":" + cell.value.toFixed(3)).join("|");
}

export function schemaDriftSummary(raw: RawRecord) {
  const validation = validateWidgetDefinitionSchema(raw);
  const normalized = validation.normalized!;

  return {
    isValid: validation.isValid,
    errors: validation.errors,
    schemaVersion: "p3-t15-dashboard-widget-definition-v1",
    widgetCode: normalized.widgetCode,
    chartType: normalized.chartType,
    query: buildWidgetQueryFromDefinition(normalized),
  };
}

export const p3t15DemoBackendWidget = {
  DashboardDefinitionId: "dashboard-demo-quality",
  WidgetDefinitionId: "widget-demo-heatmap",
  WidgetCode: "P3T15_HEATMAP_SCHEMA_DRIFT_PROOF",
  WidgetTitle: "Defect heatmap by line and day",
  WidgetType: "chart",
  ChartType: "heatmap",
  DimensionCode: "equipment",
  MeasureCode: "defectRate",
  ParameterCode: null,
  FilterJson: "{\"sourceSystem\":\"demo\",\"riskClass\":\"high\"}",
  LayoutJson: "{\"x\":0,\"y\":0,\"w\":8,\"h\":5}",
  DisplayOptionsJson: "{\"maxRows\":50,\"rawRowLimit\":10000,\"sortDirection\":\"desc\",\"includeWarnings\":true}",
  SortOrder: 1,
  IsActive: true,
  SourceSystem: "p3-t15-contract-test",
  SourceRecordId: "schema-drift-hotfix-206-207-root-cause",
};

export const p3t15DemoHeatmapRows = [
  { equipment: "Caster 1", day: "Mon", defectRate: 0.16 },
  { equipment: "Caster 1", day: "Tue", defectRate: 0.27 },
  { equipment: "Caster 2", day: "Mon", defectRate: 0.34 },
  { equipment: "Caster 2", day: "Tue", defectRate: 0.11 },
  { equipment: "Mill 1", day: "Mon", defectRate: 0.45 },
  { equipment: "Mill 1", day: "Tue", defectRate: 0.22 },
];
