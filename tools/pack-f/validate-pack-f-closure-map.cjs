const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.json",
  "docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.md",
  "docs/pack-f/PACK_F_CLOSURE_MAP.json",
  "docs/pack-f/PACK_F_CLOSURE_MAP.md",
  "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md"
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

if (isFile("docs/pack-f/PACK_F_CLOSURE_MAP.json")) {
  const map = JSON.parse(read("docs/pack-f/PACK_F_CLOSURE_MAP.json"));
  const tasks = new Set((map.closureMap || []).map((item) => item.task));
  for (const task of ["T-066", "T-067", "T-068", "T-071"]) {
    if (!tasks.has(task)) failures.push({ task, reason: "missing-from-closure-map" });
  }
}

if (isFile("docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.json")) {
  const audit = JSON.parse(read("docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.json"));
  for (const section of ["backend", "frontend", "infrastructure", "docs", "scorecard"]) {
    if (!audit[section]) failures.push({ section, reason: "missing-audit-section" });
  }
}

if (isFile("docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md")) {
  const evidence = read("docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md");
  if (!evidence.includes("PPIQ_PACK_F1_EDGE_AGENT_AUDIT_CLOSURE_MAP")) failures.push({ reason: "evidence-marker-missing" });
}

if (failures.length) {
  console.error("Pack F-1 closure map validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack F-1 edge-agent closure map validation passed.");
