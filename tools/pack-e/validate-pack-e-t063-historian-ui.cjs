const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "Frontend/PlantProcess.Web/src/api/historianConnector.ts",
  "Frontend/PlantProcess.Web/src/pages/HistorianConnector/HistorianConnectorPage.tsx",
  "docs/pack-e/PACK_E3_T063_HISTORIAN_UI_REPORT.json",
  "docs/pack-e/PACK_E3_T063_HISTORIAN_UI_REPORT.md",
  "docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "Frontend/PlantProcess.Web/src/api/historianConnector.ts", signal: "/api/v5/historian-connector/test-connection" },
  { file: "Frontend/PlantProcess.Web/src/api/historianConnector.ts", signal: "/api/v5/historian-connector/browse-tags" },
  { file: "Frontend/PlantProcess.Web/src/api/historianConnector.ts", signal: "/api/v5/historian-connector/read-window" },
  { file: "Frontend/PlantProcess.Web/src/api/historianConnector.ts", signal: "/api/v5/historian-connector/mapping-hints" },
  { file: "Frontend/PlantProcess.Web/src/pages/HistorianConnector/HistorianConnectorPage.tsx", signal: "Pack E · T-063 · Historian connector UI" },
  { file: "Frontend/PlantProcess.Web/src/pages/HistorianConnector/HistorianConnectorPage.tsx", signal: "Test connection" },
  { file: "Frontend/PlantProcess.Web/src/pages/HistorianConnector/HistorianConnectorPage.tsx", signal: "Browse tags" },
  { file: "Frontend/PlantProcess.Web/src/pages/HistorianConnector/HistorianConnectorPage.tsx", signal: "Create mapping hints" },
  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/historian-connector" },
  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/connectors/historian" },
  { file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", signal: "Historian Connector" }
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
  if (!isFile(item.file)) {
    failures.push({ file: item.file, signal: item.signal, reason: "missing-file" });
    continue;
  }

  if (!read(item.file).includes(item.signal)) failures.push({ file: item.file, signal: item.signal, reason: "missing-signal" });
}

if (isFile("docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md")) {
  const evidence = read("docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md");
  if (!evidence.includes("PPIQ_PACK_E3_T063_HISTORIAN_UI")) failures.push({ reason: "evidence-marker-missing" });
}

if (failures.length) {
  console.error("Pack E-3 T-063 historian UI validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack E-3 T-063 historian UI validation passed.");
