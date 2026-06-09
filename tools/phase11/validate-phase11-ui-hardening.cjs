const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function exists(file) {
  return fs.existsSync(path.join(root, file));
}

function read(file) {
  return fs.readFileSync(path.join(root, file), "utf8");
}

function lineCount(file) {
  return read(file).split(/\r?\n/).length;
}

function check(file, signals) {
  if (!exists(file)) {
    failures.push("Missing file: " + file);
    return;
  }

  const text = read(file);
  for (const signal of signals) {
    if (!text.includes(signal)) {
      failures.push(file + " missing signal: " + signal);
    }
  }
}

check("Frontend/PlantProcess.Web/src/App.implementation.tsx", [
  "PPIQ_REALIZATION_T063_APP_IMPLEMENTATION_DECOMPOSED",
  "AppRoutes.generated"
]);

check("Frontend/PlantProcess.Web/src/AppRoutes.generated.tsx", [
  "export default function App"
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

const appLines = lineCount("Frontend/PlantProcess.Web/src/App.implementation.tsx");
if (appLines > 80) {
  failures.push("App.implementation.tsx still too large after T-063: " + appLines + " lines");
}

const pageRoot = path.join(root, "Frontend", "PlantProcess.Web", "src", "pages");
if (fs.existsSync(pageRoot)) {
  const pageFiles = [];
  function walk(dir) {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        walk(full);
      } else if (entry.name.endsWith(".tsx") && !entry.name.includes(".generated.")) {
        pageFiles.push(full);
      }
    }
  }

  walk(pageRoot);

  const oversized = pageFiles
    .map((file) => ({
      file: path.relative(root, file).replaceAll("\\", "/"),
      lines: fs.readFileSync(file, "utf8").split(/\r?\n/).length,
    }))
    .filter((x) => x.lines > 600);

  if (oversized.length) {
    failures.push("Page files over 600 lines: " + JSON.stringify(oversized));
  }
}

if (failures.length) {
  console.error("PPIQ Phase 11 validation failed.");
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("PPIQ Phase 11 passed: T-063/T-064/T-065/T-066/T-067/T-068/T-069 implementation files and gates are present.");