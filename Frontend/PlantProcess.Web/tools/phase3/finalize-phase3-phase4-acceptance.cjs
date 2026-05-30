const fs = require("node:fs");
const path = require("node:path");
const cp = require("node:child_process");

const root = process.cwd();

function p(...parts) {
  return path.join(root, ...parts);
}

function exists(file) {
  return fs.existsSync(p(file));
}

function read(file) {
  return fs.existsSync(p(file)) ? fs.readFileSync(p(file), "utf8") : "";
}

function write(file, content) {
  const full = p(file);
  fs.mkdirSync(path.dirname(full), { recursive: true });
  fs.writeFileSync(full, content.replace(/^\n/, ""), "utf8");
  console.log("Wrote " + file);
}

function remove(file) {
  const full = p(file);
  if (fs.existsSync(full)) {
    fs.rmSync(full, { force: true, recursive: true });
    console.log("Deleted " + file);
  }
}

function walk(dir, result = []) {
  const full = p(dir);
  if (!fs.existsSync(full)) return result;

  for (const entry of fs.readdirSync(full, { withFileTypes: true })) {
    if (["node_modules", "dist", "build", "coverage", "playwright-report", "test-results"].includes(entry.name)) continue;

    const absolute = path.join(full, entry.name);
    const relative = path.relative(root, absolute).replaceAll("\\", "/");

    if (entry.isDirectory()) {
      walk(relative, result);
    } else {
      result.push(relative);
    }
  }

  return result;
}

function patch(file, patcher) {
  if (!exists(file)) return;
  const before = read(file);
  const after = patcher(before);
  if (after !== before) write(file, after);
}

function patchSourceImports() {
  const files = walk("src").filter((file) => /\.(ts|tsx)$/.test(file));

  const replacements = [
    ["@/components/hardening/StandardButton", "@/components/standard/StandardButton"],
    ["@/hardening/StandardButton", "@/components/standard/StandardButton"],
    ["@/components/hardening/DataFetchBoundary", "@/components/standard/DataFetchBoundary"],
    ["@/hardening/DataFetchBoundary", "@/components/standard/DataFetchBoundary"],
    ["@/components/table/StandardTable", "@/components/standard/StandardTable"],
    ["@/components/hardening/AppErrorBoundary", "@/components/standard/ErrorBoundary"],
    ["@/components/hardening/ErrorBoundary", "@/components/standard/ErrorBoundary"],
    ["@/hardening/RouteErrorBoundary", "@/components/standard/ErrorBoundary"],
    ["@/components/ErrorBoundary", "@/components/standard/ErrorBoundary"],
    ["../components/ErrorBoundary", "../components/standard/ErrorBoundary"],
    ["./components/ErrorBoundary", "./components/standard/ErrorBoundary"],
  ];

  for (const file of files) {
    patch(file, (text) => {
      let output = text;
      for (const [from, to] of replacements) {
        output = output.split(from).join(to);
      }
      return output;
    });
  }
}

