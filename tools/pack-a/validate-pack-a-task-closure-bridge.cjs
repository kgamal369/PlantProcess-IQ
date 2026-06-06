const fs = require("fs");
const path = require("path");

const root = process.cwd();

const required = [
  "tools/task-closure/ppiq-pack-a-scorecard-bridge.cjs",
  "docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.PACK_A_BRIDGED.json",
  "docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.PACK_A_BRIDGED.md",
  "docs/pack-a/PACK_A3B_TASK_CLOSURE_EVIDENCE_BRIDGE_REPORT.json",
  "docs/pack-a/PACK_A3B_TASK_CLOSURE_EVIDENCE_BRIDGE_REPORT.md",
  "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"
];

function isFile(relativePath) {
  const absolute = path.join(root, relativePath);
  return fs.existsSync(absolute) && fs.statSync(absolute).isFile();
}

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

const failures = [];

for (const file of required) {
  if (!isFile(file)) failures.push({ file, reason: "missing" });
}

if (isFile("docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.PACK_A_BRIDGED.json")) {
  const json = JSON.parse(read("docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.PACK_A_BRIDGED.json"));
  const rows = Array.isArray(json.tasks) ? json.tasks : Array.isArray(json.rows) ? json.rows : Array.isArray(json) ? json : [];
  for (const task of ["T-010", "T-028"]) {
    const row = rows.find((item) => String(item.task || item.taskCode || item.id || item.code || "") === task);
    if (!row) {
      failures.push({ task, reason: "missing-row" });
      continue;
    }
    const score = Number(row.score ?? row.percent ?? row.completionPercent ?? row.percentage ?? 0);
    if (score < 90) failures.push({ task, reason: "score-still-below-90", score });
  }
}

if (isFile("docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md")) {
  const evidence = read("docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md");
  if (!evidence.includes("PPIQ_PACK_A3B_TASK_CLOSURE_EVIDENCE_BRIDGE")) {
    failures.push({ file: "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md", reason: "missing-marker" });
  }
}

if (failures.length) {
  console.error("Pack A-3B task-closure bridge validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack A-3B task-closure bridge validation passed.");
