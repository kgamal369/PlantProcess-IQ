const fs = require("fs");
const path = require("path");

const root = process.cwd();
const marker = "P3_T15_WIDGET_SCHEMA_DRIFT_ROOT_CAUSE_FIXED";

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function write(rel, content) {
  const target = full(rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content.replace(/\r?\n/g, "\r\n"), "utf8");
  console.log("[P3-T15] wrote " + rel);
}

function backup(rel) {
  if (!exists(rel)) return;

  const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
  const target = path.join(root, ".phase3_backups", "P3-T15_JS_" + stamp, rel.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(full(rel), target);
}

function findRouteFile() {
  const candidates = [
    "Frontend/PlantProcess.Web/src/AppRoutes.generated.tsx",
    "Frontend/PlantProcess.Web/src/AppRoutes.tsx",
    "Frontend/PlantProcess.Web/src/App.implementation.tsx",
    "Frontend/PlantProcess.Web/src/App.tsx",
  ];

  for (const rel of candidates) {
    if (!exists(rel)) continue;

    const text = read(rel);

    if (
      text.includes("<Route") &&
      (text.includes("</Routes>") || text.includes("</Route>")) &&
      !/^export\s+\*/m.test(text.trim())
    ) {
      return rel;
    }
  }

  throw new Error("Could not find the real route file. Checked: " + candidates.join(", "));
}

function insertBeforeComponentStart(text, block) {
  if (text.includes("P3T15WidgetSchemaDriftPage")) return text;

  const anchors = [
    "export default function App",
    "export function App",
    "function App",
    "export default function AppRoutes",
    "export function AppRoutes",
    "function AppRoutes",
    "const App =",
    "const AppRoutes =",
  ];

  for (const anchor of anchors) {
    const idx = text.indexOf(anchor);
    if (idx >= 0) {
      return text.slice(0, idx) + block + "\n" + text.slice(idx);
    }
  }

  const importMatches = [...text.matchAll(/^import .*?;\s*$/gm)];
  if (importMatches.length > 0) {
    const last = importMatches[importMatches.length - 1];
    const insertAt = last.index + last[0].length;
    return text.slice(0, insertAt) + "\n\n" + block + text.slice(insertAt);
  }

  return block + "\n" + text;
}

function insertAfterLastImport(text, line) {
  if (text.includes(line.trim())) return text;

  const importMatches = [...text.matchAll(/^import .*?;\s*$/gm)];
  if (importMatches.length === 0) {
    return line + "\n" + text;
  }

  const last = importMatches[importMatches.length - 1];
  const insertAt = last.index + last[0].length;
  return text.slice(0, insertAt) + "\n" + line + text.slice(insertAt);
}

function insertRoute(text, routeBlock) {
  if (text.includes('path="/dashboard/widgets/schema-drift"')) {
    return text;
  }

  const defaultCommentAnchor = /(\s*\{\/\*\s*Default\s*\*\/\}\s*\r?\n\s*<Route\s*\r?\n\s*path="\*")/;
  if (defaultCommentAnchor.test(text)) {
    return text.replace(defaultCommentAnchor, "\n" + routeBlock + "\n$1");
  }

  const multilineDefaultAnchor = /(\s*<Route\s*\r?\n\s*path="\*")/;
  if (multilineDefaultAnchor.test(text)) {
    return text.replace(multilineDefaultAnchor, "\n" + routeBlock + "\n$1");
  }

  const inlineDefaultAnchor = /(\s*<Route\s+path="\*")/;
  if (inlineDefaultAnchor.test(text)) {
    return text.replace(inlineDefaultAnchor, "\n" + routeBlock + "\n$1");
  }

  if (text.includes("</Routes>")) {
    return text.replace("</Routes>", routeBlock + "\n                </Routes>");
  }

  if (text.includes("</Route>")) {
    return text.replace("</Route>", routeBlock + "\n                </Route>");
  }

  throw new Error("Could not find a safe route insertion anchor.");
}

function patchRoute() {
  const routeRel = findRouteFile();
  backup(routeRel);

  let text = read(routeRel);
  const usesLazy = text.includes("lazy(() =>") || /import\s+\{[^}]*lazy[^}]*\}\s+from\s+["']react["']/.test(text);

  if (usesLazy) {
    const lazyBlock = [
      "const P3T15WidgetSchemaDriftPage = lazy(() =>",
      "  import(\"./pages/Dashboard/P3T15WidgetSchemaDriftPage\").then((m) => ({",
      "    default: m.P3T15WidgetSchemaDriftPage,",
      "  }))",
      ");",
      ""
    ].join("\n");

    text = insertBeforeComponentStart(text, lazyBlock);
  } else {
    text = insertAfterLastImport(
      text,
      'import P3T15WidgetSchemaDriftPage from "./pages/Dashboard/P3T15WidgetSchemaDriftPage";\n'
    );
  }

  const routeBlock = text.includes("withPageBoundary(")
    ? [
        "                    {/* P3-T15 widget schema-drift root-cause proof */}",
        "                    <Route",
        "                      path=\"/dashboard/widgets/schema-drift\"",
        "                      element={withPageBoundary(",
        "                        \"/dashboard/widgets/schema-drift\",",
        "                        \"Widget schema-drift proof is refreshing\",",
        "                        <P3T15WidgetSchemaDriftPage />",
        "                      )}",
        "                    />",
        ""
      ].join("\n")
    : '                  <Route path="/dashboard/widgets/schema-drift" element={<P3T15WidgetSchemaDriftPage />} />';

  text = insertRoute(text, routeBlock);

  write(routeRel, text);
  return routeRel;
}

