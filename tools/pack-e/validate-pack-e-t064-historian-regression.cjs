const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

const requiredFiles = [
  "docs/pack-e/PACK_E4_HISTORIAN_CONTRACT_SNAPSHOT.json",
  "docs/pack-e/PACK_E4_HISTORIAN_CONTRACT_SNAPSHOT.md",
  "docs/developer/HISTORIAN_CONNECTOR_REGRESSION_GUIDE.md",
  "docs/developer/HISTORIAN_CONNECTOR_RUNBOOK.md",
  "tools/pack-e/Invoke-PackE-HistorianRegression.ps1",
  "docs/pack-e/PACK_E4_T064_HISTORIAN_TESTS_DOCS_REGRESSION_REPORT.json",
  "docs/pack-e/PACK_E4_T064_HISTORIAN_TESTS_DOCS_REGRESSION_REPORT.md",
  "docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "docs/pack-e/PACK_E4_HISTORIAN_CONTRACT_SNAPSHOT.json", signal: "PPIQ_PACK_E4_HISTORIAN_CONTRACT_SNAPSHOT" },
  { file: "docs/developer/HISTORIAN_CONNECTOR_REGRESSION_GUIDE.md", signal: "read-only historian gateway" },
  { file: "docs/developer/HISTORIAN_CONNECTOR_REGRESSION_GUIDE.md", signal: "Scorecard bridge" },
  { file: "docs/developer/HISTORIAN_CONNECTOR_RUNBOOK.md", signal: "Non-negotiable honesty rule" },
  { file: "docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_E4_T064_HISTORIAN_TESTS_DOCS_REGRESSION" }
];

function isFile(relativePath) {
  const absolute = path.join(root, relativePath);
  return fs.existsSync(absolute) && fs.statSync(absolute).isFile();
}

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function runOk(command, args) {
  try { cp.execFileSync(command, args, { cwd: root, stdio: "pipe", shell: false }); return true; }
  catch { return false; }
}

const failures = [];

for (const file of requiredFiles) {
  if (!isFile(file)) failures.push({ file, reason: "missing" });
}

for (const item of requiredSignals) {
  if (!isFile(item.file)) { failures.push({ file: item.file, signal: item.signal, reason: "missing-file" }); continue; }
  if (!read(item.file).includes(item.signal)) failures.push({ file: item.file, signal: item.signal, reason: "missing-signal" });
}

if (isFile("docs/pack-e/PACK_E4_HISTORIAN_CONTRACT_SNAPSHOT.json")) {
  const snapshot = JSON.parse(read("docs/pack-e/PACK_E4_HISTORIAN_CONTRACT_SNAPSHOT.json"));
  if (!snapshot.acceptance?.backendRoutesPresent) failures.push({ reason: "backend-route-contract-not-green" });
  if (!snapshot.acceptance?.frontendSignalsPresent) failures.push({ reason: "frontend-contract-not-green" });
  if (!snapshot.acceptance?.safetySignalsPresent) failures.push({ reason: "safety-contract-not-green" });
}

if (!runOk("node", ["tools/pack-e/validate-pack-e-t060-ga-historian-backend.cjs"])) failures.push({ reason: "pack-e2-backend-validator-failed" });
if (!runOk("node", ["tools/pack-e/validate-pack-e-t063-historian-ui.cjs"])) failures.push({ reason: "pack-e3-ui-validator-failed" });

if (failures.length) {
  console.error("Pack E-4 T-064 historian regression validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack E-4 T-064 historian tests/docs/regression validation passed.");
