
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function file(relativePath) {
  return path.join(root, relativePath);
}

function read(relativePath) {
  return fs.readFileSync(file(relativePath), "utf8");
}

function exists(relativePath) {
  return fs.existsSync(file(relativePath));
}

const checks = [
  {
    file: "Backend/PlantProcess.Application/Analytics/Value/Demo/Phase7WorkedCaseFixtures.cs",
    signals: [
      "PPIQ_REALIZATION_T038_EUR_28K_56K_WORKED_CASE_FIXTURE",
      "PPIQ-P07-T038-EDGE-CRACK-EUR-28K-56K",
      "DefectRateDelta: 0.02m",
      "MonthlyVolumeTons: 10_000m",
      "new CostBand(140m, 210m, 280m)",
      "ExpectedLow: 28_000m",
      "ExpectedMid: 42_000m",
      "ExpectedHigh: 56_000m",
      "not a guaranteed saving"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Phase7_ValueImpactWorkedCaseTests.cs",
    signals: [
      "T038_Reproduces_Eur28kTo56k_WorkedCase_Exactly",
      "T038_WorkedCase_Is_Deterministic_WhenRerun",
      "T038_Changing_DriverInput_Changes_Range_Traceably",
      "T038_Every_WorkedCase_Input_Is_Traceable_To_Provenance",
      "Assert.Equal(28_000m, result.Low)",
      "Assert.Equal(56_000m, result.High)",
      "downgrade_delta_per_ton"
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
  console.error("PPIQ-T038 failed: EUR 28k-56k worked case fixture is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T038 passed: deterministic EUR 28k-56k worked case fixture and provenance tests are present.");