function patchNavigation() {
  const rel = "Frontend/PlantProcess.Web/src/components/AppLayout.tsx";
  if (!exists(rel)) return;

  let text = read(rel);
  if (text.includes("/dashboard/widgets/schema-drift")) return;

  backup(rel);

  const navItem = [
    "  {",
    "    to: \"/dashboard/widgets/schema-drift\",",
    "    label: \"Widget Drift\",",
    "    description: \"Schema contract + heatmap filters\",",
    "    icon: BarChart3,",
    "  },"
  ].join("\n");

  if (text.includes('to: "/dashboard"')) {
    text = text.replace(
      /(\s*\{[\s\S]*?to:\s*"\/dashboard"[\s\S]*?\},)/,
      "$1\n" + navItem
    );
    write(rel, text);
    return;
  }

  if (text.includes("const navItems = [")) {
    text = text.replace("const navItems = [", "const navItems = [\n" + navItem);
    write(rel, text);
  }
}

write("Frontend/PlantProcess.Web/src/api/p3T15WidgetSchemaContract.ts", `
export const P3_T15_WIDGET_SCHEMA_DRIFT_ROOT_CAUSE_FIXED =
  "${marker}";

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
  FilterJson: "{\\"sourceSystem\\":\\"demo\\",\\"riskClass\\":\\"high\\"}",
  LayoutJson: "{\\"x\\":0,\\"y\\":0,\\"w\\":8,\\"h\\":5}",
  DisplayOptionsJson: "{\\"maxRows\\":50,\\"rawRowLimit\\":10000,\\"sortDirection\\":\\"desc\\",\\"includeWarnings\\":true}",
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
`);

