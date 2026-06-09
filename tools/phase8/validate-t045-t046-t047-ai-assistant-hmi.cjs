
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const failures = [];

function exists(file) {
  return fs.existsSync(path.join(root, file));
}

function read(file) {
  return fs.readFileSync(path.join(root, file), "utf8");
}

const requiredFiles = [
  "Backend/PlantProcess.Application/AssistantRuntime/Phase8AssistantRuntimeContracts.cs",
  "Backend/PlantProcess.Api/Endpoints/Assistant/Phase8AssistantRuntimeEndpoints.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Assistant/Phase8AssistantRuntimeTests.cs",
  "Frontend/PlantProcess.Web/src/api/phase8Assistant.ts",
  "Frontend/PlantProcess.Web/src/pages/Phase8/SuggestionRecommendationPage.tsx",
  "Frontend/PlantProcess.Web/src/pages/Phase8/AssistantRuntimePage.tsx",
  "Frontend/PlantProcess.Web/src/pages/Phase8/AssistantConfigurationPage.tsx",
  "Frontend/PlantProcess.Web/src/pages/Phase8/phase8-ai.css",
  "Frontend/PlantProcess.Web/src/pages/Phase8/phase8AssistantView.test.ts"
];

for (const file of requiredFiles) {
  if (!exists(file)) failures.push("Missing " + file);
}

const app = exists("Frontend/PlantProcess.Web/src/App.implementation.tsx")
  ? read("Frontend/PlantProcess.Web/src/App.implementation.tsx")
  : "";

for (const signal of [
  "/phase8/suggestions",
  "/phase8/assistant",
  "/phase8/assistant-config",
  "Phase8SuggestionRecommendationPage",
  "Phase8AssistantRuntimePage",
  "Phase8AssistantConfigurationPage"
]) {
  if (!app.includes(signal)) failures.push("App route missing " + signal);
}

const program = exists("Backend/PlantProcess.Api/Program.cs")
  ? read("Backend/PlantProcess.Api/Program.cs")
  : "";

for (const signal of [
  "MapAssistantEndpoints",
  "MapPhase8AssistantRuntimeEndpoints"
]) {
  if (!program.includes(signal)) failures.push("Program.cs missing " + signal);
}

const layout = exists("Frontend/PlantProcess.Web/src/components/AppLayout.tsx")
  ? read("Frontend/PlantProcess.Web/src/components/AppLayout.tsx")
  : "";

for (const signal of [
  "P08 Suggestions",
  "P08 Assistant",
  "Assistant Config"
]) {
  if (!layout.includes(signal)) failures.push("AppLayout missing nav " + signal);
}

if (failures.length) {
  console.error("PPIQ Phase 8 T-045/T-046/T-047 validation failed.");
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("PPIQ Phase 8 T-045/T-046/T-047 passed: suggestion page, grounded assistant runtime, and HMI assistant config are present.");
