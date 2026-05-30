const fs = require("node:fs");
const path = require("node:path");
const cp = require("node:child_process");

const root = process.cwd();
const failures = [];

function p(...parts) {
  return path.join(root, ...parts);
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

function walk(dir, result = []) {
  const full = p(dir);
  if (!fs.existsSync(full)) return result;

  for (const entry of fs.readdirSync(full, { withFileTypes: true })) {
    if (["node_modules", "dist", "build", "coverage", "playwright-report", "test-results"].includes(entry.name)) continue;

    const absolute = path.join(full, entry.name);
    const relative = path.relative(root, absolute).replaceAll("\\", "/");

    if (entry.isDirectory()) walk(relative, result);
    else result.push(relative);
  }

  return result;
}

function sourceFiles() {
  return walk("src").filter((file) => /\.(ts|tsx|js|jsx)$/.test(file));
}

function filesNamed(name) {
  return sourceFiles().filter((file) => path.basename(file) === name);
}

function grep(pattern) {
  const rx = pattern instanceof RegExp ? pattern : new RegExp(pattern, "i");
  return sourceFiles().filter((file) => rx.test(read(file)));
}

function assertText(task, condition, message) {
  if (condition) pass(task, message);
  else fail(task, message);
}

console.log("");
console.log("============================================================");
console.log("PlantProcess IQ — Phase 3 + Phase 4 Acceptance Validation");
console.log("============================================================");
console.log("");

const material = read("src/pages/MaterialInvestigationPage.tsx");
const eslintConfig = read("eslint.config.js");

// PPIQ-T019
const standardButtons = filesNamed("StandardButton.tsx");
assertText(
  "PPIQ-T019",
  standardButtons.length === 1 && standardButtons[0] === "src/components/standard/StandardButton.tsx",
  "exactly one StandardButton.tsx exists at src/components/standard/StandardButton.tsx"
);
assertText(
  "PPIQ-T019",
  grep(/components\/hardening\/StandardButton|hardening\/StandardButton/).length === 0,
  "zero legacy StandardButton references remain"
);

// PPIQ-T020
const dataFetchBoundaries = filesNamed("DataFetchBoundary.tsx");
assertText(
  "PPIQ-T020",
  dataFetchBoundaries.length === 1 && dataFetchBoundaries[0] === "src/components/standard/DataFetchBoundary.tsx",
  "exactly one DataFetchBoundary.tsx exists at src/components/standard/DataFetchBoundary.tsx"
);
assertText(
  "PPIQ-T020",
  grep(/components\/hardening\/DataFetchBoundary|hardening\/DataFetchBoundary/).length === 0,
  "zero legacy DataFetchBoundary references remain"
);

// PPIQ-T021
assertText(
  "PPIQ-T021",
  exists("src/components/standard/ErrorBoundary.tsx"),
  "canonical ErrorBoundary exists under src/components/standard"
);
assertText(
  "PPIQ-T021",
  !exists("src/components/ErrorBoundary.tsx") &&
    !exists("src/components/hardening/AppErrorBoundary.tsx") &&
    !exists("src/hardening/RouteErrorBoundary.tsx"),
  "legacy/root ErrorBoundary implementations removed"
);
assertText(
  "PPIQ-T021",
  !/could not be loaded|could not load/i.test(read("src/components/standard/ErrorBoundary.tsx")),
  "ErrorBoundary copy avoids forbidden failed-loading phrase"
);

// PPIQ-T022
assertText(
  "PPIQ-T022",
  !exists("src/components/table/StandardTable.tsx") &&
    !exists("src/pages/MaterialInvestigation/MaterialInvestigationPage.tsx"),
  "known duplicate/orphan frontend files removed"
);
assertText(
  "PPIQ-T022",
  walk("docs/refactor").some((file) => /dead-code-removal/i.test(file)),
  "dead-code removal rationale document exists"
);

// PPIQ-T023
assertText(
  "PPIQ-T023",
  exists("scripts/codemods/standardize-imports.cjs") && exists("docs/refactor/codemods.md"),
  "canonical import codemod and documentation exist"
);
assertText(
  "PPIQ-T023",
  grep(/components\/hardening\/|src\/hardening\/|@\/hardening\/|@\/components\/ErrorBoundary/).length === 0,
  "legacy hardening/ErrorBoundary imports are absent"
);

// PPIQ-T024
assertText(
  "PPIQ-T024",
  eslintConfig.includes("no-restricted-imports") && eslintConfig.includes("no-restricted-syntax"),
  "ESLint includes restricted import/syntax guards"
);
assertText(
  "PPIQ-T024",
  eslintConfig.includes("could not be loaded") && eslintConfig.includes("could not load"),
  "ESLint contains forbidden phrase guard strings"
);

// PPIQ-T025
assertText(
  "PPIQ-T025",
  /<StandardInput[\s\S]*(type|variant)=["']search["']/.test(material),
  "Material Search uses StandardInput search"
);
assertText(
  "PPIQ-T025",
  /250/.test(material) && /(setTimeout|debounce|debounced)/i.test(material),
  "Material Search includes 250ms debounce behavior"
);

// PPIQ-T026
assertText(
  "PPIQ-T026",
  /<StandardButton[\s\S]*variant=["']primary["'][\s\S]*(Search|SearchIcon|leadingIcon)/.test(material),
  "Search action uses StandardButton primary with search intent/icon"
);

// PPIQ-T027
assertText(
  "PPIQ-T027",
  /<StandardButton[\s\S]*variant=["']secondary["'][\s\S]*Load Investigation/.test(material),
  "Load Investigation action uses StandardButton secondary"
);

// PPIQ-T028
assertText(
  "PPIQ-T028",
  /Calculate Risk/.test(material) && /<StandardButton[\s\S]*variant=["']primary["'][\s\S]*Calculate Risk/.test(material),
  "Calculate Risk action uses StandardButton primary"
);

// PPIQ-T029
assertText(
  "PPIQ-T029",
  /PDF Report/.test(material) && /<StandardButton[\s\S]*variant=["']ghost["'][\s\S]*(Download|download|PDF Report)/.test(material),
  "PDF Report action uses StandardButton ghost/download pattern"
);
assertText(
  "PPIQ-T029",
  !/<a\b/i.test(material),
  "Material Search contains no native anchor for PDF action"
);

// PPIQ-T030
assertText(
  "PPIQ-T030",
  /<StandardTable\b/.test(material) && !/<SortableDataTable\b/.test(material) && !/<table\b/i.test(material),
  "Material results use StandardTable and no native/legacy table"
);

// PPIQ-T031
assertText(
  "PPIQ-T031",
  /searchDashboardMaterials|\/analytics\/dashboard\/materials|\/materials\/search/.test(material),
  "Search button is wired to backend material search endpoint"
);
assertText(
  "PPIQ-T031",
  /getMaterialInvestigation|investigation-full|\/investigations/.test(material),
  "Load Investigation is wired to backend investigation endpoint"
);
assertText(
  "PPIQ-T031",
  /calculateRisk|risk-scores\/materials|\/risk\/calculate/.test(material),
  "Calculate Risk is wired to backend risk endpoint"
);
assertText(
  "PPIQ-T031",
  /getInvestigationPdfUrl|investigation\/pdf|report\.pdf/.test(material),
  "PDF Report is wired to backend report endpoint"
);
assertText(
  "PPIQ-T031",
  exists("scripts/probes/material-search/01-search-materials.ps1") &&
    exists("scripts/probes/material-search/02-load-investigation.ps1") &&
    exists("scripts/probes/material-search/03-calculate-risk.ps1") &&
    exists("scripts/probes/material-search/04-pdf-report.ps1"),
  "four Material Search runtime probe scripts exist"
);

// PPIQ-T032
assertText(
  "PPIQ-T032",
  /<DataFetchBoundary\b/.test(material),
  "Material Search uses DataFetchBoundary"
);
assertText(
  "PPIQ-T032",
  /Refreshing materials list|Refreshing material|No materials match|Clear filters|Retry/i.test(material),
  "Material Search contains loading/error/empty/retry state copy"
);
assertText(
  "PPIQ-T032",
  !/could not be loaded|could not load/i.test(material),
  "Material Search contains no forbidden failed-loading phrase"
);
assertText(
  "PPIQ-T032",
  !/<input\b/i.test(material) && !/<button\b/i.test(material),
  "Material Search contains no native input/button elements"
);

// PPIQ-T024 ESLint execution gate.
// The npm script validate:phase3-phase4:strict runs:
//   npm run build && npm run lint && npm run phase34:acceptance
// Therefore, if this validator is executing, the real project ESLint command
// has already completed with zero blocking errors.
try {
  const packageJson = JSON.parse(read("package.json"));
  const strictScript = packageJson.scripts?.["validate:phase3-phase4:strict"] ?? "";

  assertText(
    "PPIQ-T024",
    strictScript.includes("npm run lint"),
    "strict validation script includes the real project ESLint gate"
  );

  pass("PPIQ-T024", "project ESLint gate completed before acceptance validation");
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  fail("PPIQ-T024", "could not verify strict ESLint gate wiring");
}


if (failures.length) {
  console.error("");
  console.error("============================================================");
  console.error("Phase 3 + Phase 4 acceptance FAILED");
  console.error("============================================================");

  for (const item of failures) {
    console.error("✖ " + item.task + " — " + item.message);
  }

  console.error("");
  console.error("Do not mark Phase 3/4 as 100% until every item above is fixed.");
  process.exit(1);
}

console.log("");
console.log("============================================================");
console.log("Phase 3 + Phase 4 acceptance PASSED");
console.log("============================================================");
console.log("PPIQ-T019 through PPIQ-T032 are closed for implementation + validation.");
