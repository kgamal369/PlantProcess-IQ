import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function contains(label, text, needle) {
  ok(label, text.includes(needle), `missing ${needle}`);
}

const app = read("src/App.tsx");
const form = read("src/components/proof/RequestDemoForm.tsx");
const css = read("src/styles/phase10.css");
const content = read("src/content/phase1WebsiteProof.ts");

for (const [needle, label] of [
  ['path="/"', "Home route exists"],
  ['path="/product"', "Product route exists"],
  ['path="/products/:productCode"', "Product dynamic route exists"],
  ['path="/pricing"', "Pricing route exists"],
  ['path="/security"', "Security route exists"],
  ['path="/contact"', "Contact route exists"],
]) {
  contains(label, app, needle);
}

for (const product of [
  "SOU MES",
  "SOU QES",
  "SOU Yard & Warehouse Management",
  "SOU Energy Management",
]) {
  contains(`Product exists ${product}`, app, product);
}

for (const tier of ["Light", "Pro", "Pro Plus", "Enterprise"]) {
  ok(`Pricing tier exists ${tier}`, (app + content).includes(tier));
}

for (const trust of [
  "Read-only source layer",
  "Data handling",
  "Deployment models",
  "AI honesty",
  "Enterprise controls",
]) {
  contains(`Trust pillar exists ${trust}`, app, trust);
}

for (const leadRequirement of [
  "/api/v5/outbound/leads",
  "validate(form)",
  "consentGiven",
  "honeypot",
  "lead-capture-success",
  "commercial-admin-lead-queue",
  "Open notification email",
]) {
  contains(`Backend lead capture requirement ${leadRequirement}`, form, leadRequirement);
}

ok(
  "No browser localStorage lead persistence",
  !/localStorage\.(setItem|getItem|removeItem)\([^)]*(lead|demoLead|demoLeads|ppiq\.website\.demoLeads)/i.test(form)
);

for (const responsive of [
  "@media",
  "ecosystem-card-grid",
  "trust-pillar-grid",
  "phase10-matrix__row",
]) {
  contains(`Responsive/style rule ${responsive}`, css, responsive);
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
  ok(`Forbidden claim absent ${pattern}`, !pattern.test(app) && !pattern.test(form) && !pattern.test(content));
}

console.log("");
console.log("Phase 10/P11 website e2e/source pack passed.");