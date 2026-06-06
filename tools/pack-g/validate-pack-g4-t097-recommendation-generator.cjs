const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs",
  "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryRecommendationEndpoints.cs",
  "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts",
  "Frontend/PlantProcess.Web/src/pages/Phase15/RecommendationsPage.tsx",
  "docs/developer/PHASE15_RECOMMENDATION_GENERATOR.md",
  "docs/pack-g/PACK_G4_T097_RECOMMENDATION_GENERATOR_REPORT.json",
  "docs/pack-g/PACK_G4_T097_RECOMMENDATION_GENERATOR_REPORT.md",
  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT" },
  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "ExpectedImpact" },
  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "P15AdvisoryHonestyPolicy.ValidateRecommendation" },
  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "RequiresHumanApproval = true" },
  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "HasWriteBackPath = false" },
  { file: "Backend/PlantProcess.Application/Advisory/P15RecommendationService.cs", signal: "Weak evidence blocks recommendation" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryRecommendationEndpoints.cs", signal: "/api/p15/advisory/recommendations" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryRecommendationEndpoints.cs", signal: "/generate" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryRecommendationEndpoints.cs", signal: "/approve" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15AdvisoryRecommendationEndpoints.cs", signal: "automaticWriteBack = false" },
  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapP15AdvisoryRecommendationEndpoints" },
  { file: "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts", signal: "/api/p15/advisory/recommendations/generate" },
  { file: "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts", signal: "/api/p15/advisory/recommendations/approve" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/RecommendationsPage.tsx", signal: "Pack G · T-097 · Recommendation generator with expected €-impact" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/RecommendationsPage.tsx", signal: "Approve for review" },
  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/phase15/recommendations" },
  { file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", signal: "Recommendations" },
  { file: "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT" }
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
  console.error("Pack G-4 T-097 recommendation generator validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack G-4 T-097 recommendation generator validation passed.");
