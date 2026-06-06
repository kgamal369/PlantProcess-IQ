const fs = require("fs");
const path = require("path");

const root = process.cwd();
const canonicalJenkinsfile = "Jenkinsfile";
const canonicalTelemetryWorker = "Backend/PlantProcess.Workers/TelemetryIngestionWorker.cs";

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function read(file) { return fs.readFileSync(file, "utf8"); }
function rel(file) { return path.relative(root, file).split(path.sep).join("/"); }

function shouldSkipDir(name, fullPath) {
  const relative = rel(fullPath).toLowerCase();
  return [".git", ".vs", "bin", "obj", "node_modules", "dist", "coverage"].includes(name.toLowerCase()) ||
    relative.includes("/tools/_archive/") ||
    relative.includes("/.pack_a_backup/") ||
    relative.includes("/.pack_b_backup/") ||
    relative.includes("/.pack_d_backup/");
}

function walk(dir) {
  if (!exists(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (shouldSkipDir(entry.name, full)) return [];
      return walk(full);
    }
    return [full];
  });
}

const failures = [];

const jenkinsfiles = walk(root).filter((file) => path.basename(file).toLowerCase() === "jenkinsfile").map(rel).sort();
if (!jenkinsfiles.includes(canonicalJenkinsfile)) failures.push({ reason: "canonical-jenkinsfile-missing", canonicalJenkinsfile });
if (jenkinsfiles.length !== 1) failures.push({ reason: "jenkinsfile-count-not-one", count: jenkinsfiles.length, files: jenkinsfiles });

const telemetryClasses = walk(path.join(root, "Backend"))
  .filter((file) => file.endsWith(".cs"))
  .filter((file) => /class\s+TelemetryIngestionWorker\b/.test(read(file)))
  .map(rel)
  .sort();

if (!telemetryClasses.includes(canonicalTelemetryWorker)) failures.push({ reason: "canonical-telemetry-worker-missing", canonicalTelemetryWorker });
if (telemetryClasses.length !== 1) failures.push({ reason: "telemetry-worker-class-count-not-one", count: telemetryClasses.length, files: telemetryClasses });

const hostedRegistrations = walk(path.join(root, "Backend"))
  .filter((file) => file.endsWith(".cs"))
  .filter((file) => /AddHostedService\s*<\s*TelemetryIngestionWorker\s*>/.test(read(file)))
  .map(rel)
  .sort();

if (hostedRegistrations.length > 1) failures.push({ reason: "duplicate-hosted-service-registrations", count: hostedRegistrations.length, files: hostedRegistrations });

const requiredDocs = [
  "docs/pack-a/PACK_A4_T007_JENKINS_TELEMETRY_DEDUP_REPORT.json",
  "docs/pack-a/PACK_A4_T007_JENKINS_TELEMETRY_DEDUP_REPORT.md",
  "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"
];

for (const file of requiredDocs) {
  if (!isFile(path.join(root, file))) failures.push({ reason: "required-doc-missing", file });
}

if (isFile(path.join(root, "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"))) {
  const evidence = read(path.join(root, "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"));
  if (!evidence.includes("PPIQ_PACK_A4_T007_JENKINS_TELEMETRY_DEDUP")) {
    failures.push({ reason: "evidence-marker-missing" });
  }
}

if (failures.length) {
  console.error("Pack A-4 T-007 dedup validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack A-4 T-007 dedup validation passed.");
console.log("Active Jenkinsfiles: " + jenkinsfiles.length);
console.log("TelemetryIngestionWorker class definitions: " + telemetryClasses.length);
console.log("Telemetry hosted-service registrations: " + hostedRegistrations.length);
