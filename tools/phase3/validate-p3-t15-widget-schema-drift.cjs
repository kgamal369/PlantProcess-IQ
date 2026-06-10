
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

if (!page.includes("data-testid=\"p3-t15-heatmap\"")) fail("missing heatmap test id");
if (!page.includes("data-testid=\"p3-t15-filter-search\"")) fail("missing search filter control");
if (!page.includes("data-testid=\"p3-t15-sort-direction\"")) fail("missing sort direction control");
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
const backendJoined = backendCsFiles.map((file) => fs.readFileSync(file, "utf8")).join("\n");

if (!backendJoined.includes("DashboardWidgetQueryDto")) fail("backend DashboardWidgetQueryDto not found");
if (!backendJoined.includes("DashboardWidgetQueryOptionsDto")) fail("backend DashboardWidgetQueryOptionsDto not found");
if (!backendJoined.includes("SortDirection")) fail("backend widget sortDirection contract not found");
if (!backendJoined.includes("FilterJson")) fail("backend DashboardWidgetDefinition FilterJson not found");
if (!backendJoined.includes("DisplayOptionsJson")) fail("backend DashboardWidgetDefinition DisplayOptionsJson not found");
if (!backendJoined.includes("Heatmap")) fail("backend heatmap chart type not found");

console.log("[GREEN] P3-T15 static validation passed.");
