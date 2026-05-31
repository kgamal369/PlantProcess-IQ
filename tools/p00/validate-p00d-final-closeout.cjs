const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");

const required = [
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/pageBuilderReducer.ts",
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/__tests__/pageBuilderReducer.test.ts",
  "docs/testing/P00D_E2E_Consolidation_Map.json",
  "docs/testing/P00D_Future_E2E_Deferrals.md",
  "Frontend/PlantProcess.Web/e2e/journeys/p00-e2e-consolidation.contract.spec.ts"
];

const failures = [];

for (const file of required) {
  if (!fs.existsSync(path.join(root, file))) {
    failures.push("Missing: " + file);
  }
}

const reducer = fs.existsSync(path.join(frontendRoot, "src/pages/PageBuilder/pageBuilderReducer.ts"))
  ? fs.readFileSync(path.join(frontendRoot, "src/pages/PageBuilder/pageBuilderReducer.ts"), "utf8")
  : "";

for (const token of [
  "type: \"addWidget\"",
  "type: \"moveWidget\"",
  "type: \"resizeWidget\"",
  "type: \"removeWidget\"",
  "createPageBuilderPayload",
]) {
  if (!reducer.includes(token)) {
    failures.push("Reducer missing token: " + token);
  }
}

const pageBuilder = fs.existsSync(path.join(frontendRoot, "src/pages/PageBuilder/PageBuilderPage.tsx"))
  ? fs.readFileSync(path.join(frontendRoot, "src/pages/PageBuilder/PageBuilderPage.tsx"), "utf8")
  : "";

if (!pageBuilder.includes("useReducer")) {
  failures.push("PageBuilderPage does not use reducer yet.");
}

if (!pageBuilder.includes("createPageBuilderPayload")) {
  failures.push("PageBuilderPage does not use createPageBuilderPayload.");
}

const mapPath = path.join(root, "docs/testing/P00D_E2E_Consolidation_Map.json");
const map = fs.existsSync(mapPath)
  ? JSON.parse(fs.readFileSync(mapPath, "utf8"))
  : null;

if (!map) {
  failures.push("Missing readable P00D consolidation map.");
} else {
  if (!Array.isArray(map.canonicalJourneys) || map.canonicalJourneys.length < 8) {
    failures.push("E2E consolidation map must contain at least 8 canonical journeys.");
  }

  if (!Array.isArray(map.futureDeferredJourneys) || map.futureDeferredJourneys.length !== 2) {
    failures.push("Future deferrals must explicitly contain the P06 and P09 journeys.");
  }
}

if (failures.length > 0) {
  console.error("P00D final closeout validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("P00D final closeout validation passed.");
console.log("Reducer test, E2E consolidation map, future deferrals and validator are present.");