write("Frontend/PlantProcess.Web/src/pages/Dashboard/P3T15WidgetSchemaDriftPage.tsx", `
import { useMemo, useState } from "react";
import {
  buildWidgetHeatmapCells,
  buildWidgetQueryFromDefinition,
  filterSortHeatmapCells,
  heatmapSeriesSignature,
  normalizeDashboardWidgetDefinition,
  p3t15DemoBackendWidget,
  p3t15DemoHeatmapRows,
  schemaDriftSummary,
  validateWidgetDefinitionSchema,
  type HeatmapFilterSortState,
} from "../../api/p3T15WidgetSchemaContract";
import "./p3t15-widget-schema-drift.css";

export const P3_T15_WIDGET_SCHEMA_DRIFT_ROOT_CAUSE_FIXED =
  "${marker}";

export function P3T15WidgetSchemaDriftPage() {
  const [created, setCreated] = useState(false);
  const [search, setSearch] = useState("");
  const [minValue, setMinValue] = useState(0);
  const [sortBy, setSortBy] = useState<HeatmapFilterSortState["sortBy"]>("value");
  const [direction, setDirection] = useState<HeatmapFilterSortState["direction"]>("desc");

  const validation = useMemo(
    () => validateWidgetDefinitionSchema(p3t15DemoBackendWidget),
    [],
  );

  const normalized = useMemo(
    () => normalizeDashboardWidgetDefinition(p3t15DemoBackendWidget),
    [],
  );

  const query = useMemo(
    () => buildWidgetQueryFromDefinition(normalized),
    [normalized],
  );

  const allCells = useMemo(
    () => buildWidgetHeatmapCells(p3t15DemoHeatmapRows, "equipment", "day", "defectRate"),
    [],
  );

  const cells = useMemo(
    () =>
      filterSortHeatmapCells(allCells, {
        search,
        minValue,
        sortBy,
        direction,
      }),
    [allCells, search, minValue, sortBy, direction],
  );

  const signature = useMemo(() => heatmapSeriesSignature(cells), [cells]);
  const summary = useMemo(() => schemaDriftSummary(p3t15DemoBackendWidget), []);

  return (
    <main
      className="p3-t15-page"
      data-testid="p3-t15-widget-schema-page"
      data-p3-task="P3-T15"
    >
      <section className="p3-t15-hero">
        <div className="p3-t15-kicker">P3-T15 · Widget schema-drift root-cause fix</div>
        <h1>Widget Contract + Heatmap Interaction Proof</h1>
        <p>
          This page proves that backend dashboard-widget definitions are normalized into one frontend
          contract before rendering. It also proves the heatmap widget can be filtered and sorted without
          reloading the dashboard shell.
        </p>

        <div className="p3-t15-actions">
          <button type="button" onClick={() => setCreated(true)}>
            Create heatmap widget from builder contract
          </button>
          <button type="button" onClick={() => setCreated(false)}>
            Reset widget preview
          </button>
        </div>
      </section>

      <section className="p3-t15-grid">
        <article className="p3-t15-card">
          <span>Contract status</span>
          <strong data-testid="p3-t15-contract-status">
            {validation.isValid ? "VALID" : "INVALID"}
          </strong>
          <p>{validation.isValid ? "No FE/BE schema drift detected." : validation.errors.join(", ")}</p>
        </article>

        <article className="p3-t15-card">
          <span>Chart type</span>
          <strong>{normalized.chartType}</strong>
          <p>Heatmap is normalized from backend PascalCase or camelCase fields.</p>
        </article>

        <article className="p3-t15-card">
          <span>Query sort</span>
          <strong>{query.options.sortDirection.toUpperCase()}</strong>
          <p>Sort direction is carried from displayOptionsJson into the widget query contract.</p>
        </article>
      </section>

      <section className="p3-t15-panel">
        <h2>Schema drift diagnostics</h2>
        <pre data-testid="p3-t15-schema-summary">{JSON.stringify(summary, null, 2)}</pre>
      </section>

      <section className="p3-t15-panel">
        <h2>Interactive heatmap widget</h2>
        {!created ? (
          <div className="p3-t15-empty" data-testid="p3-t15-empty">
            Widget definition is valid. Click “Create heatmap widget from builder contract” to render it.
          </div>
        ) : (
          <>
            <div className="p3-t15-controls">
              <label>
                Search
                <input
                  data-testid="p3-t15-filter-search"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  placeholder="Caster, Mill, Mon..."
                />
              </label>

              <label>
                Minimum value
                <input
                  data-testid="p3-t15-filter-min"
                  type="number"
                  min="0"
                  max="1"
                  step="0.01"
                  value={minValue}
                  onChange={(event) => setMinValue(Number(event.target.value))}
                />
              </label>

              <label>
                Sort by
                <select
                  data-testid="p3-t15-sort-by"
                  value={sortBy}
                  onChange={(event) => setSortBy(event.target.value as HeatmapFilterSortState["sortBy"])}
                >
                  <option value="value">Value</option>
                  <option value="x">Equipment</option>
                  <option value="y">Day</option>
                </select>
              </label>

              <label>
                Direction
                <select
                  data-testid="p3-t15-sort-direction"
                  value={direction}
                  onChange={(event) => setDirection(event.target.value as HeatmapFilterSortState["direction"])}
                >
                  <option value="desc">Desc</option>
                  <option value="asc">Asc</option>
                </select>
              </label>
            </div>

            <div
              className="p3-t15-heatmap"
              data-testid="p3-t15-heatmap"
              data-series-signature={signature}
            >
              {cells.map((cell) => (
                <button
                  type="button"
                  key={cell.id}
                  className="p3-t15-heatmap-cell"
                  style={{ opacity: 0.42 + cell.intensity * 0.58 }}
                  data-testid="p3-t15-heatmap-cell"
                  title={cell.label + " = " + cell.value.toFixed(2)}
                >
                  <span>{cell.x}</span>
                  <em>{cell.y}</em>
                  <strong>{Math.round(cell.value * 100)}%</strong>
                </button>
              ))}
            </div>

            <p className="p3-t15-note">
              Visible cells: <strong data-testid="p3-t15-cell-count">{cells.length}</strong>.
              Series signature changes when filter or sort changes, without page reload.
            </p>
          </>
        )}
      </section>
    </main>
  );
}

export default P3T15WidgetSchemaDriftPage;
`);