function ensureCanonicalErrorBoundary() {
  const canonical = "src/components/standard/ErrorBoundary.tsx";

  if (!exists(canonical)) {
    const candidates = [
      "src/components/ErrorBoundary.tsx",
      "src/components/hardening/ErrorBoundary.tsx",
      "src/components/hardening/AppErrorBoundary.tsx",
      "src/hardening/RouteErrorBoundary.tsx",
    ];

    const source = candidates.find(exists);

    if (!source) {
      throw new Error("PPIQ-T021 failed: no ErrorBoundary source found to move into src/components/standard/ErrorBoundary.tsx");
    }

    let content = read(source);

    content = content
      .replace(/import\s+\{\s*API_BASE_URL\s*\}\s+from\s+["']\.\.\/api\/apiConfig["'];/g, 'import { API_BASE_URL } from "../../api/apiConfig";')
      .replace(/import\s+\{\s*API_BASE_URL\s*\}\s+from\s+["']@\/api\/apiConfig["'];/g, 'import { API_BASE_URL } from "@/api/apiConfig";')
      .replace(/import\s+["']\.\/ErrorBoundary\.css["'];/g, 'import "../ErrorBoundary.css";')
      .replace(/\/\/ eslint-disable-next-line no-console\s*\n\s*/g, "");

    write(canonical, content);
  }

  patch("src/components/standard/index.ts", (text) => {
    if (text.includes('export * from "./ErrorBoundary";')) return text;
    return text.trimEnd() + '\nexport * from "./ErrorBoundary";\n';
  });

  patchSourceImports();

  remove("src/components/ErrorBoundary.tsx");
  remove("src/components/hardening/ErrorBoundary.tsx");
  remove("src/components/hardening/AppErrorBoundary.tsx");
  remove("src/hardening/RouteErrorBoundary.tsx");
}

function deleteKnownDuplicateAndDeadFiles() {
  const files = [
    "src/components/hardening/StandardButton.tsx",
    "src/hardening/StandardButton.tsx",
    "src/components/hardening/DataFetchBoundary.tsx",
    "src/hardening/DataFetchBoundary.tsx",
    "src/components/table/StandardTable.tsx",
    "src/pages/MaterialInvestigation/MaterialInvestigationPage.tsx",
  ];

  for (const file of files) remove(file);

  write("src/components/table/index.ts", `
export * from "@/components/standard/StandardTable";
`);
}

function ensureCodemodAndDocs() {
  write("scripts/codemods/standardize-imports.cjs", `
const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

const rewrites = new Map([
  ["@/components/hardening/StandardButton", "@/components/standard/StandardButton"],
  ["@/hardening/StandardButton", "@/components/standard/StandardButton"],
  ["@/components/hardening/DataFetchBoundary", "@/components/standard/DataFetchBoundary"],
  ["@/hardening/DataFetchBoundary", "@/components/standard/DataFetchBoundary"],
  ["@/components/hardening/ErrorBoundary", "@/components/standard/ErrorBoundary"],
  ["@/components/hardening/AppErrorBoundary", "@/components/standard/ErrorBoundary"],
  ["@/hardening/RouteErrorBoundary", "@/components/standard/ErrorBoundary"],
  ["@/components/ErrorBoundary", "@/components/standard/ErrorBoundary"],
  ["@/components/table/StandardTable", "@/components/standard/StandardTable"],
]);

function walk(dir, files = []) {
  if (!fs.existsSync(dir)) return files;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (["node_modules", "dist", "build", "coverage"].includes(entry.name)) continue;

    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, files);
    else if (/\\.(ts|tsx|js|jsx)$/.test(entry.name)) files.push(full);
  }

  return files;
}

let changed = 0;

for (const file of walk(path.join(root, "src"))) {
  const before = fs.readFileSync(file, "utf8");
  let after = before;

  for (const [from, to] of rewrites) {
    after = after.split(from).join(to);
  }

  if (after !== before) {
    fs.writeFileSync(file, after, "utf8");
    changed++;
  }
}

console.log("PPIQ standardize-imports codemod completed. Files changed: " + changed);
`);

  write("docs/refactor/codemods.md", `
# PlantProcess IQ Codemods

## scripts/codemods/standardize-imports.cjs

Purpose: keeps Phase 3 canonical UI imports stable.

It rewrites legacy imports from:

- src/components/hardening/*
- src/hardening/*
- src/components/table/StandardTable
- src/components/ErrorBoundary

to the canonical locations:

- src/components/standard/StandardButton
- src/components/standard/DataFetchBoundary
- src/components/standard/ErrorBoundary
- src/components/standard/StandardTable

Run from Frontend/PlantProcess.Web:

\`\`\`powershell
node scripts/codemods/standardize-imports.cjs
\`\`\`
`);

  write("docs/refactor/dead-code-removal-30May2026.md", `
# Phase 3 Dead Code Removal — 30 May 2026

## Deleted duplicate / orphan files

| File | Reason |
|---|---|
| src/components/hardening/StandardButton.tsx | Duplicate non-canonical StandardButton implementation. |
| src/hardening/StandardButton.tsx | Duplicate non-canonical StandardButton implementation. |
| src/components/hardening/DataFetchBoundary.tsx | Duplicate non-canonical DataFetchBoundary implementation. |
| src/hardening/DataFetchBoundary.tsx | Duplicate non-canonical DataFetchBoundary implementation. |
| src/components/table/StandardTable.tsx | Duplicate table implementation. Canonical table is src/components/standard/StandardTable.tsx. |
| src/pages/MaterialInvestigation/MaterialInvestigationPage.tsx | Orphan duplicate page path. Router uses src/pages/MaterialInvestigationPage.tsx. |
| src/components/ErrorBoundary.tsx | Moved to canonical src/components/standard/ErrorBoundary.tsx. |
| src/components/hardening/AppErrorBoundary.tsx | Legacy hardening wrapper replaced by canonical ErrorBoundary. |
| src/hardening/RouteErrorBoundary.tsx | Legacy route wrapper replaced by canonical ErrorBoundary. |

## Keep / wire note

Known workflow components that belong to later Phase 7 work must be tracked, not blindly deleted:

- Phase1WorkflowTruthPanel
- SaveInspectionJobModal
- OperationProgressPanel
`);
}

function ensureEslintContainsPhase34Rules() {
  const eslint = read("eslint.config.js");

  if (
    eslint.includes("no-restricted-imports") &&
    eslint.includes("no-restricted-syntax") &&
    eslint.includes("could not be loaded") &&
    eslint.includes("StandardButton") &&
    eslint.includes("DataFetchBoundary")
  ) {
    return;
  }

  console.warn("WARNING: eslint.config.js does not visibly contain all Phase 3 guard strings.");
  console.warn("The acceptance validator will fail PPIQ-T024 until eslint.config.js contains:");
  console.warn("- no-restricted-imports");
  console.warn("- no-restricted-syntax");
  console.warn("- forbidden phrase guard for could not be loaded / could not load");
  console.warn("- replacement guidance for StandardButton / DataFetchBoundary");
}

function ensureProbeScripts() {
  const probeRoot = "scripts/probes/material-search";

  write(`${probeRoot}/01-search-materials.ps1`, `
param(
  [string]$ApiBaseUrl = "http://localhost:5063",
  [string]$Token = $env:PPIQ_TOKEN,
  [string]$Query = ""
)

$headers = @{}
if ($Token) { $headers["Authorization"] = "Bearer $Token" }

Invoke-RestMethod -Method GET -Uri "$ApiBaseUrl/analytics/dashboard/materials?search=$Query&pageSize=10" -Headers $headers
`);

  write(`${probeRoot}/02-load-investigation.ps1`, `
param(
  [string]$ApiBaseUrl = "http://localhost:5063",
  [string]$Token = $env:PPIQ_TOKEN,
  [Parameter(Mandatory=$true)][string]$MaterialUnitId
)

$headers = @{}
if ($Token) { $headers["Authorization"] = "Bearer $Token" }

Invoke-RestMethod -Method GET -Uri "$ApiBaseUrl/materials/$MaterialUnitId/investigation-full?maxDepth=5&parameterPage=1&parameterPageSize=100" -Headers $headers
`);

  write(`${probeRoot}/03-calculate-risk.ps1`, `
param(
  [string]$ApiBaseUrl = "http://localhost:5063",
  [string]$Token = $env:PPIQ_TOKEN,
  [Parameter(Mandatory=$true)][string]$MaterialUnitId
)

$headers = @{ "Content-Type" = "application/json" }
if ($Token) { $headers["Authorization"] = "Bearer $Token" }

$body = @{ riskType = "QualityRisk" } | ConvertTo-Json

Invoke-RestMethod -Method POST -Uri "$ApiBaseUrl/risk-scores/materials/$MaterialUnitId/calculate" -Headers $headers -Body $body
`);

  write(`${probeRoot}/04-pdf-report.ps1`, `
param(
  [string]$ApiBaseUrl = "http://localhost:5063",
  [string]$Token = $env:PPIQ_TOKEN,
  [Parameter(Mandatory=$true)][string]$MaterialUnitId,
  [string]$OutFile = ".\\material-investigation-report.pdf"
)

$headers = @{}
if ($Token) { $headers["Authorization"] = "Bearer $Token" }

Invoke-WebRequest -Method GET -Uri "$ApiBaseUrl/reports/materials/$MaterialUnitId/investigation/pdf" -Headers $headers -OutFile $OutFile
Write-Host "Saved $OutFile"
`);

  write(`${probeRoot}/README.md`, `
# Phase 4 Material Search Runtime Probes

These probes validate the four Material Search actions against the current backend contracts.

| Action | Probe |
|---|---|
| Search | 01-search-materials.ps1 |
| Load Investigation | 02-load-investigation.ps1 |
| Calculate Risk | 03-calculate-risk.ps1 |
| PDF Report | 04-pdf-report.ps1 |

Run after backend is up and authenticated token is available if required:

\`\`\`powershell
$env:PPIQ_TOKEN = "<jwt-if-needed>"
.\\scripts\\probes\\material-search\\01-search-materials.ps1 -Query ""
.\\scripts\\probes\\material-search\\02-load-investigation.ps1 -MaterialUnitId "<guid>"
.\\scripts\\probes\\material-search\\03-calculate-risk.ps1 -MaterialUnitId "<guid>"
.\\scripts\\probes\\material-search\\04-pdf-report.ps1 -MaterialUnitId "<guid>"
\`\`\`
`);
}

function writeAcceptanceValidator() {
  write("tools/phase3/validate-phase3-phase4-acceptance.cjs", `
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
    const relative = path.relative(root, absolute).replaceAll("\\\\", "/");

    if (entry.isDirectory()) walk(relative, result);
    else result.push(relative);
  }

  return result;
}

function sourceFiles() {
  return walk("src").filter((file) => /\\.(ts|tsx|js|jsx)$/.test(file));
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
  grep(/components\\/hardening\\/StandardButton|hardening\\/StandardButton/).length === 0,
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
  grep(/components\\/hardening\\/DataFetchBoundary|hardening\\/DataFetchBoundary/).length === 0,
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
  grep(/components\\/hardening\\/|src\\/hardening\\/|@\\/hardening\\/|@\\/components\\/ErrorBoundary/).length === 0,
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
  /<StandardInput[\\s\\S]*(type|variant)=["']search["']/.test(material),
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
  /<StandardButton[\\s\\S]*variant=["']primary["'][\\s\\S]*(Search|SearchIcon|leadingIcon)/.test(material),
  "Search action uses StandardButton primary with search intent/icon"
);

// PPIQ-T027
assertText(
  "PPIQ-T027",
  /<StandardButton[\\s\\S]*variant=["']secondary["'][\\s\\S]*Load Investigation/.test(material),
  "Load Investigation action uses StandardButton secondary"
);

// PPIQ-T028
assertText(
  "PPIQ-T028",
  /Calculate Risk/.test(material) && /<StandardButton[\\s\\S]*variant=["']primary["'][\\s\\S]*Calculate Risk/.test(material),
  "Calculate Risk action uses StandardButton primary"
);

// PPIQ-T029
assertText(
  "PPIQ-T029",
  /PDF Report/.test(material) && /<StandardButton[\\s\\S]*variant=["']ghost["'][\\s\\S]*(Download|download|PDF Report)/.test(material),
  "PDF Report action uses StandardButton ghost/download pattern"
);
assertText(
  "PPIQ-T029",
  !/<a\\b/i.test(material),
  "Material Search contains no native anchor for PDF action"
);

// PPIQ-T030
assertText(
  "PPIQ-T030",
  /<StandardTable\\b/.test(material) && !/<SortableDataTable\\b/.test(material) && !/<table\\b/i.test(material),
  "Material results use StandardTable and no native/legacy table"
);

// PPIQ-T031
assertText(
  "PPIQ-T031",
  /searchDashboardMaterials|\\/analytics\\/dashboard\\/materials|\\/materials\\/search/.test(material),
  "Search button is wired to backend material search endpoint"
);
assertText(
  "PPIQ-T031",
  /getMaterialInvestigation|investigation-full|\\/investigations/.test(material),
  "Load Investigation is wired to backend investigation endpoint"
);
assertText(
  "PPIQ-T031",
  /calculateRisk|risk-scores\\/materials|\\/risk\\/calculate/.test(material),
  "Calculate Risk is wired to backend risk endpoint"
);
assertText(
  "PPIQ-T031",
  /getInvestigationPdfUrl|investigation\\/pdf|report\\.pdf/.test(material),
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
  /<DataFetchBoundary\\b/.test(material),
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
  !/<input\\b/i.test(material) && !/<button\\b/i.test(material),
  "Material Search contains no native input/button elements"
);

// Targeted strict ESLint for Phase 3/4 files only.
try {
  const npx = process.platform === "win32" ? "npx.cmd" : "npx";
  cp.execFileSync(npx, [
    "eslint",
    "src/pages/MaterialInvestigationPage.tsx",
    "src/components/standard/StandardButton.tsx",
    "src/components/standard/StandardFields.tsx",
    "src/components/standard/StandardTable.tsx",
    "src/components/standard/DataFetchBoundary.tsx",
    "src/components/standard/ErrorBoundary.tsx",
    "--max-warnings=0",
  ], { stdio: "inherit" });
  pass("PPIQ-T024", "targeted Phase 3/4 ESLint gate has zero warnings/errors");
} catch {
  fail("PPIQ-T024", "targeted Phase 3/4 ESLint gate failed");
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
`);
}

function updatePackageScripts() {
  patch("package.json", (text) => {
    const pkg = JSON.parse(text);
    pkg.scripts = pkg.scripts || {};
    pkg.scripts["phase34:acceptance"] = "node tools/phase3/validate-phase3-phase4-acceptance.cjs";
    pkg.scripts["validate:phase3-phase4:strict"] = "npm run build && npm run phase34:acceptance";
    return JSON.stringify(pkg, null, 2) + "\n";
  });
}

ensureCanonicalErrorBoundary();
deleteKnownDuplicateAndDeadFiles();
patchSourceImports();
ensureCodemodAndDocs();
ensureProbeScripts();
ensureEslintContainsPhase34Rules();
writeAcceptanceValidator();
updatePackageScripts();

console.log("");
console.log("Phase 3/4 finalizer applied.");
console.log("");
console.log("Run:");
console.log("  npm run validate:phase3-phase4:strict");
