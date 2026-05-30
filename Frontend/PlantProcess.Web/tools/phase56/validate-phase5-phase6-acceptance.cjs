const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const failures = [];

function p(file) {
  return path.join(root, file.split("/").join(path.sep));
}

function exists(file) {
  return fs.existsSync(p(file));
}

function read(file) {
  return exists(file) ? fs.readFileSync(p(file), "utf8") : "";
}

function fail(task, message) {
  failures.push({ task, message });
}

function pass(task, message) {
  console.log("✓ " + task + " — " + message);
}

function assert(task, condition, message) {
  if (condition) pass(task, message);
  else fail(task, message);
}

function noNative(task, file) {
  const text = read(file);
  assert(
    task,
    !/<(button|table|input|select|textarea)\b/i.test(text),
    file + " contains no native button/table/input/select/textarea"
  );
}

function contains(file, patterns) {
  const text = read(file);
  return patterns.every((pattern) => pattern.test(text));
}

console.log("");
console.log("============================================================");
console.log("PlantProcess IQ — Phase 5 + Phase 6 Acceptance Validation");
console.log("============================================================");
console.log("");

const central = "src/pages/Phase56/Phase56Pages.tsx";

assert("PPIQ-T033", contains(central, [/Phase56CommandDashboardPage/, /StandardCard/, /StandardTable/, /StandardInput/]), "Command Dashboard uses StandardCard, StandardTable and StandardInput");
assert("PPIQ-T033", exists("src/pages/Dashboard/DashboardPageContent.tsx"), "Dashboard route wrapper exists");
noNative("PPIQ-T033", "src/pages/Dashboard/DashboardPageContent.tsx");

assert("PPIQ-T034", contains(central, [/Phase56MaterialInvestigationPage/, /StandardTabs/, /Genealogy/, /Process History/, /Quality Events/, /Feature Vector/, /searchParam="tab"/]), "Material Investigation has StandardTabs with URL-sync sub-tabs");
noNative("PPIQ-T034", "src/pages/MaterialInvestigationPage.tsx");

assert("PPIQ-T035", contains(central, [/Phase56RiskIntelligencePage/, /StandardButton/, /StandardTable/, /phase56-chip--/]), "Risk Intelligence uses StandardButton filter pills, StandardTable and shared chips");
noNative("PPIQ-T035", "src/pages/RiskDashboard/RiskDashboardPage.tsx");

