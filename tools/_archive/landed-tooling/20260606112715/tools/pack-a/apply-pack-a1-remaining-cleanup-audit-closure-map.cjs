const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

const docsDir = path.join(root, "docs", "pack-a");
const toolsDir = path.join(root, "tools", "pack-a");

const auditJsonPath = path.join(docsDir, "PACK_A1_REMAINING_CLEANUP_AUDIT.json");
const auditMdPath = path.join(docsDir, "PACK_A1_REMAINING_CLEANUP_AUDIT.md");
const closureJsonPath = path.join(docsDir, "PACK_A_CLOSURE_MAP.json");
const closureMdPath = path.join(docsDir, "PACK_A_CLOSURE_MAP.md");
const evidencePath = path.join(docsDir, "PACK_A_IMPLEMENTATION_EVIDENCE.md");

const validatorPath = path.join(toolsDir, "validate-pack-a-closure-map.cjs");
const regressionWrapperPath = path.join(toolsDir, "Invoke-PackA-Regression.ps1");

const packATasks = [
  {
    task: "T-007",
    pack: "A",
    title: "De-duplicate Jenkinsfile and TelemetryIngestionWorker",
    currentScore: 75,
    currentStatus: "MOSTLY DONE",
    closeIn: "Pack A-4",
    objective:
      "Remove or neutralize duplicate CI/worker definitions so Jenkinsfile and TelemetryIngestionWorker have one clear canonical implementation each.",
    auditSignals: [
      "Jenkinsfile-like files",
      "TelemetryIngestionWorker files/classes",
      "duplicate worker registrations",
      "duplicate pipeline certification commands"
    ]
  },
  {
    task: "T-010",
    pack: "A",
    title: "Archive landed run-once tooling scripts",
    currentScore: 0,
    currentStatus: "NOT YET STARTED",
    closeIn: "Pack A-2",
    objective:
      "Move landed one-time apply/repair/continue scripts out of active tooling into an archive index so the active tools folder contains reusable validators and commands only.",
    auditSignals: [
      "apply-* scripts",
      "repair-* scripts",
      "continue-* scripts",
      "one-off generated scripts",
      "archive folder and archive index"
    ]
  },
  {
    task: "T-028",
    pack: "A",
    title: "Wire all gate-exit tests into CI certification stage",
    currentScore: 80,
    currentStatus: "MOSTLY DONE",
    closeIn: "Pack A-3",
    objective:
      "Ensure CI certification calls the core gate-exit validators: backend build/tests, frontend build, task closure, Pack B/Pack D gates, Phase 5/6 validation, and route-contract validation.",
    auditSignals: [
      "Jenkinsfile certification stage",
      "GitHub workflow certification stage",
      "task closure gate references",
      "phase56 validation references",
      "pack-b and pack-d validation references",
      "dotnet build/test and npm build references"
    ]
  },
  {
    task: "T-035",
    pack: "A",
    title: "Mapping and drift developer docs",
    currentScore: 25,
    currentStatus: "NOT YET STARTED",
    closeIn: "Pack A-5",
    objective:
      "Create developer documentation explaining mapping lifecycle, drift detection, validation gates, schema dictionary, safe SQL, and how to debug mapping/drift failures.",
    auditSignals: [
      "mapping docs",
      "drift docs",
      "developer docs",
      "schema mapping docs",
      "safe SQL docs",
      "gate-exit docs"
    ]
  }
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

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function normalize(text) {
  return text.replace(/\r\n/g, "\n");
}

function lineCount(file) {
  if (!isFile(file)) return 0;
  return normalize(read(file)).split("\n").length;
}

function walk(dir) {
  if (!exists(dir)) return [];

  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if ([
        ".git",
        ".vs",
        "bin",
        "obj",
        "node_modules",
        "dist",
        "coverage",
        ".pack_b_backup",
        ".pack_d_backup"
      ].includes(entry.name)) {
        return [];
      }

      return walk(full);
    }

    return [full];
  });
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

function getTextFiles() {
  const allowed = new Set([
    ".cs",
    ".csproj",
    ".sln",
    ".json",
    ".yml",
    ".yaml",
    ".ps1",
    ".cjs",
    ".js",
    ".ts",
    ".tsx",
    ".md",
    ".txt",
    ".sql",
    ".props",
    ".targets"
  ]);

  return walk(root).filter((file) => {
    const base = path.basename(file).toLowerCase();
    const ext = path.extname(file).toLowerCase();

    if (base.includes("jenkinsfile")) return true;
    if (base === "dockerfile") return true;

    return allowed.has(ext);
  });
}

