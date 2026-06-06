const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

const validators = [
  "tools/pack-g/validate-pack-g-phase15-closure-map.cjs",
  "tools/pack-g/validate-pack-g2-phase15-contract.cjs",
  "tools/pack-g/validate-pack-g3-t096-scenario-engine.cjs",
  "tools/pack-g/validate-pack-g4-t097-recommendation-generator.cjs",
  "tools/pack-g/validate-pack-g5-t098-value-realization.cjs",
  "tools/pack-g/validate-pack-g6-t099-roi-cfo-dashboard.cjs",
  "tools/pack-g/validate-pack-g7-t100-benchmarking.cjs",
  "tools/pack-g/validate-pack-g8-t101-honesty-certification.cjs",
];

const expectedDoneTasks = ["T-096", "T-097", "T-098", "T-099", "T-100", "T-101"];
const allPhase15Tasks = ["T-096", "T-097", "T-098", "T-099", "T-100", "T-101", "T-102"];

function isFile(relativePath) {
  const absolute = path.join(root, relativePath);
  return fs.existsSync(absolute) && fs.statSync(absolute).isFile();
}

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function runOk(cmd, args) {
  try { cp.execFileSync(cmd, args, { cwd: root, stdio: "pipe", shell: false }); return true; }
  catch { return false; }
}

function rowsOf(scorecard) { return Array.isArray(scorecard.tasks) ? scorecard.tasks : []; }
function code(row) { return String(row.task || row.taskCode || row.code || row.id || "").trim(); }
function score(row) { return Number(row.score ?? row.percent ?? row.completionPercent ?? row.percentage ?? 0); }

const failures = [];

for (const validator of validators) {
  if (!isFile(validator)) {
    failures.push({ validator, reason: "missing-validator" });
    continue;
  }
  if (!runOk("node", [validator])) failures.push({ validator, reason: "validator-failed" });
}

const requiredFiles = [
  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md",
  "docs/task-closure/T096_T102_PHASE15_SCORECARD.json",
  "docs/pack-g/PACK_G9_T102_PHASE15_REGRESSION_REPORT.json",
  "docs/pack-g/PACK_G9_T102_PHASE15_REGRESSION_REPORT.md"
];

for (const file of requiredFiles) {
  if (!isFile(file)) failures.push({ file, reason: "missing-file" });
}

if (isFile("docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md")) {
  const evidence = read("docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md");
  for (const marker of [
    "PPIQ_PACK_G1_PHASE15_AUDIT_CLOSURE_MAP",
    "PPIQ_PACK_G2_PHASE15_ADVISORY_VALUE_CONTRACT",
    "PPIQ_PACK_G3_T096_WHATIF_SCENARIO_ENGINE",
    "PPIQ_PACK_G4_T097_RECOMMENDATION_GENERATOR_EIMPACT",
    "PPIQ_PACK_G5_T098_VALUE_REALIZATION_TRACKING",
    "PPIQ_PACK_G6_T099_ROI_CFO_VALUE_DASHBOARD",
    "PPIQ_PACK_G7_T100_CROSS_PLANT_INDUSTRY_BENCHMARKING",
    "PPIQ_PACK_G8_T101_RECOMMENDATION_HONESTY_APPROVAL_CERTIFICATION",
    "PPIQ_PACK_G9_T102_PHASE15_REGRESSION_FINAL_CLOSURE"
  ]) {
    if (!evidence.includes(marker)) failures.push({ marker, reason: "missing-evidence-marker" });
  }
}

if (isFile("docs/task-closure/T096_T102_PHASE15_SCORECARD.json")) {
  const scorecard = JSON.parse(read("docs/task-closure/T096_T102_PHASE15_SCORECARD.json"));
  const rows = rowsOf(scorecard);
  const map = new Map(rows.map((row) => [code(row), row]));

  for (const task of allPhase15Tasks) {
    if (!map.has(task)) failures.push({ task, reason: "missing-scorecard-row" });
  }

  for (const task of expectedDoneTasks) {
    const row = map.get(task);
    if (!row || score(row) < 90) failures.push({ task, score: row ? score(row) : null, reason: "previous-task-not-green" });
  }
}

if (failures.length) {
  console.error("Pack G-9 T-102 Phase 15 regression validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack G-9 T-102 Phase 15 regression validation passed.");
