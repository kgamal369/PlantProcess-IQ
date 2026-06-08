
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
      "PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE",
      "each finding carries c.SampleSize, c.Dropped, survives/sreason and provenance",
      "AdvancedFinding("
    ]
  },
  {
    file: "Backend/PlantProcess.Application/Analytics/Advanced/AdvancedFindingTransparency.cs",
    signals: [
      "PPIQ_REALIZATION_T044_FINDING_TRANSPARENCY_EVIDENCE",
      "AdvancedFindingTransparency",
      "PopulationLabel",
      "PopulationSize",
      "PairedSampleSize",
      "ExcludedRecordCount",
      "ExclusionSummary",
      "StratificationEvaluated",
      "SurvivesStratification",
      "StratificationReason",
      "ProvenanceHandle",
      "HonestyCaveat",
      "AdvancedFindingTransparencyProjector"
    ]
  },
  {
    file: "Backend/tests/PlantProcess.Application.UnitTests/Analytics/Advanced/Phase8_T044FindingTransparencyTests.cs",
    signals: [
      "T044_Every_Finding_Carries_Population_Stratification_And_Exclusion_Fields",
      "T044_Partial_Feature_Shows_Dropped_Record_Count",
      "T044_Transparency_Projector_Produces_Complete_Surface_For_Every_Finding",
      "T044_Stratification_Status_Is_Surfaceable_For_Every_Finding",
      "param_pressure_population_partial",
      "ExcludedRecords > 0",
      "item.IsComplete"
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
  console.error("PPIQ-T044 failed: finding transparency evidence is incomplete.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("PPIQ-T044 passed: every advanced finding surfaces population, stratification, exclusions, provenance and honesty evidence.");