function safeRead(file) {
  try {
    return normalize(read(file));
  } catch {
    return "";
  }
}

function searchFiles(patterns, files = getTextFiles()) {
  const result = [];

  for (const file of files) {
    const text = safeRead(file);
    if (!text) continue;

    const hits = [];

    for (const pattern of patterns) {
      const regex = pattern instanceof RegExp ? pattern : new RegExp(pattern, "i");
      if (regex.test(text) || regex.test(rel(file))) {
        hits.push(regex.toString());
      }
    }

    if (hits.length) {
      result.push({
        path: rel(file),
        lines: lineCount(file),
        hits
      });
    }
  }

  return result.sort((a, b) => a.path.localeCompare(b.path));
}

function findJenkinsFiles() {
  return walk(root)
    .filter((file) => path.basename(file).toLowerCase().includes("jenkinsfile"))
    .map((file) => ({
      path: rel(file),
      lines: lineCount(file)
    }))
    .sort((a, b) => a.path.localeCompare(b.path));
}

function findTelemetryWorkerSignals(files) {
  const workerFiles = searchFiles(
    [
      /TelemetryIngestionWorker/i,
      /class\s+TelemetryIngestionWorker/i,
      /AddHostedService\s*<\s*TelemetryIngestionWorker\s*>/i,
      /Telemetry.*Worker/i
    ],
    files
  );

  const classFiles = workerFiles.filter((item) => {
    const text = safeRead(path.join(root, item.path));
    return /class\s+TelemetryIngestionWorker/i.test(text);
  });

  const registrations = workerFiles.filter((item) => {
    const text = safeRead(path.join(root, item.path));
    return /AddHostedService\s*<\s*TelemetryIngestionWorker\s*>/i.test(text);
  });

  return {
    workerReferences: workerFiles,
    classDefinitions: classFiles,
    hostedServiceRegistrations: registrations,
    duplicateRisk:
      classFiles.length > 1 || registrations.length > 1
        ? "HIGH"
        : workerFiles.length > 0
          ? "LOW"
          : "UNKNOWN"
  };
}

function findRunOnceCandidates(files) {
  const activeToolFiles = files.filter((file) => rel(file).startsWith("tools/"));

  const candidates = activeToolFiles
    .filter((file) => {
      const relative = rel(file);
      const base = path.basename(file).toLowerCase();

      if (relative.includes("/archive/")) return false;
      if (relative.includes("/archived/")) return false;
      if (relative.includes("/_archive/")) return false;

      return (
        /^(apply|repair|continue|fix|patch)-/i.test(base) ||
        /-(apply|repair|continue|fix|patch)-/i.test(base) ||
        /run-once/i.test(base) ||
        /generated/i.test(base)
      );
    })
    .map((file) => ({
      path: rel(file),
      lines: lineCount(file),
      type: path.extname(file).replace(".", "") || "file"
    }))
    .sort((a, b) => a.path.localeCompare(b.path));

  const archiveSignals = files
    .filter((file) => /archive|archived|run-once/i.test(rel(file)))
    .map((file) => ({
      path: rel(file),
      lines: lineCount(file)
    }))
    .sort((a, b) => a.path.localeCompare(b.path));

  return {
    activeCandidates: candidates,
    archiveSignals,
    candidateCount: candidates.length,
    archiveExists: archiveSignals.length > 0
  };
}

