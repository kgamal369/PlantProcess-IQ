const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "Backend/PlantProcess.Application/Advisory/P15ValueRealizationService.cs",
  "Backend/PlantProcess.Api/PlantConnectors/P15ValueRealizationEndpoints.cs",
  "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts",
  "Frontend/PlantProcess.Web/src/pages/Phase15/ValueRealizationPage.tsx",
  "docs/developer/PHASE15_VALUE_REALIZATION_TRACKING.md",
  "docs/pack-g/PACK_G5_T098_VALUE_REALIZATION_REPORT.json",
  "docs/pack-g/PACK_G5_T098_VALUE_REALIZATION_REPORT.md",
  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "Backend/PlantProcess.Application/Advisory/P15ValueRealizationService.cs", signal: "PPIQ_PACK_G5_T098_VALUE_REALIZATION_TRACKING" },
  { file: "Backend/PlantProcess.Application/Advisory/P15ValueRealizationService.cs", signal: "BaselineWindow" },
  { file: "Backend/PlantProcess.Application/Advisory/P15ValueRealizationService.cs", signal: "ActualWindow" },
  { file: "Backend/PlantProcess.Application/Advisory/P15ValueRealizationService.cs", signal: "P15ValueRealizationLedgerEntry" },
  { file: "Backend/PlantProcess.Application/Advisory/P15ValueRealizationService.cs", signal: "RecommendationId is required" },
  { file: "Backend/PlantProcess.Application/Advisory/P15ValueRealizationService.cs", signal: "AttributionCaveat" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15ValueRealizationEndpoints.cs", signal: "/api/p15/value-realization" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15ValueRealizationEndpoints.cs", signal: "/calculate" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15ValueRealizationEndpoints.cs", signal: "changingActualValueChangesRealizedValue" },
  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapP15ValueRealizationEndpoints" },
  { file: "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts", signal: "/api/p15/value-realization/calculate" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/ValueRealizationPage.tsx", signal: "Pack G · T-098 · Value-realization tracking baseline vs actual" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/ValueRealizationPage.tsx", signal: "Calculate realized value" },
  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/phase15/value-realization" },
  { file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", signal: "Value Realization" },
  { file: "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_G5_T098_VALUE_REALIZATION_TRACKING" }
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
  console.error("Pack G-5 T-098 value-realization validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack G-5 T-098 value-realization validation passed.");
