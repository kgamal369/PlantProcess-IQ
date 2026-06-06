const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

const requiredFiles = [
  "docs/pack-f/PACK_F5_EDGE_FINAL_CONTRACT_SNAPSHOT.json",
  "docs/pack-f/PACK_F5_EDGE_FINAL_CONTRACT_SNAPSHOT.md",
  "docs/developer/OT_SAFE_EDGE_AGENT_REGRESSION_GUIDE.md",
  "docs/developer/OT_SAFE_EDGE_AGENT_FINAL_RUNBOOK.md",
  "docs/pack-f/PACK_F_FINAL_ACCEPTANCE.md",
  "tools/pack-f/Invoke-PackF-FinalRegression.ps1",
  "tools/pack-f/Invoke-PackF-FinalClosure.ps1",
  "docs/pack-f/PACK_F5_T071_EDGE_TESTS_DOCS_REGRESSION_REPORT.json",
  "docs/pack-f/PACK_F5_T071_EDGE_TESTS_DOCS_REGRESSION_REPORT.md",
  "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md"
];

const requiredSignals = [
  { file: "docs/pack-f/PACK_F5_EDGE_FINAL_CONTRACT_SNAPSHOT.json", signal: "PPIQ_PACK_F5_EDGE_FINAL_CONTRACT_SNAPSHOT" },
  { file: "docs/developer/OT_SAFE_EDGE_AGENT_REGRESSION_GUIDE.md", signal: "Non-negotiable safety rule" },
  { file: "docs/developer/OT_SAFE_EDGE_AGENT_FINAL_RUNBOOK.md", signal: "Tasks below 90%" },
  { file: "docs/pack-f/PACK_F_FINAL_ACCEPTANCE.md", signal: "T-071" },
  { file: "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md", signal: "PPIQ_PACK_F5_EDGE_TESTS_DOCS_REGRESSION" }
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

if (isFile("docs/pack-f/PACK_F5_EDGE_FINAL_CONTRACT_SNAPSHOT.json")) {
  const snapshot = JSON.parse(read("docs/pack-f/PACK_F5_EDGE_FINAL_CONTRACT_SNAPSHOT.json"));
  if (!snapshot.acceptance?.backendContractGreen) failures.push({ reason: "backend-contract-not-green" });
  if (!snapshot.acceptance?.otSafetyGreen) failures.push({ reason: "ot-safety-not-green" });
  if (!snapshot.acceptance?.packagingGreen) failures.push({ reason: "packaging-not-green" });
  if (!snapshot.acceptance?.uxGreen) failures.push({ reason: "ux-not-green" });
}

if (!runOk("node", ["tools/pack-f/validate-pack-f-closure-map.cjs"])) failures.push({ reason: "pack-f1-validator-failed" });
if (!runOk("node", ["tools/pack-f/validate-pack-f-t066-edge-backend.cjs"])) failures.push({ reason: "pack-f2-validator-failed" });
if (!runOk("node", ["tools/pack-f/validate-pack-f-t067-edge-packaging.cjs"])) failures.push({ reason: "pack-f3-validator-failed" });
if (!runOk("node", ["tools/pack-f/validate-pack-f-t068-edge-collector-ux.cjs"])) failures.push({ reason: "pack-f4-validator-failed" });
if (!runOk("node", ["tools/edge-agent/ppiq-edge-agent.cjs", "--config=tools/edge-agent/edge-agent.sample.json", "--dry-run", "--once"])) failures.push({ reason: "edge-agent-dry-run-failed" });

if (failures.length) {
  console.error("Pack F-5 T-071 edge tests/docs/regression validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack F-5 T-071 edge tests/docs/regression validation passed.");
