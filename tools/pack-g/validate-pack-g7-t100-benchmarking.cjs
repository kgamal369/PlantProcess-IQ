const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "Backend/PlantProcess.Application/Advisory/P15BenchmarkingService.cs",
  "Backend/PlantProcess.Api/PlantConnectors/P15BenchmarkingEndpoints.cs",
  "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts",
  "Frontend/PlantProcess.Web/src/pages/Phase15/BenchmarkingPage.tsx",
  "docs/developer/PHASE15_CROSS_PLANT_INDUSTRY_BENCHMARKING.md",
  "docs/pack-g/PACK_G7_T100_BENCHMARKING_REPORT.json",
  "docs/pack-g/PACK_G7_T100_BENCHMARKING_REPORT.md",
  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "Backend/PlantProcess.Application/Advisory/P15BenchmarkingService.cs", signal: "PPIQ_PACK_G7_T100_CROSS_PLANT_INDUSTRY_BENCHMARKING" },
  { file: "Backend/PlantProcess.Application/Advisory/P15BenchmarkingService.cs", signal: "No identifiable cross-tenant rows" },
  { file: "Backend/PlantProcess.Application/Advisory/P15BenchmarkingService.cs", signal: "Minimum cohort size is enforced" },
  { file: "Backend/PlantProcess.Application/Advisory/P15BenchmarkingService.cs", signal: "SuppressedMinimumCohort" },
  { file: "Backend/PlantProcess.Application/Advisory/P15BenchmarkingService.cs", signal: "P15BestPracticeReference" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15BenchmarkingEndpoints.cs", signal: "/api/p15/benchmarking" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15BenchmarkingEndpoints.cs", signal: "/suppressed-demo" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15BenchmarkingEndpoints.cs", signal: "noCrossTenantRowExposure" },
  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapP15BenchmarkingEndpoints" },
  { file: "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts", signal: "/api/p15/benchmarking/summary" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/BenchmarkingPage.tsx", signal: "Pack G · T-100 · Cross-plant & industry benchmarking" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/BenchmarkingPage.tsx", signal: "Demo suppressed benchmark" },
  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/phase15/benchmarking" },
  { file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", signal: "Benchmarking" },
  { file: "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_G7_T100_CROSS_PLANT_INDUSTRY_BENCHMARKING" }
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
  console.error("Pack G-7 T-100 benchmarking validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack G-7 T-100 cross-plant and industry benchmarking validation passed.");
