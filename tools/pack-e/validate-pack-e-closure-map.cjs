const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "docs/pack-e/PACK_E1_HISTORIAN_AUDIT.json",
  "docs/pack-e/PACK_E1_HISTORIAN_AUDIT.md",
  "docs/pack-e/PACK_E_CLOSURE_MAP.json",
  "docs/pack-e/PACK_E_CLOSURE_MAP.md",
  "docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md"
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

if (isFile("docs/pack-e/PACK_E_CLOSURE_MAP.json")) {
  const map = JSON.parse(read("docs/pack-e/PACK_E_CLOSURE_MAP.json"));
  const tasks = new Set((map.closureMap || []).map((item) => item.task));
  for (const task of ["T-060", "T-063", "T-064"]) {
    if (!tasks.has(task)) failures.push({ task, reason: "missing-from-closure-map" });
  }
}

if (isFile("docs/pack-e/PACK_E1_HISTORIAN_AUDIT.json")) {
  const audit = JSON.parse(read("docs/pack-e/PACK_E1_HISTORIAN_AUDIT.json"));
  for (const section of ["backend", "frontend", "docs", "scorecard"]) {
    if (!audit[section]) failures.push({ section, reason: "missing-audit-section" });
  }
}

if (isFile("docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md")) {
  const evidence = read("docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md");
  if (!evidence.includes("PPIQ_PACK_E1_HISTORIAN_AUDIT_CLOSURE_MAP")) failures.push({ reason: "evidence-marker-missing" });
}

if (failures.length) {
  console.error("Pack E-1 closure map validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack E-1 historian closure map validation passed.");
