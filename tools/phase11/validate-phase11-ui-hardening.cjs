const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function full(relativePath) {
  return path.join(root, relativePath);
}

function exists(relativePath) {
  return fs.existsSync(full(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(full(relativePath), "utf8");
}

function lineCount(relativePath) {
  return read(relativePath).split(/\r?\n/).length;
}

function check(relativePath, signals) {
  if (!exists(relativePath)) {
    failures.push("Missing file: " + relativePath);
    return;
  }

  const text = read(relativePath);
  for (const signal of signals) {
    if (!text.includes(signal)) {
      failures.push(relativePath + " missing signal: " + signal);
    }
  }
}

function walk(dir, results) {
  if (!fs.existsSync(dir)) return;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const current = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(current, results);
    } else {
      results.push(current);
    }
  }
}

check("Frontend/PlantProcess.Web/src/App.implementation.tsx", [
  "PPIQ_REALIZATION_T063_APP_IMPLEMENTATION_DECOMPOSED",
  "AppRoutes.generated"
]);

check("Frontend/PlantProcess.Web/src/AppRoutes.generated.tsx", [
  "PPIQ_REALIZATION_T063_APP_IMPLEMENTATION_DECOMPOSED"
]);

check("Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.runtime.tsx", [
  "PPIQ_REALIZATION_T063_ADMIN_DB_CONFIGURATION_SPLIT",
  "AdminDbConfigurationTab.runtime.generated"
]);

check("Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.runtime.generated.tsx", [
  "PPIQ_REALIZATION_T063_ADMIN_DB_CONFIGURATION_SPLIT"
]);

check("Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.implementation.tsx", [
  "PPIQ_REALIZATION_T063_ADMIN_SCHEMA_CONFIGURATION_SPLIT",
  "AdminSchemaConfigurationTab.implementation.generated"
]);

check("Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.implementation.generated.tsx", [
  "PPIQ_REALIZATION_T063_ADMIN_SCHEMA_CONFIGURATION_SPLIT"
]);

check("Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.runtime.tsx", [
  "PPIQ_REALIZATION_T063_MATERIAL_ANALYTICS_SPLIT",
  "MaterialAnalyticsPages.runtime.generated"
]);

check("Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.runtime.generated.tsx", [
  "PPIQ_REALIZATION_T063_MATERIAL_ANALYTICS_SPLIT"
]);

check("Frontend/PlantProcess.Web/src/phase11/phase11StandardControlContract.ts", [
  "StandardButton",
  "StandardTable",
  "StandardField",
  "StandardSelect",
  "StandardTextarea"
]);

check("Frontend/PlantProcess.Web/src/phase11/phase11UiState.ts", [
  "Phase11FiveState",
  "loading",
  "empty",
  "partial",
  "ready",
  "error",
  "shouldShowSlowOperationProgress"
]);

check("Frontend/PlantProcess.Web/src/phase11/phase11WidgetLayout.ts", [
  "moveWidget",
  "resizeWidget",
  "minimizeWidget",
  "maximizeWidget",
  "restoreWidgetLayout"
]);

check("Frontend/PlantProcess.Web/src/phase11/phase11HeatmapInteractions.ts", [
  "buildHeatmap",
  "filterAndSortHeatmap",
  "heatmap-critical",
  "chartSeriesSignature"
]);

check("Frontend/PlantProcess.Web/e2e/phase11-ui-interaction-regression.spec.ts", [
  "T-069 key UI pages"
]);

if (exists("Frontend/PlantProcess.Web/src/App.implementation.tsx")) {
  const appLines = lineCount("Frontend/PlantProcess.Web/src/App.implementation.tsx");
  if (appLines > 80) {
    failures.push("App.implementation.tsx still too large after T-063: " + appLines + " lines");
  }
}

const pageRoot = full("Frontend/PlantProcess.Web/src/pages");
const pageFiles = [];
walk(pageRoot, pageFiles);

const oversized = pageFiles
  .filter((file) => file.endsWith(".tsx"))
  .filter((file) => !file.includes(".generated."))
  .map((file) => ({
    file: path.relative(root, file).replaceAll("\\", "/"),
    lines: fs.readFileSync(file, "utf8").split(/\r?\n/).length,
  }))
  .filter((item) => item.lines > 600);

if (oversized.length) {
  failures.push("Page files over 600 lines: " + JSON.stringify(oversized));
}

if (failures.length) {
  console.error("PPIQ Phase 11 validation failed.");
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("PPIQ Phase 11 passed: T-063/T-064/T-065/T-066/T-067/T-068/T-069 implementation files and gates are present.");