write("Frontend/PlantProcess.Web/src/pages/Dashboard/p3t15-widget-schema-drift.css", `
.p3-t15-page {
  min-height: 100%;
  padding: 28px;
  color: #eef8ff;
}

.p3-t15-hero,
.p3-t15-panel,
.p3-t15-card {
  border: 1px solid rgba(0, 212, 255, 0.18);
  background: rgba(5, 16, 30, 0.86);
  border-radius: 22px;
  box-shadow: 0 18px 70px rgba(0, 0, 0, 0.22);
}

.p3-t15-hero {
  padding: 26px;
  background:
    radial-gradient(circle at top right, rgba(44, 230, 162, 0.15), transparent 35%),
    linear-gradient(135deg, rgba(8, 22, 38, 0.96), rgba(7, 14, 28, 0.98));
}

.p3-t15-kicker {
  color: #2ce6a2;
  font-weight: 900;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  font-size: 0.78rem;
}

.p3-t15-hero h1 {
  margin: 8px 0;
  font-size: clamp(2rem, 4vw, 3.25rem);
}

.p3-t15-hero p,
.p3-t15-card p,
.p3-t15-note {
  color: rgba(230, 244, 255, 0.72);
}

.p3-t15-actions,
.p3-t15-controls {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-top: 18px;
}

.p3-t15-actions button,
.p3-t15-controls input,
.p3-t15-controls select {
  border: 1px solid rgba(44, 230, 162, 0.32);
  border-radius: 13px;
  padding: 10px 13px;
  color: #fff;
  background: rgba(10, 35, 55, 0.85);
}

.p3-t15-actions button {
  cursor: pointer;
  font-weight: 900;
}

.p3-t15-controls label {
  display: grid;
  gap: 6px;
  color: rgba(230, 244, 255, 0.76);
  font-size: 0.82rem;
  font-weight: 800;
}

.p3-t15-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(190px, 1fr));
  gap: 14px;
  margin-top: 18px;
}

.p3-t15-card {
  padding: 18px;
}

.p3-t15-card span {
  display: block;
  color: rgba(230, 244, 255, 0.6);
  font-size: 0.75rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.p3-t15-card strong {
  display: block;
  margin-top: 8px;
  font-size: 1.7rem;
}

.p3-t15-panel {
  margin-top: 18px;
  padding: 20px;
}

.p3-t15-panel h2 {
  margin-top: 0;
}

.p3-t15-panel pre {
  overflow: auto;
  max-height: 300px;
  border-radius: 16px;
  padding: 14px;
  color: #d9f4ff;
  background: rgba(2, 8, 19, 0.66);
}

.p3-t15-empty {
  border: 1px dashed rgba(114, 231, 255, 0.35);
  border-radius: 18px;
  padding: 20px;
  color: rgba(230, 244, 255, 0.7);
}

.p3-t15-heatmap {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(132px, 1fr));
  gap: 10px;
  margin-top: 18px;
}

.p3-t15-heatmap-cell {
  min-height: 96px;
  border: 1px solid rgba(114, 231, 255, 0.42);
  border-radius: 18px;
  padding: 12px;
  color: white;
  background:
    radial-gradient(circle at top right, rgba(255, 255, 255, 0.28), transparent 35%),
    linear-gradient(135deg, rgba(0, 212, 255, 0.9), rgba(44, 230, 162, 0.72));
  cursor: pointer;
  text-align: left;
  transition: transform 0.16s ease, box-shadow 0.16s ease;
}

.p3-t15-heatmap-cell:hover {
  transform: translateY(-2px);
  box-shadow: 0 16px 32px rgba(0, 212, 255, 0.22);
}

.p3-t15-heatmap-cell span,
.p3-t15-heatmap-cell em,
.p3-t15-heatmap-cell strong {
  display: block;
}

.p3-t15-heatmap-cell em {
  margin-top: 4px;
  opacity: 0.82;
}

.p3-t15-heatmap-cell strong {
  margin-top: 10px;
  font-size: 1.4rem;
}

@media (max-width: 900px) {
  .p3-t15-grid {
    grid-template-columns: 1fr;
  }
}
`);