function findCiSignals(files) {
  const ciFiles = files.filter((file) => {
    const relative = rel(file).toLowerCase();
    const base = path.basename(file).toLowerCase();

    return (
      base.includes("jenkinsfile") ||
      relative.includes(".github/workflows") ||
      relative.includes("azure-pipelines") ||
      relative.includes("gitlab-ci") ||
      relative.includes("ci") ||
      relative.includes("certification")
    );
  });

  const requiredSignals = [
    { key: "dotnetBuild", regex: /dotnet\s+build/i, label: "dotnet build" },
    { key: "dotnetTest", regex: /dotnet\s+test/i, label: "dotnet test" },
    { key: "npmBuild", regex: /npm\s+run\s+build/i, label: "npm run build" },
    { key: "phase56", regex: /validate-phase56|phase56/i, label: "Phase 5/6 validation" },
    { key: "taskClosure", regex: /Invoke-T001-T071-TaskClosureGate|validate-t001-t071-task-closure/i, label: "T001-T071 task closure" },
    { key: "packB", regex: /validate-pack-b|Pack B|pack-b/i, label: "Pack B validation" },
    { key: "packD", regex: /validate-pack-d|Pack D|pack-d/i, label: "Pack D validation" },
    { key: "routeContract", regex: /validate-pack-d-route-contract-snapshot|route-contract/i, label: "Route contract validation" }
  ];

  const fileSummaries = ciFiles.map((file) => {
    const text = safeRead(file);
    const signals = {};

    for (const signal of requiredSignals) {
      signals[signal.key] = signal.regex.test(text);
    }

    return {
      path: rel(file),
      lines: lineCount(file),
      signals
    };
  });

  const coverage = {};

  for (const signal of requiredSignals) {
    coverage[signal.key] = {
      label: signal.label,
      found: fileSummaries.some((file) => file.signals[signal.key]),
      files: fileSummaries
        .filter((file) => file.signals[signal.key])
        .map((file) => file.path)
    };
  }

  return {
    ciFiles: fileSummaries,
    coverage,
    missing: Object.entries(coverage)
      .filter(([, value]) => !value.found)
      .map(([key, value]) => ({ key, label: value.label }))
  };
}

function findMappingDriftDocs(files) {
  const docFiles = files.filter((file) => {
    const relative = rel(file).toLowerCase();
    const base = path.basename(file).toLowerCase();

    return (
      relative.startsWith("docs/") ||
      base.endsWith(".md") ||
      relative.includes("readme")
    );
  });

  const topics = [
    { key: "mappingLifecycle", regex: /mapping lifecycle|schema mapping lifecycle|canonical mapping/i, label: "mapping lifecycle" },
    { key: "drift", regex: /drift detection|schema drift|mapping drift|data drift/i, label: "drift detection" },
    { key: "businessKey", regex: /business key|business-key/i, label: "business-key dictionary" },
    { key: "safeSql", regex: /safe sql|safe-sql|ppiq_resolve_safe_sql/i, label: "safe SQL" },
    { key: "validationGate", regex: /gate|validator|closure|certification/i, label: "validation gates" },
    { key: "developerWorkflow", regex: /developer workflow|debug|troubleshoot|runbook/i, label: "developer workflow/runbook" }
  ];

  const matches = docFiles.map((file) => {
    const text = safeRead(file);
    const foundTopics = topics
      .filter((topic) => topic.regex.test(text) || topic.regex.test(rel(file)))
      .map((topic) => topic.key);

    return {
      path: rel(file),
      lines: lineCount(file),
      topics: foundTopics
    };
  }).filter((item) => item.topics.length);

  const coverage = {};

  for (const topic of topics) {
    coverage[topic.key] = {
      label: topic.label,
      found: matches.some((file) => file.topics.includes(topic.key)),
      files: matches
        .filter((file) => file.topics.includes(topic.key))
        .map((file) => file.path)
    };
  }

  return {
    matchingDocs: matches.sort((a, b) => a.path.localeCompare(b.path)),
    coverage,
    missing: Object.entries(coverage)
      .filter(([, value]) => !value.found)
      .map(([key, value]) => ({ key, label: value.label }))
  };
}

function readScorecard() {
  const scorecardPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.json");

  if (!isFile(scorecardPath)) {
    return {
      exists: false,
      path: rel(scorecardPath),
      packA: []
    };
  }

  try {
    const json = JSON.parse(read(scorecardPath));
    const rows = Array.isArray(json.tasks)
      ? json.tasks
      : Array.isArray(json)
        ? json
        : Array.isArray(json.rows)
          ? json.rows
          : [];

    return {
      exists: true,
      path: rel(scorecardPath),
      packA: rows.filter((row) => {
        const task = String(row.task || row.taskCode || row.id || "");
        const pack = String(row.pack || row.phase || "");
        return task.startsWith("T-0") && ["T-007", "T-010", "T-028", "T-035"].includes(task) || pack === "A";
      })
    };
  } catch (error) {
    return {
      exists: true,
      path: rel(scorecardPath),
      parseError: error.message,
      packA: []
    };
  }
}

