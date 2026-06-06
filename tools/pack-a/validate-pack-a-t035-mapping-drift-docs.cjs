const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "docs/developer/MAPPING_AND_DRIFT_DEVELOPER_GUIDE.md",
  "docs/developer/MAPPING_DRIFT_TROUBLESHOOTING_RUNBOOK.md",
  "docs/developer/MAPPING_DRIFT_GATE_COMMANDS.md",
  "docs/pack-a/PACK_A5_T035_MAPPING_DRIFT_DOCS_REPORT.json",
  "docs/pack-a/PACK_A5_T035_MAPPING_DRIFT_DOCS_REPORT.md",
  "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"
];

const requiredTopics = [
  "PPIQ_PACK_A5_T035_MAPPING_DRIFT_DOCS",
  "mapping lifecycle",
  "schema discovery",
  "business-key",
  "canonical mapping",
  "safe sql",
  "drift detection",
  "schema drift",
  "validation gates",
  "troubleshooting",
  "staging",
  "ML readiness"
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

const combined = requiredFiles
  .filter((file) => isFile(file))
  .map((file) => read(file))
  .join("\n")
  .toLowerCase();

for (const topic of requiredTopics) {
  if (!combined.includes(topic.toLowerCase())) failures.push({ topic, reason: "missing-topic" });
}

if (isFile("docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md")) {
  const evidence = read("docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md");
  if (!evidence.includes("PPIQ_PACK_A5_T035_MAPPING_DRIFT_DOCS")) failures.push({ reason: "evidence-marker-missing" });
}

if (failures.length) {
  console.error("Pack A-5 T-035 mapping/drift docs validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack A-5 T-035 mapping/drift docs validation passed.");