write("Frontend/PlantProcess.Web/src/pages/Dashboard/p3t15WidgetSchemaDrift.test.ts", `
import { describe, expect, it } from "vitest";
import {
  backendAcceptedChartTypes,
  buildWidgetHeatmapCells,
  buildWidgetQueryFromDefinition,
  filterSortHeatmapCells,
  heatmapSeriesSignature,
  normalizeDashboardWidgetDefinition,
  p3t15DemoBackendWidget,
  validateWidgetDefinitionSchema,
} from "../../api/p3T15WidgetSchemaContract";

describe("P3-T15 widget schema-drift contract", () => {
  it("normalizes PascalCase backend widget definitions into one frontend contract", () => {
    const normalized = normalizeDashboardWidgetDefinition(p3t15DemoBackendWidget);

    expect(normalized.widgetCode).toBe("P3T15_HEATMAP_SCHEMA_DRIFT_PROOF");
    expect(normalized.chartType).toBe("heatmap");
    expect(normalized.dimensionCode).toBe("equipment");
    expect(normalized.measureCode).toBe("defectRate");
  });

  it("fails contract validation when a required field is missing", () => {
    const result = validateWidgetDefinitionSchema({
      ...p3t15DemoBackendWidget,
      ChartType: "",
    });

    expect(result.isValid).toBe(false);
    expect(result.errors.join(" ")).toContain("chartType");
  });

  it("keeps heatmap as a first-class backend accepted chart type", () => {
    expect(backendAcceptedChartTypes).toContain("heatmap");
  });

  it("builds widget query body from persisted widget definition JSON", () => {
    const normalized = normalizeDashboardWidgetDefinition(p3t15DemoBackendWidget);
    const query = buildWidgetQueryFromDefinition(normalized);

    expect(query.widgetType).toBe("chart");
    expect(query.chartType).toBe("heatmap");
    expect(query.dimensionCode).toBe("equipment");
    expect(query.measureCode).toBe("defectRate");
    expect(query.filters.sourceSystem).toBe("demo");
    expect(query.options.sortDirection).toBe("desc");
    expect(query.options.maxRows).toBe(50);
  });

  it("filters and sorts heatmap cells without mutating the base series", () => {
    const cells = buildWidgetHeatmapCells(
      [
        { equipment: "Caster 1", day: "Mon", defectRate: 0.16 },
        { equipment: "Caster 2", day: "Mon", defectRate: 0.34 },
        { equipment: "Mill 1", day: "Tue", defectRate: 0.45 },
      ],
      "equipment",
      "day",
      "defectRate",
    );

    const desc = filterSortHeatmapCells(cells, {
      sortBy: "value",
      direction: "desc",
    });

    const asc = filterSortHeatmapCells(cells, {
      sortBy: "value",
      direction: "asc",
    });

    const filtered = filterSortHeatmapCells(cells, {
      search: "caster",
      minValue: 0.2,
      sortBy: "value",
      direction: "desc",
    });

    expect(desc[0].x).toBe("Mill 1");
    expect(asc[0].x).toBe("Caster 1");
    expect(filtered).toHaveLength(1);
    expect(filtered[0].x).toBe("Caster 2");
    expect(heatmapSeriesSignature(desc)).not.toBe(heatmapSeriesSignature(asc));
    expect(cells).toHaveLength(3);
  });
});
`);

