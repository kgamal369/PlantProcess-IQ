const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs",
  "Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs",
  "docs/developer/OT_SAFE_EDGE_AGENT_CONTRACT.md",
  "docs/developer/OT_SAFE_EDGE_AGENT_BACKEND_RUNBOOK.md",
  "docs/pack-f/PACK_F2_T066_OT_SAFE_EDGE_BACKEND_REPORT.json",
  "docs/pack-f/PACK_F2_T066_OT_SAFE_EDGE_BACKEND_REPORT.md",
  "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapV5OtSafeEdgeCollectorEndpoints" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "/api/v5/edge-collector" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "/register" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "/heartbeat" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "/push-batch" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "/queue-status" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "ReadOnlyCollection" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "OutboundOnly" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/V5OtSafeEdgeCollectorEndpoints.cs", signal: "OpensInboundListener" },
  { file: "Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs", signal: "OpensInboundListener = false" },
  { file: "Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs", signal: "ReadOnlyCollection = true" },
  { file: "Backend/PlantProcess.Workers/Edge/OtSafeEdgeAgentContract.cs", signal: "OutboundOnly = true" },
  { file: "docs/developer/OT_SAFE_EDGE_AGENT_CONTRACT.md", signal: "No inbound OT listener" },
  { file: "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_F2_OT_SAFE_EDGE_BACKEND" }
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
  console.error("Pack F-2 T-066 OT-safe edge backend validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack F-2 T-066 OT-safe edge backend validation passed.");
