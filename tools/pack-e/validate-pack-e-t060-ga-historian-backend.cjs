const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "Backend/PlantProcess.Infrastructure/Connectors/Historian/OpcUaHistorianConnector.cs",
  "Backend/PlantProcess.Api/PlantConnectors/V5GaHistorianConnectorEndpoints.cs",
  "docs/pack-e/PACK_E2_T060_GA_HISTORIAN_BACKEND_REPORT.json",
  "docs/pack-e/PACK_E2_T060_GA_HISTORIAN_BACKEND_REPORT.md",
  "docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "Backend/PlantProcess.Application/Integration/Connectors/ConnectorProviderCatalog.cs", signal: "PPIQ_PACK_E2_GA_HISTORIAN_PROVIDER" },
  { file: "Backend/PlantProcess.Application/Integration/Connectors/ConnectorProviderCatalog.cs", signal: "IsAvailableNow: true" },
  { file: "Backend/PlantProcess.Infrastructure/Connectors/Common/DataSourceConnectorFactory.cs", signal: "\"historian\"      => \"opcuahistorian\"" },
  { file: "Backend/PlantProcess.Infrastructure/DependencyInjection.cs", signal: "OpcUaHistorianConnector" },
  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapV5GaHistorianConnectorEndpoints" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5GaHistorianConnectorEndpoints.cs", signal: "/test-connection" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5GaHistorianConnectorEndpoints.cs", signal: "/browse-tags" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5GaHistorianConnectorEndpoints.cs", signal: "/read-window" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5GaHistorianConnectorEndpoints.cs", signal: "/mapping-hints" }
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

  if (!read(item.file).includes(item.signal)) {
    failures.push({ file: item.file, signal: item.signal, reason: "missing-signal" });
  }
}

if (isFile("docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md")) {
  const evidence = read("docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md");
  if (!evidence.includes("PPIQ_PACK_E2_GA_HISTORIAN_BACKEND")) failures.push({ reason: "evidence-marker-missing" });
}

if (failures.length) {
  console.error("Pack E-2 T-060 GA historian backend validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack E-2 T-060 GA historian backend validation passed.");