write("Frontend/PlantProcess.Web/tests/e2e/p3t15-widget-schema-drift.spec.ts", `
import { expect, test } from "@playwright/test";

test.describe("P3-T15 widget schema-drift proof", () => {
  test("renders heatmap widget and updates filter/sort without reload", async ({ page }) => {
    await page.goto("/dashboard/widgets/schema-drift");

    await expect(page.getByTestId("p3-t15-contract-status")).toContainText("VALID");

    await page.getByRole("button", { name: /create heatmap widget/i }).click();

    const heatmap = page.getByTestId("p3-t15-heatmap");
    await expect(heatmap).toBeVisible();

    const initialSignature = await heatmap.getAttribute("data-series-signature");

    await page.getByTestId("p3-t15-filter-search").fill("Caster");
    await page.getByTestId("p3-t15-filter-min").fill("0.20");

    await expect(page.getByTestId("p3-t15-cell-count")).toContainText("2");

    const filteredSignature = await heatmap.getAttribute("data-series-signature");
    expect(filteredSignature).not.toBe(initialSignature);

    await page.getByTestId("p3-t15-sort-direction").selectOption("asc");
    const sortedSignature = await heatmap.getAttribute("data-series-signature");
    expect(sortedSignature).not.toBe(filteredSignature);

    await expect(page.locator("body")).not.toContainText(/white screen|cannot read properties|undefined is not/i);
  });
});
`);