function buildClosureMap(audit) {
  const closureMap = [
    {
      task: "T-010",
      packStep: "Pack A-2",
      title: "Archive landed run-once tooling scripts",
      priority: 1,
      risk: "LOW",
      reason: "Is 0% and mostly file organization/documentation if archive is done with manifest and no active validators moved.",
      acceptance: [
        "Create tools/archive/run-once or tools/_archive/run-once folder",
        "Move or copy landed one-off apply/repair/continue scripts into archive",
        "Create archive manifest with original path, reason, and date",
        "Keep reusable validators in active tools folders",
        "Task closure score for T-010 >= 90"
      ],
      currentFindings: {
        activeRunOnceCandidateCount: audit.t010.activeCandidates.length,
        archiveExists: audit.t010.archiveExists
      }
    },
    {
      task: "T-028",
      packStep: "Pack A-3",
      title: "Wire all gate-exit tests into CI certification stage",
      priority: 2,
      risk: "MEDIUM",
      reason: "CI wiring should be done after Pack B/D validators exist and are green.",
      acceptance: [
        "Certification stage references frontend build",
        "Certification stage references backend build/test",
        "Certification stage references Phase 5/6 validation",
        "Certification stage references Pack B validation",
        "Certification stage references Pack D validation",
        "Certification stage references T001-T071 task closure gate",
        "Task closure score for T-028 >= 90"
      ],
      currentFindings: {
        missingSignals: audit.t028.missing
      }
    },
    {
      task: "T-007",
      packStep: "Pack A-4",
      title: "De-duplicate Jenkinsfile and TelemetryIngestionWorker",
      priority: 3,
      risk: "MEDIUM",
      reason: "Dedup should happen after CI certification target is clear.",
      acceptance: [
        "Single canonical Jenkinsfile/pipeline definition or clear wrapper/redirect",
        "Single canonical TelemetryIngestionWorker class definition",
        "No duplicate hosted-service worker registrations",
        "Architecture/build tests remain green",
        "Task closure score for T-007 >= 90"
      ],
      currentFindings: {
        jenkinsfileCount: audit.t007.jenkinsFiles.length,
        telemetryClassDefinitions: audit.t007.telemetry.classDefinitions.length,
        telemetryHostedServiceRegistrations: audit.t007.telemetry.hostedServiceRegistrations.length,
        duplicateRisk: audit.t007.telemetry.duplicateRisk
      }
    },
    {
      task: "T-035",
      packStep: "Pack A-5",
      title: "Mapping and drift developer docs",
      priority: 4,
      risk: "LOW",
      reason: "Documentation should reference final validators and current mapping/drift reality.",
      acceptance: [
        "Add mapping lifecycle developer guide",
        "Add drift detection developer guide/runbook",
        "Document business-key dictionary validation",
        "Document safe SQL resolver behavior",
        "Document troubleshooting and gate commands",
        "Task closure score for T-035 >= 90"
      ],
      currentFindings: {
        missingDocTopics: audit.t035.missing
      }
    }
  ];

  return {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_A1_CLOSURE_MAP",
    recommendedOrder: closureMap.map((item) => item.task),
    closureMap
  };
}

