const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

const docsDir = path.join(root, "docs", "pack-f");
const toolsDir = path.join(root, "tools", "pack-f");

const auditJsonPath = path.join(docsDir, "PACK_F1_EDGE_AGENT_AUDIT.json");
const auditMdPath = path.join(docsDir, "PACK_F1_EDGE_AGENT_AUDIT.md");
const closureJsonPath = path.join(docsDir, "PACK_F_CLOSURE_MAP.json");
const closureMdPath = path.join(docsDir, "PACK_F_CLOSURE_MAP.md");
const evidencePath = path.join(docsDir, "PACK_F_IMPLEMENTATION_EVIDENCE.md");
const validatorPath = path.join(toolsDir, "validate-pack-f-closure-map.cjs");
const regressionWrapperPath = path.join(toolsDir, "Invoke-PackF-Regression.ps1");

const packFTasks = [
  {
    task: "T-066",
    pack: "F",
    title: "Build OT-safe edge agent one-way push",
    currentScore: 17,
    status: "NOT YET STARTED",
    closeIn: "Pack F-2",
    objective:
      "Create an OT-safe, read-only edge collector/agent pattern that only pushes outbound data to PlantProcess IQ and never requires inbound OT network access."
  },
  {
    task: "T-067",
    pack: "F",
    title: "Edge agent packaging and deployment",
    currentScore: 0,
    status: "NOT YET STARTED",
    closeIn: "Pack F-3",
    objective:
      "Package the edge agent with deployment scripts, sample configuration, service wrapper guidance, Docker-friendly mode, and safety documentation."
  },
  {
    task: "T-068",
    pack: "F",
    title: "Edge collector management UX",
    currentScore: 50,
    status: "PARTIALLY DONE",
    closeIn: "Pack F-4",
    objective:
      "Expose edge collector registration, heartbeat, queue status, collector profile, one-way push status, and deployment guidance in the UI."
  },
  {
    task: "T-071",
    pack: "F",
    title: "Edge tests docs regression",
    currentScore: 60,
    status: "PARTIALLY DONE",
    closeIn: "Pack F-5",
    objective:
      "Lock Pack F with validators, build regression, documentation, final scorecard bridge, and OT-safety proof."
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

function shouldSkipDir(name, fullPath) {
  const relative = rel(fullPath).toLowerCase();

  return [
    ".git",
    ".vs",
    "bin",
    "obj",
    "node_modules",
    "dist",
    "coverage"
  ].includes(name.toLowerCase()) ||
  relative.includes("/tools/_archive/") ||
  relative.includes("/.pack_a_backup/") ||
  relative.includes("/.pack_b_backup/") ||
  relative.includes("/.pack_d_backup/") ||
  relative.includes("/.pack_e_backup/") ||
  relative.includes("/.pack_f_backup/");
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

function getTextFiles() {
  const allowed = new Set([
    ".cs",
    ".csproj",
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
    ".css",
    ".dockerfile"
  ]);

  return walk(root).filter((file) => {
    const base = path.basename(file).toLowerCase();
    return allowed.has(path.extname(file).toLowerCase()) || base === "dockerfile";
  });
}

function safeRead(file) {
  try {
    return normalize(read(file));
  } catch {
    return "";
  }
}

function searchFiles(patterns, files) {
  const result = [];

  for (const file of files) {
    const relative = rel(file);
    const text = safeRead(file);
    const hits = [];

    for (const pattern of patterns) {
      if (pattern.test(relative) || pattern.test(text)) {
        hits.push(pattern.toString());
      }
    }

    if (hits.length) {
      result.push({
        path: relative,
        lines: lineCount(file),
        hits
      });
    }
  }

  return result.sort((a, b) => a.path.localeCompare(b.path));
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

function readScorecard() {
  const scorecardPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.json");

  if (!isFile(scorecardPath)) {
    return {
      exists: false,
      path: rel(scorecardPath),
      packF: []
    };
  }

  try {
    const json = JSON.parse(read(scorecardPath));
    const rows = Array.isArray(json.tasks)
      ? json.tasks
      : Array.isArray(json.rows)
        ? json.rows
        : Array.isArray(json)
          ? json
          : Array.isArray(json.items)
            ? json.items
            : [];

    return {
      exists: true,
      path: rel(scorecardPath),
      packF: rows.filter((row) => {
        const task = String(row.task || row.taskCode || row.id || row.code || "");
        const pack = String(row.pack || row.phase || row.group || "");
        return ["T-066", "T-067", "T-068", "T-071"].includes(task) || pack === "F";
      })
    };
  } catch (error) {
    return {
      exists: true,
      path: rel(scorecardPath),
      parseError: error.message,
      packF: []
    };
  }
}

function classifyBackend(files) {
  const backendFiles = files.filter((file) => rel(file).startsWith("Backend/"));

  return {
    edgeSignals: searchFiles([/edge/i, /collector/i, /agent/i, /one-way/i, /oneway/i, /outbound/i, /heartbeat/i, /spool/i, /queue/i], backendFiles),
    workerSignals: searchFiles([/BackgroundService/i, /IHostedService/i, /Worker/i, /Edge.*Agent/i, /Collector.*Worker/i, /Telemetry.*Worker/i], backendFiles),
    apiSignals: searchFiles([/edge.*endpoint/i, /collector.*endpoint/i, /heartbeat/i, /agent.*status/i, /collector.*status/i, /queue.*status/i], backendFiles),
    safetySignals: searchFiles([/read-only/i, /read only/i, /no inbound/i, /outbound only/i, /one-way/i, /OT-safe/i, /OT safe/i, /DMZ/i, /firewall/i], backendFiles),
    testSignals: searchFiles([/Edge/i, /Collector/i, /Agent/i, /Heartbeat/i, /OneWay/i, /Queue/i], backendFiles.filter((file) => rel(file).includes("/tests/")))
  };
}

function classifyFrontend(files) {
  const frontendFiles = files.filter((file) => rel(file).startsWith("Frontend/"));

  return {
    edgeSignals: searchFiles([/edge/i, /collector/i, /agent/i, /heartbeat/i, /one-way/i, /outbound/i, /queue/i], frontendFiles),
    pageSignals: searchFiles([/Edge/i, /Collector/i, /Agent/i, /Heartbeat/i, /Queue/i, /Deploy/i, /Package/i], frontendFiles),
    apiClientSignals: searchFiles([/edge/i, /collector/i, /agent/i, /heartbeat/i, /queue/i], frontendFiles.filter((file) => file.endsWith(".ts") || file.endsWith(".tsx")))
  };
}

function classifyInfrastructure(files) {
  const infraFiles = files.filter((file) => {
    const relative = rel(file);
    return relative.startsWith("Infrastructure/") ||
      relative.startsWith("scripts/") ||
      relative.startsWith("deploy/") ||
      relative.includes("docker") ||
      relative.endsWith("Dockerfile") ||
      relative.endsWith(".yml") ||
      relative.endsWith(".yaml") ||
      relative.endsWith(".ps1");
  });

  return {
    packagingSignals: searchFiles([/edge/i, /collector/i, /agent/i, /docker/i, /compose/i, /service/i, /install/i, /deploy/i, /systemd/i, /windows service/i], infraFiles),
    configSignals: searchFiles([/edge.*config/i, /collector.*config/i, /agent.*config/i, /outbound/i, /endpoint/i, /secret/i, /certificate/i], infraFiles)
  };
}

function classifyDocs(files) {
  const docFiles = files.filter((file) => {
    const relative = rel(file).toLowerCase();
    return relative.startsWith("docs/") || relative.endsWith(".md");
  });

  return {
    edgeDocs: searchFiles([/edge/i, /collector/i, /agent/i, /one-way/i, /outbound/i, /heartbeat/i, /queue/i], docFiles),
    safetyDocs: searchFiles([/OT-safe/i, /OT safe/i, /no inbound/i, /outbound only/i, /read-only/i, /firewall/i, /DMZ/i, /zero trust/i], docFiles),
    deploymentDocs: searchFiles([/deploy/i, /package/i, /docker/i, /service/i, /install/i, /systemd/i, /windows service/i], docFiles)
  };
}

function buildClosureMap(audit) {
  const closureMap = [
    {
      task: "T-066",
      packStep: "Pack F-2",
      title: "OT-safe edge agent one-way push backend",
      priority: 1,
      risk: "HIGH",
      reason:
        "The edge architecture must be safe before packaging or UX. It must prove read-only collection and outbound-only push.",
      acceptance: [
        "One canonical edge agent/collector contract exists",
        "Agent config supports outbound PlantProcess IQ API endpoint",
        "Agent never opens inbound OT-facing listener",
        "Agent supports read-only collection profile",
        "Agent supports local spool/queue model",
        "Agent supports heartbeat payload",
        "Agent supports bounded batch push payload",
        "Server/API accepts heartbeat and batch push",
        "OT-safety documentation exists",
        "Backend build remains green"
      ],
      auditFindings: {
        backendEdgeSignals: audit.backend.edgeSignals.length,
        backendWorkerSignals: audit.backend.workerSignals.length,
        backendSafetySignals: audit.backend.safetySignals.length
      }
    },
    {
      task: "T-067",
      packStep: "Pack F-3",
      title: "Edge agent packaging and deployment",
      priority: 2,
      risk: "MEDIUM",
      reason:
        "After the safety contract exists, package it for repeatable demo/customer deployment without mixing local/server DB or unsafe inbound assumptions.",
      acceptance: [
        "Sample edge-agent configuration exists",
        "Packaging/deployment script exists",
        "Docker or service-wrapper mode is documented",
        "Install/uninstall or run-local commands are documented",
        "Secrets are referenced, not hardcoded",
        "Environment separation is explicit",
        "Deployment report and validator exist"
      ],
      auditFindings: {
        packagingSignals: audit.infrastructure.packagingSignals.length,
        configSignals: audit.infrastructure.configSignals.length,
        deploymentDocs: audit.docs.deploymentDocs.length
      }
    },
    {
      task: "T-068",
      packStep: "Pack F-4",
      title: "Edge collector management UX",
      priority: 3,
      risk: "MEDIUM",
      reason:
        "The UI should display edge collector readiness, heartbeat, queue, and deployment status only after backend and packaging contracts exist.",
      acceptance: [
        "UI route exists for edge collectors",
        "UI shows collector profile",
        "UI shows heartbeat status",
        "UI shows queue/spool status",
        "UI shows one-way push status",
        "UI shows deployment command guidance",
        "UI is honest about OT-safe outbound-only behavior",
        "Frontend build remains green"
      ],
      auditFindings: {
        frontendEdgeSignals: audit.frontend.edgeSignals.length,
        frontendPageSignals: audit.frontend.pageSignals.length,
        frontendApiSignals: audit.frontend.apiClientSignals.length
      }
    },
    {
      task: "T-071",
      packStep: "Pack F-5",
      title: "Edge tests, docs, regression, final closure",
      priority: 4,
      risk: "LOW",
      reason:
        "After backend, packaging, and UI are complete, lock Pack F with validators, documentation, build regression, and scorecard closure.",
      acceptance: [
        "Pack F backend validator exists",
        "Pack F packaging validator exists",
        "Pack F UI validator exists",
        "Pack F OT-safety contract snapshot exists",
        "Pack F regression wrapper exists",
        "Pack F final scorecard bridge exists",
        "Backend and frontend build remain green",
        "Remaining below-90 tasks drop to zero"
      ],
      auditFindings: {
        backendTestSignals: audit.backend.testSignals.length,
        edgeDocs: audit.docs.edgeDocs.length,
        safetyDocs: audit.docs.safetyDocs.length
      }
    }
  ];

  return {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_F1_EDGE_AGENT_CLOSURE_MAP",
    recommendedOrder: closureMap.map((item) => item.task),
    closureMap
  };
}

function writeAuditMarkdown(audit, closureMap) {
  const md = [];

  md.push("# Pack F-1 OT-Safe Edge Agent Audit + Closure Map");
  md.push("");
  md.push("Generated: " + audit.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_F1_EDGE_AGENT_AUDIT_CLOSURE_MAP");
  md.push("");
  md.push("## Executive Summary");
  md.push("");
  md.push("Pack F closes the remaining OT-safe edge-agent scope: one-way push backend/agent, packaging/deployment, management UX, and final regression/docs closure.");
  md.push("");
  md.push("The safety principle is non-negotiable: the edge agent must be read-only toward OT sources and outbound-only toward PlantProcess IQ. It must not require inbound access into the OT network.");
  md.push("");
  md.push("## Remaining Pack F Tasks");
  md.push("");
  md.push("| Task | Score | Status | Recommended Step | Objective |");
  md.push("|---|---:|---|---|---|");

  for (const task of packFTasks) {
    md.push("| " + task.task + " | " + task.currentScore + "% | " + task.status + " | " + task.closeIn + " | " + task.objective + " |");
  }

  md.push("");
  md.push("## Backend / Agent Signals");
  md.push("");
  md.push("- Backend edge signals: **" + audit.backend.edgeSignals.length + "**");
  md.push("- Backend worker signals: **" + audit.backend.workerSignals.length + "**");
  md.push("- Backend API signals: **" + audit.backend.apiSignals.length + "**");
  md.push("- Backend OT-safety signals: **" + audit.backend.safetySignals.length + "**");
  md.push("- Backend test signals: **" + audit.backend.testSignals.length + "**");
  md.push("");
  md.push("### Backend candidates");
  md.push("");
  md.push("| File | Lines |");
  md.push("|---|---:|");
  for (const item of audit.backend.edgeSignals.slice(0, 80)) {
    md.push("| `" + item.path + "` | " + item.lines + " |");
  }

  md.push("");
  md.push("## Frontend Signals");
  md.push("");
  md.push("- Frontend edge signals: **" + audit.frontend.edgeSignals.length + "**");
  md.push("- Frontend page/component signals: **" + audit.frontend.pageSignals.length + "**");
  md.push("- Frontend API client signals: **" + audit.frontend.apiClientSignals.length + "**");
  md.push("");

  md.push("## Infrastructure / Packaging Signals");
  md.push("");
  md.push("- Packaging signals: **" + audit.infrastructure.packagingSignals.length + "**");
  md.push("- Config signals: **" + audit.infrastructure.configSignals.length + "**");
  md.push("");

  md.push("## Docs Signals");
  md.push("");
  md.push("- Edge docs: **" + audit.docs.edgeDocs.length + "**");
  md.push("- OT-safety docs: **" + audit.docs.safetyDocs.length + "**");
  md.push("- Deployment docs: **" + audit.docs.deploymentDocs.length + "**");
  md.push("");

  md.push("## Closure Order");
  md.push("");
  md.push("| Priority | Task | Step | Risk | Reason |");
  md.push("|---:|---|---|---|---|");

  for (const item of closureMap.closureMap) {
    md.push("| " + item.priority + " | " + item.task + " | " + item.packStep + " | " + item.risk + " | " + item.reason + " |");
  }

  md.push("");
  md.push("## Next Step");
  md.push("");
  md.push("Next implementation step: **Pack F-2 / T-066 — OT-safe edge agent one-way push backend**.");
  md.push("");

  write(auditMdPath, md.join("\n"));
}

function writeClosureMarkdown(closureMap) {
  const md = [];

  md.push("# Pack F Closure Map");
  md.push("");
  md.push("Generated: " + closureMap.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_F1_EDGE_AGENT_CLOSURE_MAP");
  md.push("");
  md.push("## Recommended Execution Order");
  md.push("");
  md.push("1. Pack F-2 / T-066 — OT-safe edge agent one-way push backend.");
  md.push("2. Pack F-3 / T-067 — Edge agent packaging and deployment.");
  md.push("3. Pack F-4 / T-068 — Edge collector management UX.");
  md.push("4. Pack F-5 / T-071 — Edge tests/docs/regression and final closure.");
  md.push("");
  md.push("## Acceptance by Task");
  md.push("");

  for (const item of closureMap.closureMap) {
    md.push("### " + item.task + " — " + item.title);
    md.push("");
    md.push("- Pack step: " + item.packStep);
    md.push("- Priority: " + item.priority);
    md.push("- Risk: " + item.risk);
    md.push("- Reason: " + item.reason);
    md.push("");
    md.push("Acceptance:");
    md.push("");

    for (const rule of item.acceptance) {
      md.push("- " + rule);
    }

    md.push("");
  }

  write(closureMdPath, md.join("\n"));
}

function writeValidator() {
  const lines = [];

  lines.push('const fs = require("fs");');
  lines.push('const path = require("path");');
  lines.push('');
  lines.push('const root = process.cwd();');
  lines.push('');
  lines.push('const requiredFiles = [');
  lines.push('  "docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.json",');
  lines.push('  "docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.md",');
  lines.push('  "docs/pack-f/PACK_F_CLOSURE_MAP.json",');
  lines.push('  "docs/pack-f/PACK_F_CLOSURE_MAP.md",');
  lines.push('  "docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md"');
  lines.push('];');
  lines.push('');
  lines.push('function isFile(relativePath) {');
  lines.push('  const absolute = path.join(root, relativePath);');
  lines.push('  return fs.existsSync(absolute) && fs.statSync(absolute).isFile();');
  lines.push('}');
  lines.push('');
  lines.push('function read(relativePath) {');
  lines.push('  return fs.readFileSync(path.join(root, relativePath), "utf8");');
  lines.push('}');
  lines.push('');
  lines.push('const failures = [];');
  lines.push('');
  lines.push('for (const file of requiredFiles) {');
  lines.push('  if (!isFile(file)) failures.push({ file, reason: "missing" });');
  lines.push('}');
  lines.push('');
  lines.push('if (isFile("docs/pack-f/PACK_F_CLOSURE_MAP.json")) {');
  lines.push('  const map = JSON.parse(read("docs/pack-f/PACK_F_CLOSURE_MAP.json"));');
  lines.push('  const tasks = new Set((map.closureMap || []).map((item) => item.task));');
  lines.push('  for (const task of ["T-066", "T-067", "T-068", "T-071"]) {');
  lines.push('    if (!tasks.has(task)) failures.push({ task, reason: "missing-from-closure-map" });');
  lines.push('  }');
  lines.push('}');
  lines.push('');
  lines.push('if (isFile("docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.json")) {');
  lines.push('  const audit = JSON.parse(read("docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.json"));');
  lines.push('  for (const section of ["backend", "frontend", "infrastructure", "docs", "scorecard"]) {');
  lines.push('    if (!audit[section]) failures.push({ section, reason: "missing-audit-section" });');
  lines.push('  }');
  lines.push('}');
  lines.push('');
  lines.push('if (isFile("docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md")) {');
  lines.push('  const evidence = read("docs/pack-f/PACK_F_IMPLEMENTATION_EVIDENCE.md");');
  lines.push('  if (!evidence.includes("PPIQ_PACK_F1_EDGE_AGENT_AUDIT_CLOSURE_MAP")) failures.push({ reason: "evidence-marker-missing" });');
  lines.push('}');
  lines.push('');
  lines.push('if (failures.length) {');
  lines.push('  console.error("Pack F-1 closure map validation failed.");');
  lines.push('  console.error(JSON.stringify(failures, null, 2));');
  lines.push('  process.exit(1);');
  lines.push('}');
  lines.push('');
  lines.push('console.log("Pack F-1 edge-agent closure map validation passed.");');

  write(validatorPath, lines.join("\n") + "\n");
}

function writeRegressionWrapper() {
  const content = [
    "[CmdletBinding()]",
    "param(",
    "    [string]$ProjectRoot = (Resolve-Path \".\").Path,",
    "    [switch]$RunBuilds",
    ")",
    "",
    "$ErrorActionPreference = \"Stop\"",
    "",
    "function Run-Step([string]$Name, [scriptblock]$Block) {",
    "    Write-Host \"\"",
    "    Write-Host \"---- $Name\" -ForegroundColor Cyan",
    "    & $Block",
    "    if ($LASTEXITCODE -ne 0) { throw \"$Name failed with exit code $LASTEXITCODE\" }",
    "}",
    "",
    "Push-Location $ProjectRoot",
    "try {",
    "    Run-Step \"Pack F-1 closure map validation\" {",
    "        node \".\\tools\\pack-f\\validate-pack-f-closure-map.cjs\"",
    "    }",
    "",
    "    if ($RunBuilds) {",
    "        Run-Step \"Backend build\" { dotnet build \".\\Backend\" }",
    "        Push-Location \".\\Frontend\\PlantProcess.Web\"",
    "        try { Run-Step \"Frontend build\" { npm.cmd run build } }",
    "        finally { Pop-Location }",
    "    }",
    "",
    "    Write-Host \"\"",
    "    Write-Host \"Pack F regression wrapper completed.\" -ForegroundColor Green",
    "}",
    "finally { Pop-Location }",
    ""
  ].join("\n");

  write(regressionWrapperPath, content);
}

function writeEvidence() {
  let evidence = isFile(evidencePath)
    ? normalize(read(evidencePath))
    : "# PlantProcess IQ Pack F Evidence\n";

  if (!evidence.includes("PPIQ_PACK_F1_EDGE_AGENT_AUDIT_CLOSURE_MAP")) {
    evidence += [
      "",
      "## Pack F-1 OT-safe edge agent audit and closure map",
      "",
      "- Marker: PPIQ_PACK_F1_EDGE_AGENT_AUDIT_CLOSURE_MAP.",
      "- Audited remaining Pack F tasks: T-066, T-067, T-068, T-071.",
      "- Created closure map with recommended order: edge one-way backend, packaging/deployment, management UX, tests/docs/regression.",
      "- Added Pack F closure-map validator.",
      "- Added Pack F regression wrapper.",
      "",
      "Generated artifacts:",
      "",
      "- docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.md",
      "- docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.json",
      "- docs/pack-f/PACK_F_CLOSURE_MAP.md",
      "- docs/pack-f/PACK_F_CLOSURE_MAP.json",
      "- tools/pack-f/validate-pack-f-closure-map.cjs",
      "- tools/pack-f/Invoke-PackF-Regression.ps1",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack F-1 — OT-safe edge agent audit + closure map");
console.log("=================================================================================================");

ensureDir(docsDir);
ensureDir(toolsDir);

const files = getTextFiles();

const audit = {
  generatedAtUtc: new Date().toISOString(),
  marker: "PPIQ_PACK_F1_EDGE_AGENT_AUDIT_CLOSURE_MAP",
  tasks: packFTasks,
  scorecard: readScorecard(),
  backend: classifyBackend(files),
  frontend: classifyFrontend(files),
  infrastructure: classifyInfrastructure(files),
  docs: classifyDocs(files)
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
console.log("Pack F-1 audit summary:");
console.log(" - Backend edge signals          : " + audit.backend.edgeSignals.length);
console.log(" - Backend worker signals        : " + audit.backend.workerSignals.length);
console.log(" - Backend API signals           : " + audit.backend.apiSignals.length);
console.log(" - Backend OT-safety signals     : " + audit.backend.safetySignals.length);
console.log(" - Backend test signals          : " + audit.backend.testSignals.length);
console.log(" - Frontend edge signals         : " + audit.frontend.edgeSignals.length);
console.log(" - Frontend page/API signals     : " + (audit.frontend.pageSignals.length + audit.frontend.apiClientSignals.length));
console.log(" - Packaging signals             : " + audit.infrastructure.packagingSignals.length);
console.log(" - Config signals                : " + audit.infrastructure.configSignals.length);
console.log(" - Edge docs                     : " + audit.docs.edgeDocs.length);
console.log(" - OT-safety docs                : " + audit.docs.safetyDocs.length);
console.log(" - Deployment docs               : " + audit.docs.deploymentDocs.length);

console.log("");
console.log("Recommended Pack F closure order:");
for (const item of closureMap.closureMap) {
  console.log(" - " + item.packStep + " / " + item.task + " / " + item.title);
}

run("node --check Pack F-1 validator", ["node", "--check", "tools/pack-f/validate-pack-f-closure-map.cjs"]);
run("Pack F-1 closure-map validation", ["node", "tools/pack-f/validate-pack-f-closure-map.cjs"]);

console.log("");
console.log("=================================================================================================");
console.log("Pack F-1 completed.");
console.log("Audit      : docs/pack-f/PACK_F1_EDGE_AGENT_AUDIT.md");
console.log("Closure map: docs/pack-f/PACK_F_CLOSURE_MAP.md");
console.log("Next       : Pack F-2 / T-066 — OT-safe edge agent one-way push backend.");
console.log("=================================================================================================");