
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

const checks = [
  {
    file: "Backend/PlantProcess.Application/Analytics/Value/ValueContracts.cs",
    signals: [
      "PPIQ_REALIZATION_T037_VALUE_ENGINE_BOUNDED_CONTRACTS",
      "public decimal Expected => Mid",
      "public bool IsMonotonic",
      "HonestyCaveat"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Analytics/Value/ValueImpactEngine.cs",
    signals: [
      "PPIQ_REALIZATION_T037_VALUE_ENGINE_BOUNDED_RANGE",
      "RequiredAssumptionGaps",
      "missing or invalid required assumption",
      "OrderBy(x => x)",
      "not raw equipment-stop time"
    ]
  },
  {
    file: "Backend/PlantProcess.Infrastructure/Analytics/NpgsqlValueImpactRepository.cs",
    signals: [
      "PPIQ_REALIZATION_T037_VALUE_IMPACT_REPOSITORY_EVIDENCE",
      "supportStatus",
      "Expected = result.Expected",
      "HonestyCaveat",
      "RangeWidth"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Phase7_ValueImpactEngineDepthTests.cs",
    signals: [
      "T037_ComputesBoundedExpectedRange_WithProvenanceAndNoGuarantee",
      "T037_Abstains_WhenScrapBandRequiredButMissing",
      "T037_Abstains_WhenBandIsNotLowExpectedHigh",
      "T037_NegativeImprovementFactor_StillProducesMonotonicBoundedRange"
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
  console.error("PPIQ-T037 failed: value engine depth implementation is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T037 passed: bounded value engine, abstain guard, evidence persistence and tests are present.");
