import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function file(relativePath) {
  const full = path.join(root, relativePath);

  if (!fs.existsSync(full)) {
    throw new Error(`Missing required file: ${relativePath}`);
  }

  return fs.readFileSync(full, "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) {
    throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  }

  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function contains(label, text, needle) {
  ok(label, text.includes(needle), `missing ${needle}`);
}

function containsAny(label, text, needles) {
  ok(label, needles.some((needle) => text.includes(needle)), `missing one of: ${needles.join(" | ")}`);
}

const app = file("src/App.tsx");
const requestForm = file("src/components/proof/RequestDemoForm.tsx");
const pricingMatrix = file("src/components/proof/PricingLicenseMatrix.tsx");
const phase10Css = file("src/styles/phase10.css");
const globalCss = file("src/styles/global.css");
const content = file("src/content/phase1WebsiteProof.ts");
const packageJson = file("package.json");

for (const required of [
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
]) {
  ok(`Required file ${required}`, fs.existsSync(path.join(root, required)));
}

// P10-01 product ecosystem
for (const needle of [
  "SOU MES",
  "SOU QES",
  "SOU Yard & Warehouse Management",
  "SOU Energy Management",
  "/products/mes",
  "/products/qes",
  "/products/yard",
  "/products/energy",
  "EcosystemGraphic",
]) {
  contains("P10-01 product ecosystem", app, needle);
}

// P10-02 pricing/security: avoid fragile arrow/mojibake checks.
// Checking each tier separately is stronger and encoding-safe.
for (const tier of ["Light", "Pro", "Pro Plus", "Enterprise"]) {
  contains("P10-02 pricing tier", app + content + pricingMatrix, tier);
}

for (const needle of [
  "Feature and usage tiers",
  "Read-only source layer",
  "Data handling",
  "Deployment models",
  "AI honesty",
  "Enterprise controls",
  "PricingLicenseMatrix",
]) {
  contains("P10-02 pricing/security", app, needle);
}

// P11 replaces old localStorage lead capture with backend lead system.
for (const needle of [
  "/api/v5/outbound/leads",
  "lead-capture-success",
  "commercial-admin-lead-queue",
  "Capture lead and prepare notification",
  "sourceSystems",
  "fitScore",
  "consentGiven",
  "honeypot",
]) {
  contains("P11 backend lead capture", requestForm, needle);
}

ok(
  "P11 no browser lead persistence",
  !/localStorage\.(setItem|getItem|removeItem)\([^)]*(lead|demoLead|demoLeads|ppiq\.website\.demoLeads)/i.test(requestForm),
  "RequestDemoForm must not persist leads in localStorage"
);

// P10/P11 brand and responsive CSS.
for (const token of ["#050B18", "#0B1730", "#102A43", "#0A84FF", "#00D4FF", "#2CE6A2"]) {
  containsAny("Brand color token", phase10Css + globalCss, [token, token.toLowerCase()]);
}

for (const needle of ["@media", "ecosystem-card-grid", "phase10-matrix", "lead-success"]) {
  contains("Responsive/website CSS", phase10Css + globalCss, needle);
}

contains("Existing brand foundation", globalCss + phase10Css, "Inter");
contains("Existing brand foundation", globalCss + phase10Css, "JetBrains Mono");

// Connector and positioning honesty.
for (const needle of [
  "implemented-certification-pending",
  "available-now",
  "simulated-source-shape",
  "Not MES",
  "Not SCADA",
  "Not Level 2",
  "Not BI-only",
]) {
  contains("Website honesty/content", app + content, needle);
}

const forbidden = [
  /fully autonomous root cause/i,
  /automatic root cause proof/i,
  /replaces mes/i,
  /replaces scada/i,
  /replaces level 2/i,
  /controls plc/i,
  /writes back to plc/i,
];

for (const pattern of forbidden) {
  ok(`Forbidden claim absent ${pattern}`, !pattern.test(app) && !pattern.test(requestForm) && !pattern.test(content));
}

contains("Package build script", packageJson, "build");
contains("Package validate content script", packageJson, "validate:content");

console.log("");
console.log("Website content validation passed.");