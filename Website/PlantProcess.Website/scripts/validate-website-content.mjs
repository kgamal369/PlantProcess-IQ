
import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) throw new Error(`Missing required file: ${relativePath}`);
  return fs.readFileSync(full, "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function contains(label, text, needle) {
  ok(label, text.includes(needle), `missing ${needle}`);
}

const app = read("src/App.tsx");
const requestForm = read("src/components/proof/RequestDemoForm.tsx");
const pricingMatrix = read("src/components/proof/PricingLicenseMatrix.tsx");
const phase10Css = read("src/styles/phase10.css");
const globalCss = read("src/styles/global.css");
const content = read("src/content/phase1WebsiteProof.ts");
const packageJson = read("package.json");

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

for (const product of [
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
  contains("P10 product ecosystem", app, product);
}

for (const tier of ["Standard", "Pro Plus", "Enterprise"]) {
  contains("Pricing tier", app + content + pricingMatrix, tier);
}

for (const commercial of ["deposit", "month"]) {
  contains("Deposit + subscription model", content, commercial);
}

for (const trust of [
  "Read-only source layer",
  "Data handling",
  "Deployment models",
  "AI honesty",
  "Enterprise controls",
  "PricingLicenseMatrix",
]) {
  contains("Pricing/security proof", app, trust);
}

for (const valueChain of [
  "Connect (read-only)",
  "Stage",
  "Unify",
  "Analyse and Correlate",
  "Suggest (AI/ML)",
  "Ask (Grounded AI)",
]) {
  contains("PPIQ value-chain workflow", app, valueChain);
}

for (const leadRequirement of [
  "/api/v5/outbound/leads",
  "lead-capture-success",
  "commercial-admin-lead-queue",
  "Capture lead and prepare notification",
  "sourceSystems",
  "fitScore",
  "consentGiven",
  "honeypot",
]) {
  contains("P11 backend lead capture", requestForm, leadRequirement);
}

ok(
  "P11 no browser lead persistence",
  !/localStorage\.(setItem|getItem|removeItem)\([^)]*(lead|demoLead|demoLeads|ppiq\.website\.demoLeads)/i.test(requestForm)
);

for (const token of ["#050B18", "#0B1730", "#102A43", "#0A84FF", "#00D4FF", "#2CE6A2"]) {
  ok(`Brand color token ${token}`, (phase10Css + globalCss).toLowerCase().includes(token.toLowerCase()));
}

for (const cssNeedle of ["@media", "ecosystem-card-grid", "phase10-matrix", "lead-success"]) {
  contains("Responsive CSS", phase10Css + globalCss, cssNeedle);
}

contains("Font foundation Inter", globalCss + phase10Css, "Inter");
contains("Font foundation JetBrains Mono", globalCss + phase10Css, "JetBrains Mono");

for (const honesty of [
  "implemented-certification-pending",
  "available-now",
  "demo-certified",
  "simulated-source-shape",
  "Not MES",
  "Not SCADA",
  "Not Level 2",
  "Not BI-only",
]) {
  contains("Website honesty/content", app + content, honesty);
}

for (const aiHonesty of [
  "suspected contributors",
  "citations",
  "uncited number",
]) {
  contains("AI honesty language", app + content, aiHonesty);
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
