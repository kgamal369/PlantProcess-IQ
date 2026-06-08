
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

const checks = [
  {
    file: "Backend/PlantProcess.Application/Analytics/Advanced/AdvancedReadinessGateSurface.cs",
    signals: [
      "PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES",
      "AdvancedReadinessGateStates",
      "Ready",
      "Partial",
      "Blocked",
      "AdvancedReadinessGateDto",
      "AdvancedReadinessGateSummaryDto",
      "AdvancedReadinessGateProjector",
      "NormalizeState"
    ]
  },
  {
    file: "Backend/PlantProcess.Api/Endpoints/Analytics/AdvancedResultsEndpoints.cs",
    signals: [
      "/readiness/gates",
      "PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES",
      "AdvancedReadinessGateProjector.Project"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T045ReadinessGateSurfaceTests.cs",
    signals: [
      "T045_Projects_AllReady_Dimensions_To_Ready_State",
      "T045_Projects_Warning_Dimension_To_Partial_State",
      "T045_Projects_Blocking_Dimension_To_Blocked_State",
      "T045_Normalizes_Legacy_And_Canonical_Readiness_States"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/api/advancedAnalysis.ts",
    signals: [
      "AdvancedReadinessGateSummaryDto",
      "AdvancedReadinessGateDto",
      "getAnalysisReadinessGates",
      "/readiness/gates"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/pages/Analytics/AdvancedAnalysisPage.tsx",
    signals: [
      "PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES",
      "getAnalysisReadinessGates",
      "phase8-readiness-state-badge",
      "phase8-readiness-gates",
      "phase8-readiness-gate-row",
      "Readiness gates"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/pages/Analytics/advancedReadinessGateView.test.ts",
    signals: [
      "normalizes Ready / Partial / Blocked states",
      "returns UI tone for HMI badges",
      "builds explicit readiness summary text"
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

if (failures.length) {
  console.error("PPIQ-T045 failed: Ready/Partial/Blocked readiness gates are incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T045 passed: Ready/Partial/Blocked readiness gates are exposed in API and HMI.");
