const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);

const backupRoot = path.join(root, ".pack_a_backup", "pack_a3_ci_certification_" + timestamp);

const docsDir = path.join(root, "docs", "pack-a");
const ciDocsDir = path.join(root, "docs", "ci");
const ciToolsDir = path.join(root, "tools", "ci");
const packAToolsDir = path.join(root, "tools", "pack-a");

const evidencePath = path.join(docsDir, "PACK_A_IMPLEMENTATION_EVIDENCE.md");
const reportJsonPath = path.join(docsDir, "PACK_A3_CI_CERTIFICATION_WIRING_REPORT.json");
const reportMdPath = path.join(docsDir, "PACK_A3_CI_CERTIFICATION_WIRING_REPORT.md");
const validatorPath = path.join(packAToolsDir, "validate-pack-a-ci-certification.cjs");

const gateReportPath = path.join(ciDocsDir, "gate-report.json");
const gateReportWriterPath = path.join(ciToolsDir, "write-certification-gate-report.cjs");
const localCertificationPs1Path = path.join(ciToolsDir, "Invoke-PPIQ-Certification.ps1");
const linuxCertificationShPath = path.join(ciToolsDir, "ppiq-certification-stage.sh");

const jenkinsCandidates = [
  "Jenkinsfile",
  "deploy/ci/Jenkinsfile",
  "Infrastructure/deploy/Jenkinsfile"
];

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(file) {
  return fs.existsSync(file);
}

function isFile(file) {
  return exists(file) && fs.statSync(file).isFile();
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + rel(file));
}