function writeAuditMarkdown(audit, closureMap) {
  const md = [];

  md.push("# Pack A-1 Remaining Cleanup Audit + Closure Map");
  md.push("");
  md.push("Generated: " + audit.generatedAtUtc);
  md.push("");
  md.push("## Executive Summary");
  md.push("");
  md.push("Pack A has four remaining below-90 tasks. The fastest safe route is: archive landed one-off scripts, wire CI certification, de-duplicate Jenkins/Telemetry surfaces, then close mapping/drift developer documentation.");
  md.push("");
  md.push("## Remaining Pack A Tasks");
  md.push("");
  md.push("| Task | Score | Status | Recommended Step | Objective |");
  md.push("|---|---:|---|---|---|");

  for (const task of packATasks) {
    md.push(
      `| ${task.task} | ${task.currentScore}% | ${task.currentStatus} | ${task.closeIn} | ${task.objective} |`
    );
  }

  md.push("");
  md.push("## T-007 Audit — Jenkinsfile + TelemetryIngestionWorker");
  md.push("");
  md.push("- Jenkinsfile-like files: **" + audit.t007.jenkinsFiles.length + "**");
  md.push("- Telemetry worker class definitions: **" + audit.t007.telemetry.classDefinitions.length + "**");
  md.push("- Telemetry hosted-service registrations: **" + audit.t007.telemetry.hostedServiceRegistrations.length + "**");
  md.push("- Duplicate risk: **" + audit.t007.telemetry.duplicateRisk + "**");
  md.push("");
  md.push("### Jenkinsfile-like files");
  md.push("");
  md.push("| File | Lines |");
  md.push("|---|---:|");
  for (const item of audit.t007.jenkinsFiles.slice(0, 50)) {
    md.push(`| \`${item.path}\` | ${item.lines} |`);
  }

  md.push("");
  md.push("### Telemetry worker references");
  md.push("");
  md.push("| File | Lines |");
  md.push("|---|---:|");
  for (const item of audit.t007.telemetry.workerReferences.slice(0, 50)) {
    md.push(`| \`${item.path}\` | ${item.lines} |`);
  }

  md.push("");
  md.push("## T-010 Audit — Run-once tooling archive");
  md.push("");
  md.push("- Active archive candidates: **" + audit.t010.activeCandidates.length + "**");
  md.push("- Existing archive signals: **" + audit.t010.archiveSignals.length + "**");
  md.push("");
  md.push("| Candidate | Lines | Type |");
  md.push("|---|---:|---|");
  for (const item of audit.t010.activeCandidates.slice(0, 100)) {
    md.push(`| \`${item.path}\` | ${item.lines} | ${item.type} |`);
  }

  md.push("");
  md.push("## T-028 Audit — CI certification wiring");
  md.push("");
  md.push("| Signal | Found | Files |");
  md.push("|---|---|---|");
  for (const [, value] of Object.entries(audit.t028.coverage)) {
    md.push(
      `| ${value.label} | ${value.found ? "YES" : "**NO**"} | ${value.files.map((x) => "`" + x + "`").join("<br>")} |`
    );
  }

  md.push("");
  md.push("## T-035 Audit — Mapping and drift developer docs");
  md.push("");
  md.push("| Topic | Found | Files |");
  md.push("|---|---|---|");
  for (const [, value] of Object.entries(audit.t035.coverage)) {
    md.push(
      `| ${value.label} | ${value.found ? "YES" : "**NO**"} | ${value.files.map((x) => "`" + x + "`").join("<br>")} |`
    );
  }

  md.push("");
  md.push("## Closure Order");
  md.push("");
  md.push("| Priority | Task | Pack Step | Risk | Reason |");
  md.push("|---:|---|---|---|---|");
  for (const item of closureMap.closureMap) {
    md.push(`| ${item.priority} | ${item.task} | ${item.packStep} | ${item.risk} | ${item.reason} |`);
  }

  md.push("");
  md.push("## Next Commands");
  md.push("");
  md.push("```powershell");
  md.push("node .\\tools\\pack-a\\validate-pack-a-closure-map.cjs");
  md.push("powershell -ExecutionPolicy Bypass -File .\\tools\\pack-a\\Invoke-PackA-Regression.ps1 -ProjectRoot \"C:\\Workspace\\PlantProcess-IQ\"");
  md.push("```");
  md.push("");

  write(auditMdPath, md.join("\n"));
}

function writeClosureMarkdown(closureMap) {
  const md = [];

  md.push("# Pack A Closure Map");
  md.push("");
  md.push("Generated: " + closureMap.generatedAtUtc);
  md.push("");
  md.push("## Recommended Order");
  md.push("");
  md.push("1. **Pack A-2 / T-010** — Archive landed run-once tooling scripts.");
  md.push("2. **Pack A-3 / T-028** — Wire gate-exit tests into CI certification.");
  md.push("3. **Pack A-4 / T-007** — De-duplicate Jenkinsfile and TelemetryIngestionWorker.");
  md.push("4. **Pack A-5 / T-035** — Mapping and drift developer docs.");
  md.push("");
  md.push("## Acceptance by Task");
  md.push("");

  for (const item of closureMap.closureMap) {
    md.push("### " + item.task + " — " + item.title);
    md.push("");
    md.push("- Pack step: **" + item.packStep + "**");
    md.push("- Priority: **" + item.priority + "**");
    md.push("- Risk: **" + item.risk + "**");
    md.push("- Reason: " + item.reason);
    md.push("");
    md.push("Acceptance:");
    md.push("");
    for (const acceptance of item.acceptance) {
      md.push("- " + acceptance);
    }
    md.push("");
  }

  write(closureMdPath, md.join("\n"));
}

function writeValidator() {
  const validator = `const fs = require("fs");
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
`;

  write(validatorPath, validator);
}

function writeRegressionWrapper() {
  const wrapper = `[CmdletBinding()]
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
    Run-Step "Pack A-1 closure map validation" {
        node ".\\tools\\pack-a\\validate-pack-a-closure-map.cjs"
    }

    if (Test-Path ".\\tools\\task-closure\\Invoke-T001-T071-TaskClosureGate.ps1") {
        Run-Step "T001-T071 task closure gate" {
            powershell -ExecutionPolicy Bypass -File ".\\tools\\task-closure\\Invoke-T001-T071-TaskClosureGate.ps1" -ProjectRoot $ProjectRoot
        }
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

    Write-Host ""
    Write-Host "Pack A regression wrapper completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
`;

  write(regressionWrapperPath, wrapper);
}

