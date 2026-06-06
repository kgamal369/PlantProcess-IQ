const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "Backend/PlantProcess.Application/Advisory/P15HonestyCertificationService.cs",
  "Backend/PlantProcess.Api/PlantConnectors/P15HonestyCertificationEndpoints.cs",
  "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts",
  "Frontend/PlantProcess.Web/src/pages/Phase15/HonestyCertificationPage.tsx",
  "docs/developer/PHASE15_RECOMMENDATION_HONESTY_CERTIFICATION.md",
  "docs/pack-g/PACK_G8_T101_HONESTY_CERTIFICATION_REPORT.json",
  "docs/pack-g/PACK_G8_T101_HONESTY_CERTIFICATION_REPORT.md",
  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "Backend/PlantProcess.Application/Advisory/P15HonestyCertificationService.cs", signal: "PPIQ_PACK_G8_T101_RECOMMENDATION_HONESTY_APPROVAL_CERTIFICATION" },
  { file: "Backend/PlantProcess.Application/Advisory/P15HonestyCertificationService.cs", signal: "CertifyCausalLanguageBlocked" },
  { file: "Backend/PlantProcess.Application/Advisory/P15HonestyCertificationService.cs", signal: "CertifyWeakEvidenceBlocked" },
  { file: "Backend/PlantProcess.Application/Advisory/P15HonestyCertificationService.cs", signal: "CertifyOutOfEnvelopeAbstains" },
  { file: "Backend/PlantProcess.Application/Advisory/P15HonestyCertificationService.cs", signal: "CertifyApprovalCommandRequired" },
  { file: "Backend/PlantProcess.Application/Advisory/P15HonestyCertificationService.cs", signal: "CertifyWriteBackBlocked" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15HonestyCertificationEndpoints.cs", signal: "/api/p15/honesty-certification" },
  { file: "Backend/PlantProcess.Api/PlantConnectors/P15HonestyCertificationEndpoints.cs", signal: "/run" },
  { file: "Backend/PlantProcess.Api/Program.cs", signal: "MapP15HonestyCertificationEndpoints" },
  { file: "Frontend/PlantProcess.Web/src/api/phase15Advisory.ts", signal: "/api/p15/honesty-certification/run" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/HonestyCertificationPage.tsx", signal: "Pack G · T-101 · Recommendation honesty & approval certification" },
  { file: "Frontend/PlantProcess.Web/src/pages/Phase15/HonestyCertificationPage.tsx", signal: "Run honesty certification" },
  { file: "Frontend/PlantProcess.Web/src/App.implementation.tsx", signal: "/phase15/honesty-certification" },
  { file: "Frontend/PlantProcess.Web/src/components/AppLayout.tsx", signal: "Honesty Cert." },
  { file: "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_G8_T101_RECOMMENDATION_HONESTY_APPROVAL_CERTIFICATION" }
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
  console.error("Pack G-8 T-101 honesty certification validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack G-8 T-101 recommendation honesty and approval certification validation passed.");