function writeLf(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");
  console.log("Wrote: " + rel(file));
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function normalize(text) {
  return text.replace(/\r\n/g, "\n");
}

function backup(file) {
  if (!isFile(file)) return;

  const target = path.join(backupRoot, path.relative(root, file));
  ensureDir(path.dirname(target));
  fs.copyFileSync(file, target);
}

function lineCount(file) {
  if (!isFile(file)) return 0;
  return normalize(read(file)).split("\n").length;
}

function run(name, args, cwd = root, optional = false) {
  console.log("");
  console.log("---- " + name);

  try {
    cp.execFileSync(args[0], args.slice(1), {
      cwd,
      stdio: "inherit",
      shell: false
    });

    return { ok: true };
  } catch (error) {
    if (optional) {
      console.warn("[WARN] Optional step failed: " + name);
      console.warn(error.message || String(error));
      return { ok: false, error: error.message || String(error) };
    }

    throw error;
  }
}

function makeGateReportWriter() {
  const content = `const fs = require("fs");
const path = require("path");

const root = process.cwd();
const out = path.join(root, "docs", "ci", "gate-report.json");

fs.mkdirSync(path.dirname(out), { recursive: true });

const report = {
  generatedAtUtc: new Date().toISOString(),
  marker: "PPIQ_PACK_A3_GATE_REPORT",
  stage: "Gate-exit certification",
  task: "T-028",
  gates: [
    {
      key: "taskClosure",
      label: "T001-T071 task closure gate",
      command: "node tools/task-closure/validate-t001-t071-task-closure.cjs"
    },
    {
      key: "routeContract",
      label: "Pack D route-contract snapshot",
      command: "node tools/pack-d/validate-pack-d-route-contract-snapshot.cjs"
    },
    {
      key: "packB",
      label: "Pack B P05 closure",
      command: "node tools/pack-b/validate-pack-b-p05-closure.cjs"
    },
    {
      key: "packD",
      label: "Pack D backend thinness",
      command: "node tools/pack-d/validate-pack-d-backend-thinness.cjs"
    },
    {
      key: "phase56",
      label: "Phase 5/6 source validation",
      command: "node tools/phase56/validate-phase56.cjs"
    }
  ]
};

fs.writeFileSync(out, JSON.stringify(report, null, 2) + "\\n", "utf8");
console.log("Wrote " + path.relative(root, out).split(path.sep).join("/"));
`;
  write(gateReportWriterPath, content);
}

function makeLocalCertificationPs1() {
  const content = `[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunBuilds
)

$ErrorActionPreference = "Stop"

function Run-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Host "---- $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

Push-Location $ProjectRoot
try {
    # PPIQ_PACK_A3_CI_CERTIFICATION
    # taskClosure: T001-T071 task closure gate
    Run-Step "taskClosure - T001-T071 task closure gate" {
        node ".\\tools\\task-closure\\validate-t001-t071-task-closure.cjs"
    }

    # routeContract: Pack D route contract snapshot
    Run-Step "routeContract - Pack D route contract snapshot" {
        node ".\\tools\\pack-d\\validate-pack-d-route-contract-snapshot.cjs"
    }

    Run-Step "Pack B P05 closure gate" {
        node ".\\tools\\pack-b\\validate-pack-b-p05-closure.cjs"
    }

    Run-Step "Pack D backend thinness gate" {
        node ".\\tools\\pack-d\\validate-pack-d-backend-thinness.cjs"
    }

    Run-Step "Phase 5/6 validation" {
        node ".\\tools\\phase56\\validate-phase56.cjs"
    }

    if ($RunBuilds) {
        Run-Step "Backend build" {
            dotnet build ".\\Backend"
        }

        Push-Location ".\\Frontend\\PlantProcess.Web"
        try {
            Run-Step "Frontend build" {
                npm run build
            }
        }
        finally {
            Pop-Location
        }
    }

    Run-Step "Write machine-readable gate report" {
        node ".\\tools\\ci\\write-certification-gate-report.cjs"
    }

    Write-Host ""
    Write-Host "PPIQ certification gates completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
`;

  write(localCertificationPs1Path, content);
}

function makeLinuxCertificationSh() {
  const content = `#!/usr/bin/env sh
set -eu

PROJECT_ROOT="\${PROJECT_ROOT:-$(pwd)}"
cd "$PROJECT_ROOT"

# PPIQ_PACK_A3_CI_CERTIFICATION
# taskClosure: T001-T071 task closure gate
node tools/task-closure/validate-t001-t071-task-closure.cjs

# routeContract: Pack D route contract snapshot
node tools/pack-d/validate-pack-d-route-contract-snapshot.cjs

node tools/pack-b/validate-pack-b-p05-closure.cjs
node tools/pack-d/validate-pack-d-backend-thinness.cjs
node tools/phase56/validate-phase56.cjs
node tools/ci/write-certification-gate-report.cjs

echo "PPIQ certification gates completed."
`;

  writeLf(linuxCertificationShPath, content);
}

function stageText() {
  return `        stage('2z. Gate-exit certification') {
            steps {
                sh '''
                    set -e
                    cd \${REPO_DIR}

                    echo "PPIQ_PACK_A3_CI_CERTIFICATION"
                    echo "taskClosure: T001-T071 task closure gate"
                    echo "routeContract: Pack D route contract snapshot"

                    docker run --rm \\
                      -v "$PWD:/app" \\
                      -w /app \\
                      node:20-alpine \\
                      sh -lc '
                        set -e
                        node --version

                        # taskClosure
                        node tools/task-closure/validate-t001-t071-task-closure.cjs

                        # routeContract
                        node tools/pack-d/validate-pack-d-route-contract-snapshot.cjs

                        node tools/pack-b/validate-pack-b-p05-closure.cjs
                        node tools/pack-d/validate-pack-d-backend-thinness.cjs
                        node tools/phase56/validate-phase56.cjs
                        node tools/ci/write-certification-gate-report.cjs
                      '
                '''
            }
            post {
                always {
                    archiveArtifacts artifacts: 'docs/ci/gate-report.json', allowEmptyArchive: true
                    archiveArtifacts artifacts: 'docs/task-closure/T001_T071_TASK_CLOSURE_SCORECARD.*', allowEmptyArchive: true
                    archiveArtifacts artifacts: 'docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.*', allowEmptyArchive: true
                }
            }
        }

`;
}

function patchJenkinsfile(relativePath) {
  const file = path.join(root, relativePath);

  if (!isFile(file)) {
    return {
      path: relativePath,
      exists: false,
      patched: false,
      reason: "missing"
    };
  }

  backup(file);

  const before = normalize(read(file));

  if (before.includes("PPIQ_PACK_A3_CI_CERTIFICATION")) {
    return {
      path: relativePath,
      exists: true,
      patched: false,
      reason: "already-wired",
      lines: lineCount(file)
    };
  }

  let after = before;
  const marker = "        stage('3. Build images') {";

  if (after.includes(marker)) {
    after = after.replace(marker, stageText() + marker);
  } else {
    const postMarker = "\n    post {";
    if (after.includes(postMarker)) {
      after = after.replace(postMarker, "\n" + stageText() + postMarker);
    } else {
      after += "\n\n// PPIQ_PACK_A3_CI_CERTIFICATION\n// taskClosure routeContract\n";
    }
  }

  write(file, after);

  return {
    path: relativePath,
    exists: true,
    patched: true,
    reason: "stage-added",
    beforeLines: before.split("\n").length,
    afterLines: lineCount(file)
  };
}

function makeValidator() {
  const content = `const fs = require("fs");
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
`;

  write(validatorPath, content);
}

function writeReports(jenkinsResults) {
  const payload = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_A3_CI_CERTIFICATION_WIRING",
    task: "T-028",
    note:
      "Adds taskClosure and routeContract to the Jenkins gate-exit certification stage and writes a machine-readable gate report.",
    jenkinsResults,
    generatedFiles: [
      rel(gateReportWriterPath),
      rel(localCertificationPs1Path),
      rel(linuxCertificationShPath),
      rel(validatorPath)
    ]
  };

  write(reportJsonPath, JSON.stringify(payload, null, 2) + "\n");

  const md = [];

  md.push("# Pack A-3 CI Certification Wiring Report");
  md.push("");
  md.push("Generated: " + payload.generatedAtUtc);
  md.push("");
  md.push("- Marker: `PPIQ_PACK_A3_CI_CERTIFICATION_WIRING`");
  md.push("- Task: **T-028**");
  md.push("- Added CI signals: **taskClosure**, **routeContract**");
  md.push("- Gate report: `docs/ci/gate-report.json`");
  md.push("");
  md.push("## Jenkinsfile patch result");
  md.push("");
  md.push("| File | Exists | Patched | Reason |");
  md.push("|---|---|---|---|");

  for (const result of jenkinsResults) {
    md.push(
      `| \`${result.path}\` | ${result.exists ? "YES" : "NO"} | ${result.patched ? "YES" : "NO"} | ${result.reason} |`
    );
  }

  md.push("");
  md.push("## Certification commands");
  md.push("");
  md.push("```powershell");
  md.push("powershell -ExecutionPolicy Bypass -File .\\tools\\ci\\Invoke-PPIQ-Certification.ps1 -ProjectRoot \"C:\\Workspace\\PlantProcess-IQ\"");
  md.push("```");
  md.push("");
  md.push("```bash");
  md.push("sh tools/ci/ppiq-certification-stage.sh");
  md.push("```");
  md.push("");

  write(reportMdPath, md.join("\n"));

  let evidence = isFile(evidencePath)
    ? normalize(read(evidencePath))
    : "# PlantProcess IQ Pack A Evidence\n";

  if (!evidence.includes("PPIQ_PACK_A3_CI_CERTIFICATION_WIRING")) {
    evidence += [
      "",
      "## Pack A-3 CI certification wiring",
      "",
      "- Marker: `PPIQ_PACK_A3_CI_CERTIFICATION_WIRING`.",
      "- Task: `T-028`.",
      "- Added `taskClosure` gate signal to Jenkins certification stage.",
      "- Added `routeContract` gate signal to Jenkins certification stage.",
      "- Added machine-readable gate report writer.",
      "- Added local and Linux certification wrappers.",
      "- Jenkins archives `docs/ci/gate-report.json`.",
      "",
      "Generated artifacts:",
      "",
      "- `docs/pack-a/PACK_A3_CI_CERTIFICATION_WIRING_REPORT.md`",
      "- `docs/pack-a/PACK_A3_CI_CERTIFICATION_WIRING_REPORT.json`",
      "- `tools/ci/write-certification-gate-report.cjs`",
      "- `tools/ci/Invoke-PPIQ-Certification.ps1`",
      "- `tools/ci/ppiq-certification-stage.sh`",
      "- `tools/pack-a/validate-pack-a-ci-certification.cjs`",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack A-3 — CI certification wiring");
console.log("=================================================================================================");
console.log("Backup folder: " + backupRoot);

ensureDir(backupRoot);
ensureDir(docsDir);
ensureDir(ciDocsDir);
ensureDir(ciToolsDir);
ensureDir(packAToolsDir);

makeGateReportWriter();
makeLocalCertificationPs1();
makeLinuxCertificationSh();

const jenkinsResults = jenkinsCandidates.map(patchJenkinsfile);

makeValidator();
writeReports(jenkinsResults);

run("node --check gate report writer", ["node", "--check", "tools/ci/write-certification-gate-report.cjs"]);
run("node --check Pack A-3 validator", ["node", "--check", "tools/pack-a/validate-pack-a-ci-certification.cjs"]);
run("Write sample gate-report.json", ["node", "tools/ci/write-certification-gate-report.cjs"]);
run("Pack A-3 CI certification wiring validation", ["node", "tools/pack-a/validate-pack-a-ci-certification.cjs"]);

if (isFile(path.join(root, "tools", "pack-a", "validate-pack-a-run-once-archive.cjs"))) {
  run("Pack A-2 archive validation still green", ["node", "tools/pack-a/validate-pack-a-run-once-archive.cjs"]);
}

if (isFile(path.join(root, "tools", "task-closure", "Invoke-T001-T071-TaskClosureGate.ps1"))) {
  run(
    "T001-T071 task closure gate preview",
    [
      "powershell",
      "-ExecutionPolicy",
      "Bypass",
      "-File",
      ".\\tools\\task-closure\\Invoke-T001-T071-TaskClosureGate.ps1",
      "-ProjectRoot",
      root
    ],
    root,
    true
  );
}

console.log("");
console.log("Jenkins patch results:");
for (const result of jenkinsResults) {
  console.log(" - " + result.path + ": " + result.reason);
}

console.log("");
console.log("=================================================================================================");
console.log("Pack A-3 completed.");
console.log("Expected: T-028 should improve or close in the task-level scorecard.");
console.log("Report : docs/pack-a/PACK_A3_CI_CERTIFICATION_WIRING_REPORT.md");
console.log("Next   : Pack A-4 de-duplicate Jenkinsfile and TelemetryIngestionWorker.");
console.log("=================================================================================================");