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

const requiredFiles = [
  "tools/phase8/validate-t042-vif-multicollinearity.cjs",
  "tools/phase8/validate-t043-golden-signal-recovery.cjs",
  "tools/phase8/validate-t044-finding-transparency.cjs",
  "tools/phase8/validate-t045-readiness-gates.cjs",
  "scripts/phase8/validate-phase8-rollup.ps1",
  "docs/phase8/T042_VIF_MULTICOLLINEARITY_HANDLING.md",
  "docs/phase8/T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE.md",
  "docs/phase8/T044_FINDING_TRANSPARENCY_EVIDENCE.md",
  "docs/phase8/T045_READY_PARTIAL_BLOCKED_READINESS_GATES.md"
];

for (const item of requiredFiles) {
  if (!exists(item)) {
    failures.push({ file: item, reason: "missing required Phase 08 artifact" });
  }
}

const markerChecks = [
  {
    file: "Backend/PlantProcess.Application/Analytics/Advanced/AdvancedCorrelationComputeService.cs",
    signals: [
      "PPIQ_REALIZATION_T042_VIF_MULTICOLLINEARITY_HANDLING",
      "PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE",
      "PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Analytics/Advanced/AdvancedReadinessGateSurface.cs",
    signals: [
      "PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES",
      "AdvancedReadinessGateStates",
      "AdvancedReadinessGateProjector"
    ]
  },
  {
    file: "Frontend/PlantProcess.Web/src/pages/Analytics/AdvancedAnalysisPage.tsx",
    signals: [
      "PPIQ_REALIZATION_T045_READY_PARTIAL_BLOCKED_GATES",
      "phase8-readiness-state-badge",
      "phase8-readiness-gates"
    ]
  }
];

for (const check of markerChecks) {
  if (!exists(check.file)) {
    failures.push({ file: check.file, reason: "missing marker file" });
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
  console.error("PPIQ-T046 failed: Phase 08 certification rollup is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T046 passed: Phase 08 correlation certification artifacts, validators, API/HMI gates and evidence markers are present.");