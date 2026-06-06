const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs",
  "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs",
  "docs/developer/PHASE15_ADVISORY_VALUE_CONTRACT.md",
  "docs/developer/PHASE15_ADVISORY_VALUE_RUNBOOK.md",
  "docs/pack-g/PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT_REPORT.json",
  "docs/pack-g/PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT_REPORT.md",
  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15ScenarioRequest" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15ScenarioResponse" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15RecommendationCandidate" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15ApprovalCommand" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15ValueRealizationLedgerEntry" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15RoiSummary" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryValueContracts.cs", signal: "P15BenchmarkResponse" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "P15AdvisoryHonestyPolicy" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "ValidateScenarioRequest" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "ValidateRecommendation" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "ValidateApprovalCommand" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "ValidateBenchmarkVisibility" },
  { file: "Backend/PlantProcess.Application/Advisory/P15AdvisoryHonestyPolicy.cs", signal: "BuildStableScenarioSeed" },
  { file: "docs/developer/PHASE15_ADVISORY_VALUE_CONTRACT.md", signal: "No automatic process write-back" },
  { file: "docs/developer/PHASE15_ADVISORY_VALUE_RUNBOOK.md", signal: "BuildStableScenarioSeed" },
  { file: "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT" }
];

function isFile(relativePath) {
  const absolute = path.join(root, relativePath);
  return fs.existsSync(absolute) && fs.statSync(absolute).isFile();
}

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

const failures = [];

for (const file of requiredFiles) {
  if (!isFile(file)) failures.push({ file, reason: "missing" });
}

for (const item of requiredSignals) {
  if (!isFile(item.file)) { failures.push({ file: item.file, signal: item.signal, reason: "missing-file" }); continue; }
  if (!read(item.file).includes(item.signal)) failures.push({ file: item.file, signal: item.signal, reason: "missing-signal" });
}

if (failures.length) {
  console.error("Pack G-2 Phase 15 advisory/value contract validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack G-2 Phase 15 advisory/value contract validation passed.");
