const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs",
  "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryScenarioEndpoints.cs",
  "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts",
  "Frontend/PlantProcess.Web/src/pages/Phase15/ScenarioSimulationPage.tsx",
  "docs/developer/PHASE15_WHATIF_SCENARIO_ENGINE.md",
  "docs/pack-g/PACK_G3_T096_WHATIF_SCENARIO_ENGINE_REPORT.json",
  "docs/pack-g/PACK_G3_T096_WHATIF_SCENARIO_ENGINE_REPORT.md",
  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs", signal: "PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE" },
  { file: "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs", signal: "BuildStableScenarioSeed" },
  { file: "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs", signal: "OutOfEnvelope" },
  { file: "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs", signal: "ProjectionOnlyStatement" },
  { file: "Backend/PlantProcess.Application/Advisory/P15ScenarioSimulationService.cs", signal: "Weak evidence cannot support what-if projection." },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryScenarioEndpoints.cs", signal: "/api/p15/advisory/scenarios" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryScenarioEndpoints.cs", signal: "/simulate" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryScenarioEndpoints.cs", signal: "deterministic" },
  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapP15AdvisoryScenarioEndpoints" },
  { file: "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts", signal: "/api/p15/advisory/scenarios/simulate" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/ScenarioSimulationPage.tsx", signal: "Pack G · T-096 · What-if scenario simulation engine" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/ScenarioSimulationPage.tsx", signal: "Demo out-of-envelope abstain" },
  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/phase15/scenario-simulation" },
  { file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", signal: "What-if Simulation" },
  { file: "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE" }
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
  console.error("Pack G-3 T-096 scenario engine validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack G-3 T-096 deterministic what-if scenario engine validation passed.");
