const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "Frontend/PlantProcess.Web/src/api/edgeCollector.ts",
  "Frontend/PlantProcess.Web/src/pages/EdgeCollector/EdgeCollectorPage.tsx",
  "docs/pack-f/PACK_F4_T068_EDGE_COLLECTOR_UX_REPORT.json",
  "docs/pack-f/PACK_F4_T068_EDGE_COLLECTOR_UX_REPORT.md",
  "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "Frontend/PlantProcess.Web/src/api/edgeCollector.ts", signal: "/api/v5/edge-collector/register" },
  { file: "Frontend/PlantProcess.Web/src/api/edgeCollector.ts", signal: "/api/v5/edge-collector/heartbeat" },
  { file: "Frontend/PlantProcess.Web/src/api/edgeCollector.ts", signal: "/api/v5/edge-collector/push-batch" },
  { file: "Frontend/PlantProcess.Web/src/api/edgeCollector.ts", signal: "/api/v5/edge-collector/queue-status" },
  { file: "Frontend/PlantProcess.Web/src/pages/EdgeCollector/EdgeCollectorPage.tsx", signal: "Pack F · T-068 · Edge collector management UX" },
  { file: "Frontend/PlantProcess.Web/src/pages/EdgeCollector/EdgeCollectorPage.tsx", signal: "Register collector" },
  { file: "Frontend/PlantProcess.Web/src/pages/EdgeCollector/EdgeCollectorPage.tsx", signal: "Send heartbeat" },
  { file: "Frontend/PlantProcess.Web/src/pages/EdgeCollector/EdgeCollectorPage.tsx", signal: "Push sample batch" },
  { file: "Frontend/PlantProcess.Web/src/pages/EdgeCollector/EdgeCollectorPage.tsx", signal: "Never solve connectivity by opening inbound OT firewall access" },
  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/edge-collector" },
  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/edge-agent" },
  { file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", signal: "Edge Collector" },
  { file: "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_F4_EDGE_COLLECTOR_UX" }
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
  console.error("Pack F-4 T-068 edge collector UX validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack F-4 T-068 edge collector management UX validation passed.");
