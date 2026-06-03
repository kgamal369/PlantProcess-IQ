import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function assertFile(relativePath) {
  const fullPath = path.join(root, relativePath);

  if (!fs.existsSync(fullPath)) {
    throw new Error(`Missing required file: ${relativePath}`);
  }
}

function assertContains(label, text, needles) {
  for (const needle of needles) {
    if (!text.includes(needle)) {
      throw new Error(`${label} is missing required content: ${needle}`);
    }

    console.log(`✓ ${label}: ${needle}`);
  }
}

const requiredFiles = [
  "src/App.tsx",
  "src/components/proof/RequestDemoForm.tsx",
  "src/components/proof/PricingLicenseMatrix.tsx",
  "src/components/proof/ProductScreenshotShowcase.tsx",
  "src/components/proof/PositioningTruthBlock.tsx",
  "src/components/proof/ConnectorHonestyBlock.tsx",
  "src/components/BrandProofSection.tsx",
  "src/content/phase1WebsiteProof.ts",
  "src/styles/global.css",
  "src/styles/phase10.css",
  "scripts/phase10-website-e2e.mjs",
];

for (const file of requiredFiles) {
  assertFile(file);
}

const app = read("src/App.tsx");
const requestForm = read("src/components/proof/RequestDemoForm.tsx");
const phase10Css = read("src/styles/phase10.css");
const globalCss = read("src/styles/global.css");
const content = read("src/content/phase1WebsiteProof.ts");
const packageJson = read("package.json");

assertContains("P10-01 product ecosystem", app, [
  "SOU MES",
  "SOU QES",
  "SOU Yard & Warehouse Management",
  "SOU Energy Management",
  "/products/mes",
  "/products/qes",
  "/products/yard",
  "/products/energy",
  "EcosystemGraphic",
]);

assertContains("P10-02 pricing/security", app, [
  "Light → Pro → Pro Plus → Enterprise",
  "Feature and usage tiers",
  "Read-only source layer",
  "Data handling",
  "Deployment models",
  "AI honesty",
  "Enterprise controls",
  "PricingLicenseMatrix",
]);

assertContains("P10-03 lead capture", requestForm, [
  "ppiq.website.demoLeads.v1",
  "localStorage",
  "lead-capture-success",
  "commercial-admin-lead-queue",
  "Capture lead and prepare notification",
  "sourceSystems",
  "fitScore",
]);

assertContains("P10-04 brand tokens / responsive CSS", phase10Css, [
  "#050B18",
  "#0B1730",
  "#102A43",
  "#0A84FF",
  "#00D4FF",
  "#2CE6A2",
  "@media",
  "ecosystem-card-grid",
  "phase10-matrix",
  "lead-success",
]);

assertContains("Existing brand foundation", globalCss, [
  "Inter",
  "JetBrains Mono",
]);

assertContains("Existing commercial data", content, [
  "enterprise",
  "implemented-certification-pending",
]);

assertContains("P10-05 scripts", packageJson, [
  "validate:phase10",
  "test:phase10",
  "e2e",
]);

const searchable = [
  app,
  requestForm,
  phase10Css,
].join("\n").toLowerCase();

const forbiddenClaims = [
  "fully autonomous root cause",
  "automatic root cause proof",
  "replaces mes",
  "replaces scada",
  "replaces level 2",
  "controls plc",
  "writes back to plc",
];

for (const claim of forbiddenClaims) {
  if (searchable.includes(claim)) {
    throw new Error(`Forbidden honesty claim detected: ${claim}`);
  }
}

const appImgWithoutAlt = /<img\b(?![^>]*\balt=)/i.test(app);
if (appImgWithoutAlt) {
  throw new Error("App.tsx contains an <img> without alt text.");
}

console.log("");
console.log("Website Phase 10 content / brand / honesty validation passed.");