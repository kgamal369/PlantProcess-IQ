const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "docs/pack-g/PACK_G1_PHASE15_AUDIT.json",
  "docs/pack-g/PACK_G1_PHASE15_AUDIT.md",
  "docs/pack-g/PACK_G_PHASE15_CLOSURE_MAP.json",
  "docs/pack-g/PACK_G_PHASE15_CLOSURE_MAP.md",
  "docs/task-closure/T096_T102_PHASE15_SCORECARD.json",
  "docs/task-closure/T096_T102_PHASE15_SCORECARD.md",
  "docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md"
];

function isFile(relativePath) {
  const absolute = path.join(root, relativePath);
  return fs.existsSync(absolute) && fs.statSync(absolute).isFile();
}

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

const failures = [];

for (const file of requiredFiles) {
  if (!isFile(file)) failures.push({ file, reason: "missing" });
}

if (isFile("docs/pack-g/PACK_G_PHASE15_CLOSURE_MAP.json")) {
  const map = JSON.parse(read("docs/pack-g/PACK_G_PHASE15_CLOSURE_MAP.json"));
  const tasks = new Set((map.closureMap || []).map((item) => item.task));
  for (const task of ["T-096", "T-097", "T-098", "T-099", "T-100", "T-101", "T-102"]) {
    if (!tasks.has(task)) failures.push({ task, reason: "missing-from-closure-map" });
  }
}

if (isFile("docs/task-closure/T096_T102_PHASE15_SCORECARD.json")) {
  const scorecard = JSON.parse(read("docs/task-closure/T096_T102_PHASE15_SCORECARD.json"));
  const rows = Array.isArray(scorecard.tasks) ? scorecard.tasks : [];
  const tasks = new Set(rows.map((item) => item.task));
  for (const task of ["T-096", "T-097", "T-098", "T-099", "T-100", "T-101", "T-102"]) {
    if (!tasks.has(task)) failures.push({ task, reason: "missing-from-phase15-scorecard" });
  }
}

if (isFile("docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md")) {
  const evidence = read("docs/pack-g/PACK_G_IMPLEMENTATION_EVIDENCE.md");
  if (!evidence.includes("PPIQ_PACK_G1_PHASE15_AUDIT_CLOSURE_MAP")) failures.push({ reason: "evidence-marker-missing" });
}

if (failures.length) {
  console.error("Pack G-1 Phase 15 closure-map validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack G-1 Phase 15 audit/closure-map validation passed.");