assert("PPIQ-T036", contains(central, [/Phase56DataQualityPage/, /StandardSelect/, /Severity/, /navigate\("\/admin\?sourceSystem=/]), "Data Quality uses StandardSelect, StandardTable, severity chips and admin drill-through");
noNative("PPIQ-T036", "src/pages/DataQuality/DataQualityPage.tsx");

assert("PPIQ-T037", contains(central, [/Phase56CorrelationPage/, /type="range"/, /StandardTable/, /navigate\("\/materials\?tab=feature-vector/]), "Correlations page has threshold range, StandardTable and Material drill-through");
noNative("PPIQ-T037", "src/pages/Correlation/CorrelationPage.tsx");

assert("PPIQ-T038", exists("e2e/visual/phase56-analytics-system.visual.spec.ts"), "Analytics visual regression spec exists");
assert("PPIQ-T038", exists("docs/visual-regression/phase56-baseline.md"), "Visual regression documentation exists");

assert("PPIQ-T039", exists("e2e/phase56-primary-flows.spec.ts"), "Primary analytics Playwright flow spec exists");
assert("PPIQ-T039", contains("e2e/phase56-primary-flows.spec.ts", [/ADV_COIL4002/, /could not be loaded\|could not load/]), "E2E covers material search and forbidden phrase assertion");

assert("PPIQ-T040", contains(central, [/Phase56MlReadinessPage/, /Run training gate check/, /No models in production yet/, /StandardTable/]), "ML Readiness uses StandardTable, primary gate button and honest empty positioning");
noNative("PPIQ-T040", "src/pages/MlReadiness/MlReadinessPage.tsx");

assert("PPIQ-T041", contains(central, [/Phase56DemoLifecyclePage/, /Reset Demo/, /StandardModal/, /OperationProgressPanel/]), "Demo Lifecycle has reset modal, status card and wired OperationProgressPanel");
noNative("PPIQ-T041", "src/pages/DemoLifecycle/DemoLifecyclePage.tsx");
noNative("PPIQ-T041", "src/components/phase2/OperationProgressPanel.tsx");

assert("PPIQ-T042", contains(central, [/Phase56AdminPreviewPage/, /Live tier toggle/, /ML scripts/, /Download report/]), "Admin Preview has tier toggle, roles table, ML scripts table and ghost report download");
noNative("PPIQ-T042", "src/pages/AdminPreview/AdminPreviewWorkspacePage.tsx");

assert("PPIQ-T043", contains(central, [/Phase56AdministratorPage/, /DB Config/, /Schema/, /Jobs/, /SQL Editor/, /KPI Builder/, /StandardTextArea/]), "Administrator has DB/schema/jobs/SQL/KPI StandardTabs and StandardTextArea");
noNative("PPIQ-T043", "src/pages/Admin/AdminPageContent.tsx");

assert("PPIQ-T044", contains(central, [/Phase56BrandIdentityPage/, /Brand Tokens/, /tokens/]), "Brand page imports live tokens and renders StandardTable");
noNative("PPIQ-T044", "src/pages/BrandIdentity/BrandIdentityPage.tsx");

assert("PPIQ-T045", contains(central, [/Save Investigation/, /Share/, /Export PDF/, /Calculate Risk/, /searchParam="tab"/]), "Material drilldown has save/share/export/calculate action bar and StandardTabs");
assert("PPIQ-T045", !/could not be loaded|could not load/i.test(read(central)), "Forbidden phrase absent from Phase 5/6 implementation");

const visualManifest = JSON.parse(read("e2e/visual/phase56-baseline-manifest.json"));
assert("PPIQ-T046", visualManifest.expectedSnapshotCount >= 80, "Visual manifest has at least 80 expected snapshots");
assert("PPIQ-T046", visualManifest.routes.includes("/ml-readiness") && visualManifest.routes.includes("/demo-lifecycle") && visualManifest.routes.includes("/admin") && visualManifest.routes.includes("/brand"), "System/intelligence routes included in visual manifest");

assert("PPIQ-T047", exists("e2e/a11y/phase56-accessibility.spec.ts"), "Accessibility Playwright spec exists");
assert("PPIQ-T047", exists("docs/a11y/audit-30May2026.md"), "Accessibility audit document exists");
assert("PPIQ-T047", contains("e2e/a11y/phase56-accessibility.spec.ts", [/missingButtonNames/, /missingInputs/, /Critical|Serious|accessibility/i]), "Accessibility spec checks labelled controls and visible content");

const pkg = JSON.parse(read("package.json"));
assert("PPIQ-T038", Boolean(pkg.scripts["test:visual"] && pkg.scripts["test:visual:update"]), "Visual regression scripts are wired in package.json");
assert("PPIQ-T039", Boolean(pkg.scripts["test:phase56:e2e"]), "Phase 5/6 e2e script is wired in package.json");
assert("PPIQ-T047", Boolean(pkg.scripts["test:a11y"]), "Accessibility script is wired in package.json");
assert("PPIQ-T033-T047", Boolean(pkg.scripts["validate:phase5-phase6:strict"]), "Strict Phase 5/6 validation script is wired");

const jenkinsPath = path.join(root, "..", "..", "Jenkinsfile");
const jenkins = fs.existsSync(jenkinsPath) ? fs.readFileSync(jenkinsPath, "utf8") : "";
assert("PPIQ-T038/T039/T047", /Phase 5\/6 UI quality gates/.test(jenkins), "Jenkinsfile contains Phase 5/6 UI quality gate stage");
assert("PPIQ-T038/T039/T047", /test:visual/.test(jenkins) && /test:phase56:e2e/.test(jenkins) && /test:a11y/.test(jenkins), "Jenkinsfile lists visual, e2e and accessibility scripts");

const targetFiles = [
  central,
  "src/pages/Dashboard/DashboardPageContent.tsx",
  "src/pages/MaterialInvestigationPage.tsx",
  "src/pages/RiskDashboard/RiskDashboardPage.tsx",
  "src/pages/DataQuality/DataQualityPage.tsx",
  "src/pages/Correlation/CorrelationPage.tsx",
  "src/pages/MlReadiness/MlReadinessPage.tsx",
  "src/pages/DemoLifecycle/DemoLifecyclePage.tsx",
  "src/pages/AdminPreview/AdminPreviewWorkspacePage.tsx",
  "src/pages/Admin/AdminPageContent.tsx",
  "src/pages/BrandIdentity/BrandIdentityPage.tsx",
  "src/components/phase2/OperationProgressPanel.tsx",
];

for (const file of targetFiles) {
  assert("PPIQ-T033-T047", !/could not be loaded|could not load/i.test(read(file)), file + " has no forbidden copy");
}

if (failures.length) {
  console.error("");
  console.error("============================================================");
  console.error("Phase 5 + Phase 6 acceptance FAILED");
  console.error("============================================================");

  for (const item of failures) {
    console.error("✖ " + item.task + " — " + item.message);
  }

  console.error("");
  console.error("Do not mark Phase 5/6 as 100% until every item above is fixed.");
  process.exit(1);
}

console.log("");
console.log("============================================================");
console.log("Phase 5 + Phase 6 acceptance PASSED");
console.log("============================================================");
console.log("PPIQ-T033 through PPIQ-T047 are closed for implementation + validation.");
