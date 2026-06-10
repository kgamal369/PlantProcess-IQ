
const fs = require("fs");
const path = require("path");

const root = process.cwd();

function read(rel) {
  return fs.readFileSync(path.join(root, rel), "utf8");
}

function exists(rel) {
  return fs.existsSync(path.join(root, rel));
}

function fail(message) {
  console.error("[RED] P3-T14 validation failed: " + message);
  process.exit(1);
}

const required = [
  "Frontend/PlantProcess.Web/src/api/p3T14ValueExecutive.ts",
  "Frontend/PlantProcess.Web/src/pages/ValueExecutive/ValueExecutiveDashboardPage.tsx",
  "Frontend/PlantProcess.Web/src/pages/ValueExecutive/value-executive.css",
  "Frontend/PlantProcess.Web/src/pages/ValueExecutive/p3t14ValueExecutive.test.ts",
  "Frontend/PlantProcess.Web/tests/e2e/p3t14-value-executive.spec.ts",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const api = read("Frontend/PlantProcess.Web/src/api/p3T14ValueExecutive.ts");
const page = read("Frontend/PlantProcess.Web/src/pages/ValueExecutive/ValueExecutiveDashboardPage.tsx");
const test = read("Frontend/PlantProcess.Web/src/pages/ValueExecutive/p3t14ValueExecutive.test.ts");
const e2e = read("Frontend/PlantProcess.Web/tests/e2e/p3t14-value-executive.spec.ts");

if (!api.includes("P3_T14_VALUE_ROI_EXECUTIVE_SURFACE")) fail("missing P3-T14 marker");
if (!api.includes("/api/value/impact")) fail("page API wrapper does not call /api/value/impact");
if (!api.includes("/api/value/cost-assumptions")) fail("page API wrapper does not configure cost assumptions");
if (!page.includes("Open monthly value report PDF")) fail("missing monthly report PDF action");
if (!page.includes("provenance handle")) fail("missing provenance wording");
if (!page.includes("ABSTAIN")) fail("missing ABSTAIN presentation");
if (!page.includes("data-testid=\"p3-t14-low\"")) fail("missing low test id");
if (!page.includes("data-testid=\"p3-t14-mid\"")) fail("missing mid test id");
if (!page.includes("data-testid=\"p3-t14-high\"")) fail("missing high test id");
if (!test.includes("28000") || !test.includes("42000") || !test.includes("56000")) fail("unit test does not assert exact EUR worked case");
if (!e2e.includes("prov:value:edge-crack:001")) fail("e2e test does not assert provenance handle");

const forbidden = /guaranteed|will save/i;
for (const [name, content] of [
  ["api", api],
  ["page", page],
]) {
  if (forbidden.test(content)) fail(name + " contains forbidden value-claim phrasing");
}

const routeCandidates = [
  "Frontend/PlantProcess.Web/src/AppRoutes.generated.tsx",
  "Frontend/PlantProcess.Web/src/AppRoutes.tsx",
  "Frontend/PlantProcess.Web/src/App.implementation.tsx",
  "Frontend/PlantProcess.Web/src/App.tsx",
].filter(exists);

if (!routeCandidates.some((rel) => read(rel).includes('path="/value/executive"'))) {
  fail("missing /value/executive route");
}

console.log("[GREEN] P3-T14 static validation passed.");
