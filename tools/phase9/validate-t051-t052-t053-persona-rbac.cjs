const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function check(file, signals) {
  const full = path.join(root, file);
  if (!fs.existsSync(full)) {
    failures.push("Missing file: " + file);
    return;
  }

  const text = fs.readFileSync(full, "utf8");
  for (const signal of signals) {
    if (!text.includes(signal)) {
      failures.push(file + " missing signal: " + signal);
    }
  }
}

check("Backend/PlantProcess.Application/Security/Rbac/FormalRoleAccessMatrix.cs", [
  "PPIQ_REALIZATION_T051_FORMAL_ROLE_ACCESS_MATRIX",
  "FormalPlantRole",
  "PlantCapability",
  "Resolve("
]);

check("Backend/PlantProcess.Api/Endpoints/Security/Phase9RoleAccessEndpoints.cs", [
  "PPIQ_REALIZATION_T051_FORMAL_ROLE_ACCESS_MATRIX",
  "PPIQ_REALIZATION_T052_EXECUTIVE_PERSONA_SCOPE_DASHBOARD",
  "PPIQ_REALIZATION_T053_PERSONA_PAGE_VISIBILITY_ROUTE_GUARDS",
  "/executive/dashboard",
  "/access/enforce"
]);

check("Backend/tests/PlantProcess.Application.UnitTests/Security/Phase9RoleAccessMatrixTests.cs", [
  "Phase9RoleAccessMatrixTests",
  "T051_Executive_Can_View_Decision_Dashboard_But_Not_Admin_Config"
]);

check("Frontend/PlantProcess.Web/src/security/phase9RoleAccess.ts", [
  "routeDecision",
  "disabled",
  "executiveDashboardCards",
  "visibleNavigation"
]);

check("Frontend/PlantProcess.Web/src/pages/Phase9/ExecutivePersonaDashboardPage.tsx", [
  "Executive Decision Dashboard",
  "T-052"
]);

check("Frontend/PlantProcess.Web/src/pages/Phase9/PersonaAccessMatrixPage.tsx", [
  "Persona Access Matrix",
  "Disabled"
]);

check("Frontend/PlantProcess.Web/src/App.implementation.tsx", [
  "/phase9/executive",
  "/phase9/access",
  "Phase9ExecutivePersonaDashboardPage"
]);

if (failures.length) {
  console.error("PPIQ Phase 9 T-051/T-052/T-053 validation failed.");
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("PPIQ Phase 9 T-051/T-052/T-053 passed: RBAC matrix, executive dashboard, and route visibility guards are present.");