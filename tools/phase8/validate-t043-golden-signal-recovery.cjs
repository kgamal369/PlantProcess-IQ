
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
    file: "Backend/PlantProcess.Application/Analytics/Advanced/AdvancedCorrelationComputeService.cs",
    signals: [
      "PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE",
      "Benjamini-Hochberg FDR",
      "bootstrap stability",
      "Deterministic (fixed seed)"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T043GoldenSignalRecoveryTests.cs",
    signals: [
      "PPIQ_REALIZATION_T043_GOLDEN_SIGNAL_RECOVERY_FIXTURE",
      "T043_Recovers_AtLeastTwo_KnownTrueSignals_UnderFdr",
      "T043_Rejects_All_InjectedSpuriousFeatures_UnderBenjaminiHochbergFdr",
      "T043_Reports_BootstrapStability_For_Every_EmittedFinding",
      "T043_Reruns_Are_Deterministic_Except_RunId",
      "T043_Vif_Excludes_Collinear_Duplicate_Without_Losing_TrueRepresentative",
      "param_true_temperature_driver",
      "param_true_pressure_driver",
      "param_injected_spurious_alternating",
      "param_injected_spurious_periodic",
      "param_injected_spurious_hash",
      "QValue > request.FdrQ",
      "StabilityConsistency >= 0.70"
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
  console.error("PPIQ-T043 failed: golden signal-recovery fixture is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T043 passed: golden dataset recovers true signals, rejects spurious features under FDR, and proves deterministic bootstrap stability.");
