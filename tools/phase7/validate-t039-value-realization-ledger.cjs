
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
    file: "Backend/PlantProcess.Application/Analytics/Value/ValueRealizationContracts.cs",
    signals: [
      "PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_CONTRACTS",
      "ValueRealizationRequest",
      "ValueRealizationResult",
      "ValueRealizationLedgerEntry",
      "Baseline-vs-actual tracked value is not automatic causal attribution"
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Analytics/Value/ValueRealizationService.cs",
    signals: [
      "PPIQ_REALIZATION_T039_VALUE_REALIZATION_TRACKING_SERVICE",
      "source_recommendation_or_value_impact_link_required",
      "baseline_and_actual_metric_must_match",
      "CaptureRateMid",
      "RoiMid"
    ]
  },
  {
    file: "Backend/PlantProcess.Infrastructure/Analytics/NpgsqlValueRealizationRepository.cs",
    signals: [
      "PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_REPOSITORY",
      "canon.value_realization_ledger",
      "RecordAsync",
      "ListRecentAsync",
      "attribution_caveat"
    ]
  },
  {
    file: "Backend/PlantProcess.Api/Endpoints/Analytics/ValueRealizationEndpoints.cs",
    signals: [
      "PPIQ_REALIZATION_T039_VALUE_REALIZATION_ENDPOINTS",
      "/api/value/realization",
      "POST /api/value/realization/record",
      "GET /api/value/realization/ledger"
    ]
  },
  {
    file: "Backend/database/scripts/421_phase7_value_realization_ledger.sql",
    signals: [
      "PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_SQL",
      "CREATE TABLE IF NOT EXISTS canon.value_realization_ledger",
      "source_value_impact_id",
      "ck_value_realization_realized_band"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Phase7_ValueRealizationTrackingTests.cs",
    signals: [
      "T039_Computes_BaselineVsActual_TrackedValue_AndRoi",
      "T039_ChangingActualValue_ChangesRealizedValue",
      "T039_Abstains_WhenMetricWindowsDoNotMatch",
      "T039_Abstains_WhenSourceLinkIsMissing",
      "T039_WorseActualPerformance_CreatesNegativeTrackedValue"
    ]
  },
  {
    file: "Backend/PlantProcess.Infrastructure/Analytics/ValueEngineExtensions.cs",
    signals: [
      "IValueRealizationService",
      "NpgsqlValueRealizationRepository"
    ]
  },
  {
    file: "Backend/PlantProcess.Api/Program.cs",
    signals: [
      "app.MapValueRealizationEndpoints();"
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
  console.error("PPIQ-T039 failed: value-realization ledger implementation is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T039 passed: value-realization ledger, ROI tracking, API routes, SQL and tests are present.");
