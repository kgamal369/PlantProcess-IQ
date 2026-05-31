const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function p(relativePath) {
  return path.join(root, relativePath.replaceAll("\\", path.sep).replaceAll("/", path.sep));
}

function exists(relativePath) {
  return fs.existsSync(p(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(p(relativePath), "utf8");
}

const deleted = [
  "Backend/tests/PlantProcess.Api.IntegrationTests/ApiTestEnvironmentTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/ApplicationTestEnvironmentTests.cs",
  "Backend/tests/PlantProcess.Domain.Tests/DomainTestEnvironmentTests.cs",
  "Backend/tests/PlantProcess.PerformanceTests/PerformanceTestEnvironmentTests.cs",
  "Backend/tests/PlantProcess.Infrastructure.IntegrationTests/InfrastructureTestEnvironmentTests.cs",
  "Frontend/PlantProcess.Web/src/test/smoke/frontendSmoke.test.ts",
  "Frontend/PlantProcess.Web/e2e/phase2-navigation-refresh-survival.spec.ts",
  "Frontend/PlantProcess.Web/e2e/phase1-route-refresh.spec.ts"
];
const retired = [
  "tools/validation/validate-phase01-phase02-gates.mjs",
  "tools/validation/validate-phase01-phase02-v5-gates.mjs",
  "tools/validation/validate-phase03-gates.mjs",
  "tools/validation/validate-v6-phase01-phase02-completion.cjs",
  "tools/validation/validate-v7-phase01.cjs",
  "tools/validation/validate-v7-phase01-acceptance.cjs",
  "tools/validation/validate-v7-phase02-phase03-acceptance.cjs",
  "tools/phase78/validate-phase7-phase8-acceptance.cjs",
  "Frontend/PlantProcess.Web/tools/phase3/validate-phase3-phase4-acceptance.cjs",
  "Frontend/PlantProcess.Web/tools/phase56/validate-phase5-phase6-acceptance.cjs",
  "Backend/tools/validate-sprint6-tasks-4-8.ps1"
];
const transfer = [
  "tools/validation/validate-v7-phase04-phase05-acceptance.cjs",
  "tools/validation/validate-v7-phase04-phase05-completion.cjs",
  "tools/validation/validate-t208-exposure.cjs",
  "tools/validation/validate-sql-script-hygiene.cjs",
  "Frontend/PlantProcess.Web/scripts/validate-api-client-policy.mjs"
];
const gates = [
  "Frontend/PlantProcess.Web/scripts/validate-standard-imports.mjs",
  "Frontend/PlantProcess.Web/scripts/validate-forbidden-copy.mjs",
  "Frontend/PlantProcess.Web/scripts/validate-no-console-in-src.mjs",
  "Frontend/PlantProcess.Web/scripts/validate-ui-system-rollout.mjs",
  "Frontend/PlantProcess.Web/tools/ui/validate-ui-standards.mjs",
  "Frontend/PlantProcess.Web/tools/ui/validate-phase2-full-ui-standards.mjs",
  "tools/validation/prove-standard-import-gate.cjs",
  "Website/PlantProcess.Website/scripts/validate-website-content.mjs"
];

const failures = [];

for (const item of deleted) {
  if (exists(item)) {
    failures.push("DELETE item still exists: " + item);
  }
}

for (const item of retired) {
  if (exists(item) && !read(item).includes("P00A-TEST-REGISTER: RETIRE-PENDING-REPLACEMENT")) {
    failures.push("RETIRE item missing marker: " + item);
  }
}

for (const item of transfer) {
  if (exists(item) && !read(item).includes("P00A-TEST-REGISTER: TRANSFER-TO-REAL-TEST")) {
    failures.push("TRANSFER item missing marker: " + item);
  }
}

for (const item of gates) {
  if (exists(item) && !read(item).includes("P00A-TEST-REGISTER: KEEP-AS-STRUCTURAL-CI-GATE")) {
    failures.push("KEEP-AS-GATE item missing marker: " + item);
  }
}

if (!exists("docs/testing/PlantProcessIQ_Test_Register_v1.md")) {
  failures.push("Missing docs/testing/PlantProcessIQ_Test_Register_v1.md");
}

if (!exists("docs/testing/P00A_77_Implementation_Backlog.md")) {
  failures.push("Missing docs/testing/P00A_77_Implementation_Backlog.md");
}

if (!exists("tools/validation/Run-P00A-StructuralGates.ps1")) {
  failures.push("Missing tools/validation/Run-P00A-StructuralGates.ps1");
}

if (failures.length > 0) {
  console.error("P00A maintenance validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("P00A maintenance validation passed.");
console.log("DELETE actions archived/removed, RETIRE marked, TRANSFER marked, KEEP-AS-GATE marked, docs created.");
