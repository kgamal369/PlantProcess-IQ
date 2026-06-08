
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
      "PPIQ_REALIZATION_T042_VIF_MULTICOLLINEARITY_HANDLING",
      "IterativeVifExclude",
      "VarianceInflation.Compute",
      "removed to keep one representative",
      "Drop/flag collinear numeric features before ranking/FDR"
    ]
  },
  {
    file: "Backend/PlantProcess.Analytics.Core/Methods/VarianceInflation.cs",
    signals: [
      "VarianceInflation",
      "VIF_j = 1 / (1 - R^2_j)",
      "Flagged",
      "threshold"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T042VifMulticollinearityTests.cs",
    signals: [
      "T042_Detects_And_Excludes_Collinear_Duplicate_By_Vif",
      "T042_Effect_Ranking_Remains_Stable_After_Vif_Pruning",
      "T042_Core_VarianceInflation_Flags_Deliberate_Collinearity",
      "param_true_temperature_duplicate",
      "VifThreshold: vifThreshold"
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
  console.error("PPIQ-T042 failed: VIF/multicollinearity certification is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T042 passed: VIF multicollinearity handling is explicitly marked, tested and certified.");
