const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs",
  "Backend/PlantProcess.Api/PlantConnectors/P15RoiCfoDashboardEndpoints.cs",
  "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts",
  "Frontend/PlantProcess.Web/src/pages/Phase15/RoiCfoDashboardPage.tsx",
  "docs/developer/PHASE15_ROI_CFO_VALUE_DASHBOARD.md",
  "docs/pack-g/PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD_REPORT.json",
  "docs/pack-g/PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD_REPORT.md",
  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD" },
  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "PotentialValue" },
  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "RealizedValue" },
  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "PaybackPeriodMonths" },
  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "P15CfoEvidencePack" },
  { file: "Backend/PlantProcess.Application/Advisory/P15RoiCfoDashboardService.cs", signal: "ledger entries" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15RoiCfoDashboardEndpoints.cs", signal: "/api/p15/roi-cfo-dashboard" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15RoiCfoDashboardEndpoints.cs", signal: "/summary" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15RoiCfoDashboardEndpoints.cs", signal: "/evidence-pack" },
  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapP15RoiCfoDashboardEndpoints" },
  { file: "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts", signal: "/api/p15/roi-cfo-dashboard/summary" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/RoiCfoDashboardPage.tsx", signal: "Pack G · T-099 · ROI / CFO value dashboard" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/RoiCfoDashboardPage.tsx", signal: "Export CFO evidence pack" },
  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/phase15/roi-cfo-dashboard" },
  { file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", signal: "ROI/CFO Value" },
  { file: "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD" }
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
  console.error("Pack G-6 T-099 ROI/CFO value dashboard validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack G-6 T-099 ROI/CFO value dashboard validation passed.");
