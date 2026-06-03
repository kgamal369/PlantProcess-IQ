import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function pass(message) {
  console.log(`OK ${message}`);
}

function fail(message) {
  throw new Error(message);
}

function mustContain(relativePath, content, needle, message) {
  if (!content.includes(needle)) {
    fail(`${message}: missing ${needle}`);
  }

  pass(message);
}

const app = read("src/App.tsx");
const form = read("src/components/proof/RequestDemoForm.tsx");
const css = read("src/styles/phase10.css");
const content = read("src/content/phase1WebsiteProof.ts");

const routes = [
  ['path="/"', "Home route exists"],
  ['path="/product"', "PlantProcess IQ route exists"],
  ['path="/products/:productCode"', "Product ecosystem dynamic route exists"],
  ['path="/pricing"', "Pricing route exists"],
  ['path="/security"', "Security route exists"],
  ['path="/contact"', "Contact route exists"],
];

for (const [needle, message] of routes) {
  mustContain("src/App.tsx", app, needle, message);
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

for (const tier of ["Light", "Pro", "Pro Plus", "Enterprise"]) {
  if (!(app + content).includes(tier)) {
    fail(`Pricing tier exists: ${tier}`);
  }
  pass(`Pricing tier exists: ${tier}`);
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

// P11-compatible lead capture: backend endpoint, consent, success, admin queue,
// and no localStorage lead persistence.
for (const leadRequirement of [
  "/api/v5/outbound/leads",
  "validate(form)",
  "consentGiven",
  "honeypot",
  "lead-capture-success",
  "commercial-admin-lead-queue",
  "Open notification email",
]) {
  mustContain(
    "src/components/proof/RequestDemoForm.tsx",
    form,
    leadRequirement,
    `Backend lead capture requirement exists: ${leadRequirement}`
  );
}

if (/localStorage\.(setItem|getItem|removeItem)\([^)]*(lead|demoLead|demoLeads|ppiq\.website\.demoLeads)/i.test(form)) {
  fail("RequestDemoForm must not persist leads in localStorage after P11.");
}
pass("No browser localStorage lead persistence");

for (const responsive of [
  "@media",
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
  if (pattern.test(app) || pattern.test(form) || pattern.test(content)) {
    fail(`Forbidden website honesty claim detected: ${pattern}`);
  }
}

pass("Forbidden claim scan passed");

console.log("");
console.log("Phase 10/P11 website e2e/source pack passed.");