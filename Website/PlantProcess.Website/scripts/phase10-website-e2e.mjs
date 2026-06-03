import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function pass(message) {
  console.log(`✓ ${message}`);
}

function fail(message) {
  throw new Error(message);
}

function mustContain(file, text, pattern, message) {
  if (pattern instanceof RegExp) {
    if (!pattern.test(text)) fail(`${file}: ${message}`);
  } else if (!text.includes(pattern)) {
    fail(`${file}: ${message}`);
  }

  pass(message);
}

const app = read("src/App.tsx");
const form = read("src/components/proof/RequestDemoForm.tsx");
const css = read("src/styles/phase10.css");

const routes = [
  ["/", "Home route exists"],
  ["/product", "PlantProcess IQ route exists"],
  ["/products/:code", "Product ecosystem dynamic route exists"],
  ["/pricing", "Pricing route exists"],
  ["/security", "Security route exists"],
  ["/contact", "Contact route exists"],
];

for (const [route, message] of routes) {
  mustContain("src/App.tsx", app, `path="${route}"`, message);
}

for (const product of [
  "SOU MES",
  "SOU QES",
  "SOU Yard & Warehouse Management",
  "SOU Energy Management",
]) {
  mustContain("src/App.tsx", app, product, `${product} page content exists`);
}

for (const pathValue of [
  "/products/mes",
  "/products/qes",
  "/products/yard",
  "/products/energy",
]) {
  mustContain("src/App.tsx", app, pathValue, `${pathValue} navigation exists`);
}

for (const trust of [
  "Read-only source layer",
  "Data handling",
  "Deployment models",
  "AI honesty",
  "Enterprise controls",
]) {
  mustContain("src/App.tsx", app, trust, `Trust pillar exists: ${trust}`);
}

for (const leadRequirement of [
  "validate(form)",
  "writeLeads(nextLeads)",
  "localStorage",
  "lead-capture-success",
  "commercial-admin-lead-queue",
  "Open notification email",
]) {
  mustContain("src/components/proof/RequestDemoForm.tsx", form, leadRequirement, `Lead capture requirement exists: ${leadRequirement}`);
}

for (const responsive of [
  "@media (max-width: 1180px)",
  "@media (max-width: 860px)",
  "ecosystem-card-grid",
  "trust-pillar-grid",
  "phase10-matrix__row",
]) {
  mustContain("src/styles/phase10.css", css, responsive, `Responsive/style rule exists: ${responsive}`);
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
  if (pattern.test(app) || pattern.test(form)) {
    fail(`Forbidden website honesty claim detected: ${pattern}`);
  }
}

pass("Forbidden claim scan passed");

console.log("");
console.log("Phase 10 website e2e/source pack passed.");