function writeEvidence() {
  let evidence = exists(evidencePath)
    ? normalize(read(evidencePath))
    : "# PlantProcess IQ Pack A Evidence\n";

  if (!evidence.includes("PPIQ_PACK_A1_REMAINING_CLEANUP_AUDIT_CLOSURE_MAP")) {
    evidence += [
      "",
      "## Pack A-1 Remaining cleanup audit + closure map",
      "",
      "- Marker: `PPIQ_PACK_A1_REMAINING_CLEANUP_AUDIT_CLOSURE_MAP`.",
      "- Audited remaining Pack A below-90 tasks: T-007, T-010, T-028, T-035.",
      "- Created closure map with recommended order: T-010, T-028, T-007, T-035.",
      "- Added closure-map validator.",
      "- Added Pack A regression wrapper.",
      "",
      "Generated artifacts:",
      "",
      "- `docs/pack-a/PACK_A1_REMAINING_CLEANUP_AUDIT.md`",
      "- `docs/pack-a/PACK_A1_REMAINING_CLEANUP_AUDIT.json`",
      "- `docs/pack-a/PACK_A_CLOSURE_MAP.md`",
      "- `docs/pack-a/PACK_A_CLOSURE_MAP.json`",
      "- `tools/pack-a/validate-pack-a-closure-map.cjs`",
      "- `tools/pack-a/Invoke-PackA-Regression.ps1`",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack A-1 — Remaining cleanup audit + closure map");
console.log("=================================================================================================");

ensureDir(docsDir);
ensureDir(toolsDir);

const files = getTextFiles();
const scorecard = readScorecard();

const audit = {
  generatedAtUtc: new Date().toISOString(),
  marker: "PPIQ_PACK_A1_REMAINING_CLEANUP_AUDIT_CLOSURE_MAP",
  scorecard,
  tasks: packATasks,
  t007: {
    jenkinsFiles: findJenkinsFiles(),
    telemetry: findTelemetryWorkerSignals(files)
  },
  t010: findRunOnceCandidates(files),
  t028: findCiSignals(files),
  t035: findMappingDriftDocs(files)
};

const closureMap = buildClosureMap(audit);

write(auditJsonPath, JSON.stringify(audit, null, 2) + "\n");
write(closureJsonPath, JSON.stringify(closureMap, null, 2) + "\n");

writeAuditMarkdown(audit, closureMap);
writeClosureMarkdown(closureMap);
writeValidator();
writeRegressionWrapper();
writeEvidence();

console.log("");
console.log("Pack A-1 audit summary:");
console.log(" - T-007 Jenkinsfile-like files          : " + audit.t007.jenkinsFiles.length);
console.log(" - T-007 Telemetry worker references     : " + audit.t007.telemetry.workerReferences.length);
console.log(" - T-007 Telemetry duplicate risk        : " + audit.t007.telemetry.duplicateRisk);
console.log(" - T-010 active run-once candidates      : " + audit.t010.activeCandidates.length);
console.log(" - T-028 missing CI certification signals: " + audit.t028.missing.map((x) => x.key).join(", "));
console.log(" - T-035 missing doc topics              : " + audit.t035.missing.map((x) => x.key).join(", "));

console.log("");
console.log("Recommended Pack A closure order:");
for (const item of closureMap.closureMap) {
  console.log(" - " + item.packStep + " / " + item.task + " / " + item.title);
}

run("node --check Pack A-1 validator", ["node", "--check", "tools/pack-a/validate-pack-a-closure-map.cjs"]);
run("Pack A-1 closure map validation", ["node", "tools/pack-a/validate-pack-a-closure-map.cjs"]);

if (exists(path.join(root, "tools", "task-closure", "Invoke-T001-T071-TaskClosureGate.ps1"))) {
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
console.log("=================================================================================================");
console.log("Pack A-1 completed.");
console.log("Audit      : docs/pack-a/PACK_A1_REMAINING_CLEANUP_AUDIT.md");
console.log("Closure map: docs/pack-a/PACK_A_CLOSURE_MAP.md");
console.log("Next       : Pack A-2 archive landed run-once tooling scripts / close T-010.");
console.log("=================================================================================================");