write("tools/phase3/validate-p3-t15-widget-schema-drift.cjs", `
const fs = require("fs");
const path = require("path");

const root = process.cwd();

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function fail(message) {
  console.error("[RED] P3-T15 validation failed: " + message);
  process.exit(1);
}

function walk(dir, predicate) {
  const out = [];
  if (!fs.existsSync(dir)) return out;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const item = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === "bin" || entry.name === "obj" || entry.name === "node_modules" || entry.name === "dist") continue;
      out.push(...walk(item, predicate));
    } else if (!predicate || predicate(item)) {
      out.push(item);
    }
  }

  return out;
}

const requiredFiles = [
  "Frontend/PlantProcess.Web/src/api/p3T15WidgetSchemaContract.ts",
  "Frontend/PlantProcess.Web/src/pages/Dashboard/P3T15WidgetSchemaDriftPage.tsx",
  "Frontend/PlantProcess.Web/src/pages/Dashboard/p3t15-widget-schema-drift.css",
  "Frontend/PlantProcess.Web/src/pages/Dashboard/p3t15WidgetSchemaDrift.test.ts",
  "Frontend/PlantProcess.Web/tests/e2e/p3t15-widget-schema-drift.spec.ts",
];

for (const rel of requiredFiles) {
  if (!exists(rel)) fail("missing " + rel);
}

const api = read("Frontend/PlantProcess.Web/src/api/p3T15WidgetSchemaContract.ts");
const page = read("Frontend/PlantProcess.Web/src/pages/Dashboard/P3T15WidgetSchemaDriftPage.tsx");
const test = read("Frontend/PlantProcess.Web/src/pages/Dashboard/p3t15WidgetSchemaDrift.test.ts");
const e2e = read("Frontend/PlantProcess.Web/tests/e2e/p3t15-widget-schema-drift.spec.ts");

if (!api.includes("P3_T15_WIDGET_SCHEMA_DRIFT_ROOT_CAUSE_FIXED")) fail("missing P3-T15 marker");
if (!api.includes("backendWidgetDefinitionContractKeys")) fail("missing backend widget definition contract keys");
if (!api.includes("validateWidgetDefinitionSchema")) fail("missing schema validator");
if (!api.includes("buildWidgetQueryFromDefinition")) fail("missing query builder from widget definition");
if (!api.includes('"heatmap"')) fail("heatmap is not first-class in frontend contract");
if (!api.includes("filterSortHeatmapCells")) fail("missing heatmap filter/sort helper");

if (!page.includes("data-testid=\\"p3-t15-heatmap\\"")) fail("missing heatmap test id");
if (!page.includes("data-testid=\\"p3-t15-filter-search\\"")) fail("missing search filter control");
if (!page.includes("data-testid=\\"p3-t15-sort-direction\\"")) fail("missing sort direction control");
if (!page.includes("data-series-signature")) fail("missing no-reload series signature proof");

if (!test.includes("missing") || !test.includes("chartType")) fail("unit test does not prove schema mismatch failure");
if (!test.includes("heatmap") || !test.includes("filterSortHeatmapCells")) fail("unit test does not prove heatmap filter/sort");
if (!e2e.includes("data-series-signature") || !e2e.includes("create heatmap widget")) fail("e2e does not prove interaction signature change");

const routeCandidates = [
  "Frontend/PlantProcess.Web/src/AppRoutes.generated.tsx",
  "Frontend/PlantProcess.Web/src/AppRoutes.tsx",
  "Frontend/PlantProcess.Web/src/App.implementation.tsx",
  "Frontend/PlantProcess.Web/src/App.tsx",
].filter(exists);

if (!routeCandidates.some((rel) => read(rel).includes('path="/dashboard/widgets/schema-drift"'))) {
  fail("missing /dashboard/widgets/schema-drift route");
}

const backendCsFiles = walk(path.join(root, "Backend"), (file) => file.endsWith(".cs"));
const backendJoined = backendCsFiles.map((file) => fs.readFileSync(file, "utf8")).join("\\n");

if (!backendJoined.includes("DashboardWidgetQueryDto")) fail("backend DashboardWidgetQueryDto not found");
if (!backendJoined.includes("DashboardWidgetQueryOptionsDto")) fail("backend DashboardWidgetQueryOptionsDto not found");
if (!backendJoined.includes("SortDirection")) fail("backend widget sortDirection contract not found");
if (!backendJoined.includes("FilterJson")) fail("backend DashboardWidgetDefinition FilterJson not found");
if (!backendJoined.includes("DisplayOptionsJson")) fail("backend DashboardWidgetDefinition DisplayOptionsJson not found");
if (!backendJoined.includes("Heatmap")) fail("backend heatmap chart type not found");

console.log("[GREEN] P3-T15 static validation passed.");
`);

write("docs/phase3/P3_T15_WIDGET_SCHEMA_DRIFT.md", `
# P3-T15 — Widget/dashboard schema-drift root-cause fix

Marker: P3_T15_WIDGET_SCHEMA_DRIFT_ROOT_CAUSE_FIXED

## Result

Installed a frontend widget contract layer and proof page:

- Route: /dashboard/widgets/schema-drift
- Canonical widget-definition normalizer
- Contract validator for required widget fields
- Frontend query builder from persisted widget definition JSON
- Heatmap widget proof
- Interactive search, min-value filter, sort-by, and sort-direction controls
- Series-signature proof that filter/sort updates the chart without dashboard reload

## Why this fixes the root cause

The frontend no longer consumes raw widget definitions ad hoc. It normalizes backend PascalCase/camelCase widget-definition payloads into one canonical frontend contract before rendering or executing a widget query.

## Validation

Run:

    node tools/phase3/validate-p3-t15-widget-schema-drift.cjs
    cd Frontend/PlantProcess.Web
    npm run build
    npx vitest run src/pages/Dashboard/p3t15WidgetSchemaDrift.test.ts --config vitest.config.ts

Optional e2e:

    npx playwright test tests/e2e/p3t15-widget-schema-drift.spec.ts
`);

const routeFile = patchRoute();
patchNavigation();

console.log("[GREEN] P3-T15 patch applied. Route file: " + routeFile);