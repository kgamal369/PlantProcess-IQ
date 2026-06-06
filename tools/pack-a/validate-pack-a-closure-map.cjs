const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "docs/pack-a/PACK_A1_REMAINING_CLEANUP_AUDIT.json",
  "docs/pack-a/PACK_A1_REMAINING_CLEANUP_AUDIT.md",
  "docs/pack-a/PACK_A_CLOSURE_MAP.json",
  "docs/pack-a/PACK_A_CLOSURE_MAP.md",
  "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"
];

function exists(file) { return fs.existsSync(file); }
function isFile(relativePath) { return exists(path.join(root, relativePath)) && fs.statSync(path.join(root, relativePath)).isFile(); }
function read(relativePath) { return fs.readFileSync(path.join(root, relativePath), "utf8"); }

const failures = [];

for (const file of requiredFiles) {
  if (!isFile(file)) {
    failures.push({ file, reason: "missing" });
  }
}

if (isFile("docs/pack-a/PACK_A_CLOSURE_MAP.json")) {
  const map = JSON.parse(read("docs/pack-a/PACK_A_CLOSURE_MAP.json"));
  const requiredTasks = ["T-007", "T-010", "T-028", "T-035"];
  const present = new Set((map.closureMap || []).map((item) => item.task));

  for (const task of requiredTasks) {
    if (!present.has(task)) {
      failures.push({ task, reason: "missing-from-closure-map" });
    }
  }
}

if (isFile("docs/pack-a/PACK_A1_REMAINING_CLEANUP_AUDIT.json")) {
  const audit = JSON.parse(read("docs/pack-a/PACK_A1_REMAINING_CLEANUP_AUDIT.json"));
  const requiredSections = ["t007", "t010", "t028", "t035"];

  for (const section of requiredSections) {
    if (!audit[section]) {
      failures.push({ section, reason: "missing-audit-section" });
    }
  }
}

if (failures.length) {
  console.error("Pack A-1 closure map validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack A-1 closure map validation passed.");
