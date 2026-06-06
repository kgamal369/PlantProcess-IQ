const fs = require("fs");
const path = require("path");

const root = process.cwd();

const requiredFiles = [
  "tools/ci/write-certification-gate-report.cjs",
  "tools/ci/Invoke-PPIQ-Certification.ps1",
  "tools/ci/ppiq-certification-stage.sh",
  "docs/pack-a/PACK_A3_CI_CERTIFICATION_WIRING_REPORT.json",
  "docs/pack-a/PACK_A3_CI_CERTIFICATION_WIRING_REPORT.md",
  "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"
];

const requiredJenkinsSignals = [
  "PPIQ_PACK_A3_CI_CERTIFICATION",
  "taskClosure",
  "routeContract",
  "validate-t001-t071-task-closure.cjs",
  "validate-pack-d-route-contract-snapshot.cjs",
  "write-certification-gate-report.cjs",
  "archiveArtifacts"
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
  if (!isFile(file)) {
    failures.push({ file, reason: "missing" });
  }
}

if (!isFile("Jenkinsfile")) {
  failures.push({ file: "Jenkinsfile", reason: "missing" });
} else {
  const jenkinsfile = read("Jenkinsfile");

  for (const signal of requiredJenkinsSignals) {
    if (!jenkinsfile.includes(signal)) {
      failures.push({ file: "Jenkinsfile", signal, reason: "missing-ci-certification-signal" });
    }
  }
}

if (isFile("docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md")) {
  const evidence = read("docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md");
  if (!evidence.includes("PPIQ_PACK_A3_CI_CERTIFICATION_WIRING")) {
    failures.push({ file: "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md", reason: "missing-marker" });
  }
}

if (failures.length) {
  console.error("Pack A-3 CI certification wiring validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack A-3 CI certification wiring validation passed.");
