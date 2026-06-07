
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function file(relativePath) {
  return path.join(root, relativePath);
}

function exists(relativePath) {
  return fs.existsSync(file(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(file(relativePath), "utf8");
}

function findAppFile() {
  if (exists("Frontend/PlantProcess.Web/src/App.implementation.tsx")) {
    return "Frontend/PlantProcess.Web/src/App.implementation.tsx";
  }
  return "Frontend/PlantProcess.Web/src/App.tsx";
}

const checks = [
  {
    file: "Frontend/PlantProcess.Web/src/api/value/value.api.ts",
    signals: [
      "phase7ValueApi",
      "/api/value/cost-assumptions",
      "/api/value/impact",
      "/api/value/realization/calculate",
      "/api/value/realization/record"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/Phase7ValueScenarioPage.tsx",
    signals: [
      "PPIQ_REALIZATION_T040_VALUE_SCENARIO_PAGE",
      "Value Scenario Workbench",
      "Run value scenario",
      "Record tracked value",
      "not a guaranteed saving",
      "not automatic causal attribution"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/phase7ValueScenarioMath.ts",
    signals: [
      "workedCaseLocalProjection",
      "normalizeImpact",
      "normalizeRealization",
      "formatMoney"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/pages/Phase7ValueScenario/phase7ValueScenarioMath.test.ts",
    signals: [
      "reproduces the EUR 28k-56k worked-case projection",
      "normalizes camelCase and PascalCase impact results",
      "normalizes realization result and caveat"
    ]
  },
  {
    file: findAppFile(),
    signals: [
      "Phase7ValueScenarioPage",
      "path=\"/value/scenario\""
    ]
  }
];

for (const check of checks) {
  if (!exists(check.file)) {
    failures.push({ file: check.file, reason: "missing file" });
    continue;
  }

  const text = read(check.file);

  for (const signal of check.signals) {
    if (!text.includes(signal)) {
      failures.push({ file: check.file, reason: "missing signal: " + signal });
    }
  }
}

if (exists("Frontend/PlantProcess.Web/src/components/AppLayout.tsx")) {
  const layout = read("Frontend/PlantProcess.Web/src/components/AppLayout.tsx");
  if (!layout.includes("/value/scenario")) {
    failures.push({ file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", reason: "navigation missing /value/scenario" });
  }
}

if (failures.length) {
  console.error("PPIQ-T040 failed: value scenario page wiring is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T040 passed: value scenario page, API client, route, navigation and tests are present.");
