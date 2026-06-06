const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

const docsDir = path.join(root, "docs", "pack-e");
const toolsDir = path.join(root, "tools", "pack-e");

const auditJsonPath = path.join(docsDir, "PACK_E1_HISTORIAN_AUDIT.json");
const auditMdPath = path.join(docsDir, "PACK_E1_HISTORIAN_AUDIT.md");
const closureJsonPath = path.join(docsDir, "PACK_E_CLOSURE_MAP.json");
const closureMdPath = path.join(docsDir, "PACK_E_CLOSURE_MAP.md");
const evidencePath = path.join(docsDir, "PACK_E_IMPLEMENTATION_EVIDENCE.md");
const validatorPath = path.join(toolsDir, "validate-pack-e-closure-map.cjs");
const regressionWrapperPath = path.join(toolsDir, "Invoke-PackE-Regression.ps1");

const packETasks = [
  {
    task: "T-060",
    pack: "E",
    title: "Implement one GA historian connector",
    currentScore: 75,
    status: "MOSTLY DONE",
    closeIn: "Pack E-2",
    objective:
      "Promote one historian connector to GA-level behavior with provider registration, typed configuration, connection test, browse/read API, safe error handling, and source-to-mapping compatibility."
  },
  {
    task: "T-063",
    pack: "E",
    title: "Historian connector UI register/test/map",
    currentScore: 0,
    status: "NOT YET STARTED",
    closeIn: "Pack E-3",
    objective:
      "Expose historian registration, connection test, tag/point browsing, and mapping flow in the UI without overclaiming full vendor support."
  },
  {
    task: "T-064",
    pack: "E",
    title: "Historian tests docs regression",
    currentScore: 60,
    status: "PARTIALLY DONE",
    closeIn: "Pack E-4",
    objective:
      "Add backend/frontend regression coverage, documentation, validation scripts, and task-closure bridge for the historian connector pack."
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
    ".css"
  ]);

  return walk(root).filter((file) => allowed.has(path.extname(file).toLowerCase()));
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

function classifyHistorianBackend(files) {
  const backendFiles = files.filter((file) => rel(file).startsWith("Backend/"));

  return {
    allHistorianSignals: searchFiles([/historian/i, /opc/i, /pi\s*web\s*api/i, /influx/i, /timeseries/i, /time-series/i, /tag\s*browser/i, /point\s*browser/i], backendFiles),
    connectorSignals: searchFiles([/Historian.*Connector/i, /IHistorian/i, /ConnectorProvider/i, /ProviderType/i, /ConnectionTest/i, /Browse/i, /Read/i], backendFiles),
    apiSignals: searchFiles([/Map.*Historian/i, /historian.*endpoint/i, /provider-types/i, /connector.*test/i, /connector.*browse/i, /connector.*map/i], backendFiles),
    testSignals: searchFiles([/Historian/i, /Connector/i, /ProviderType/i, /ConnectionTest/i], backendFiles.filter((file) => rel(file).includes("/tests/")))
  };
}

function classifyHistorianFrontend(files) {
  const frontendFiles = files.filter((file) => rel(file).startsWith("Frontend/"));

  return {
    allHistorianSignals: searchFiles([/historian/i, /opc/i, /pi\s*web\s*api/i, /tag\s*browser/i, /point\s*browser/i, /time-series/i, /timeseries/i], frontendFiles),
    pageSignals: searchFiles([/Connector/i, /Historian/i, /Register/i, /Test/i, /Map/i, /Browse/i], frontendFiles),
    apiClientSignals: searchFiles([/provider-types/i, /connector.*test/i, /connector.*browse/i, /historian/i], frontendFiles.filter((file) => file.endsWith(".ts") || file.endsWith(".tsx")))
  };
}

function classifyDocs(files) {
  const docFiles = files.filter((file) => {
    const relative = rel(file).toLowerCase();
    return relative.startsWith("docs/") || relative.endsWith(".md");
  });

  return {
    historianDocs: searchFiles([/historian/i, /opc/i, /pi\s*web\s*api/i, /tag/i, /time-series/i, /connector/i], docFiles),
    honestyDocs: searchFiles([/not.*mes/i, /not.*scada/i, /not.*l2/i, /connector honesty/i, /overclaim/i, /vendor/i], docFiles)
  };
}

function readScorecard() {
  const scorecardPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.json");

  if (!isFile(scorecardPath)) {
    return {
      exists: false,
      path: rel(scorecardPath),
      packE: []
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
      packE: rows.filter((row) => {
        const task = String(row.task || row.taskCode || row.id || row.code || "");
        const pack = String(row.pack || row.phase || row.group || "");
        return ["T-060", "T-063", "T-064"].includes(task) || pack === "E";
      })
    };
  } catch (error) {
    return {
      exists: true,
      path: rel(scorecardPath),
      parseError: error.message,
      packE: []
    };
  }
}

function buildClosureMap(audit) {
  const closureMap = [
    {
      task: "T-060",
      packStep: "Pack E-2",
      title: "GA historian connector backend",
      priority: 1,
      risk: "MEDIUM",
      reason:
        "Backend connector behavior must exist before UI and tests can be meaningful.",
      acceptance: [
        "One canonical GA historian provider is declared honestly",
        "Provider appears in connector provider-types endpoint",
        "Backend supports typed historian connection test",
        "Backend supports safe tag/point browse or equivalent metadata browse",
        "Backend supports bounded read/sample flow",
        "Historian source can connect to schema/mapping workflow",
        "Errors are explicit and demo-safe, not fake success",
        "Backend build remains green"
      ],
      auditFindings: {
        backendHistorianSignals: audit.backend.allHistorianSignals.length,
        connectorSignals: audit.backend.connectorSignals.length,
        apiSignals: audit.backend.apiSignals.length
      }
    },
    {
      task: "T-063",
      packStep: "Pack E-3",
      title: "Historian connector UI register/test/map",
      priority: 2,
      risk: "MEDIUM",
      reason:
        "UI should only expose what the backend supports and must avoid vendor-overclaim.",
      acceptance: [
        "Connector UI includes historian provider option",
        "UI can create/register historian source configuration",
        "UI can run connection test and display honest result",
        "UI can browse historian tags/points or show supported metadata result",
        "UI can send selected tags/points into mapping/schema workflow",
        "UI copy clearly states supported demo/GA connector scope",
        "Frontend build remains green"
      ],
      auditFindings: {
        frontendHistorianSignals: audit.frontend.allHistorianSignals.length,
        pageSignals: audit.frontend.pageSignals.length,
        apiClientSignals: audit.frontend.apiClientSignals.length
      }
    },
    {
      task: "T-064",
      packStep: "Pack E-4",
      title: "Historian tests, docs, regression, scorecard bridge",
      priority: 3,
      risk: "LOW",
      reason:
        "After backend and UI are present, lock behavior through tests/docs/regression.",
      acceptance: [
        "Backend tests cover provider registration and failure-safe connection test",
        "Frontend/build smoke covers historian UI surface",
        "Docs explain supported historian connector scope",
        "Docs explain fake/demo vs real connector boundaries",
        "Pack E validator exists",
        "Pack E regression wrapper exists",
        "Task closure bridge marks T-060/T-063/T-064 green only after validators pass"
      ],
      auditFindings: {
        backendTestSignals: audit.backend.testSignals.length,
        historianDocs: audit.docs.historianDocs.length,
        honestyDocs: audit.docs.honestyDocs.length
      }
    }
  ];

  return {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_E1_HISTORIAN_CLOSURE_MAP",
    recommendedOrder: closureMap.map((item) => item.task),
    closureMap
  };
}

function writeAuditMarkdown(audit, closureMap) {
  const md = [];

  md.push("# Pack E-1 Historian Connector Audit + Closure Map");
  md.push("");
  md.push("Generated: " + audit.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_E1_HISTORIAN_AUDIT_CLOSURE_MAP");
  md.push("");
  md.push("## Executive Summary");
  md.push("");
  md.push("Pack E has three remaining tasks: backend GA historian connector, UI register/test/map flow, and tests/docs/regression closure. The safe execution order is backend first, UI second, tests/docs/scorecard closure third.");
  md.push("");
  md.push("## Remaining Pack E Tasks");
  md.push("");
  md.push("| Task | Score | Status | Recommended Step | Objective |");
  md.push("|---|---:|---|---|---|");

  for (const task of packETasks) {
    md.push("| " + task.task + " | " + task.currentScore + "% | " + task.status + " | " + task.closeIn + " | " + task.objective + " |");
  }

  md.push("");
  md.push("## Backend Historian Signals");
  md.push("");
  md.push("- Historian-related backend files: **" + audit.backend.allHistorianSignals.length + "**");
  md.push("- Connector-specific backend files: **" + audit.backend.connectorSignals.length + "**");
  md.push("- API/backend endpoint signals: **" + audit.backend.apiSignals.length + "**");
  md.push("- Backend test signals: **" + audit.backend.testSignals.length + "**");
  md.push("");
  md.push("### Backend connector candidates");
  md.push("");
  md.push("| File | Lines |");
  md.push("|---|---:|");
  for (const item of audit.backend.connectorSignals.slice(0, 80)) {
    md.push("| `" + item.path + "` | " + item.lines + " |");
  }

  md.push("");
  md.push("## Frontend Historian Signals");
  md.push("");
  md.push("- Historian-related frontend files: **" + audit.frontend.allHistorianSignals.length + "**");
  md.push("- UI page/component signals: **" + audit.frontend.pageSignals.length + "**");
  md.push("- API client signals: **" + audit.frontend.apiClientSignals.length + "**");
  md.push("");
  md.push("### Frontend candidates");
  md.push("");
  md.push("| File | Lines |");
  md.push("|---|---:|");
  for (const item of audit.frontend.pageSignals.slice(0, 80)) {
    md.push("| `" + item.path + "` | " + item.lines + " |");
  }

  md.push("");
  md.push("## Docs/Test Signals");
  md.push("");
  md.push("- Historian docs: **" + audit.docs.historianDocs.length + "**");
  md.push("- Connector honesty/vendor-scope docs: **" + audit.docs.honestyDocs.length + "**");
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
  md.push("Next implementation step: **Pack E-2 / T-060 — GA historian connector backend**.");
  md.push("");

  write(auditMdPath, md.join("\n"));
}

function writeClosureMarkdown(closureMap) {
  const md = [];

  md.push("# Pack E Closure Map");
  md.push("");
  md.push("Generated: " + closureMap.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_E1_HISTORIAN_CLOSURE_MAP");
  md.push("");
  md.push("## Recommended Execution Order");
  md.push("");
  md.push("1. Pack E-2 / T-060 — GA historian connector backend.");
  md.push("2. Pack E-3 / T-063 — Historian connector UI register/test/map.");
  md.push("3. Pack E-4 / T-064 — Historian tests/docs/regression and scorecard bridge.");
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
  lines.push('  "docs/pack-e/PACK_E1_HISTORIAN_AUDIT.json",');
  lines.push('  "docs/pack-e/PACK_E1_HISTORIAN_AUDIT.md",');
  lines.push('  "docs/pack-e/PACK_E_CLOSURE_MAP.json",');
  lines.push('  "docs/pack-e/PACK_E_CLOSURE_MAP.md",');
  lines.push('  "docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md"');
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
  lines.push('if (isFile("docs/pack-e/PACK_E_CLOSURE_MAP.json")) {');
  lines.push('  const map = JSON.parse(read("docs/pack-e/PACK_E_CLOSURE_MAP.json"));');
  lines.push('  const tasks = new Set((map.closureMap || []).map((item) => item.task));');
  lines.push('  for (const task of ["T-060", "T-063", "T-064"]) {');
  lines.push('    if (!tasks.has(task)) failures.push({ task, reason: "missing-from-closure-map" });');
  lines.push('  }');
  lines.push('}');
  lines.push('');
  lines.push('if (isFile("docs/pack-e/PACK_E1_HISTORIAN_AUDIT.json")) {');
  lines.push('  const audit = JSON.parse(read("docs/pack-e/PACK_E1_HISTORIAN_AUDIT.json"));');
  lines.push('  for (const section of ["backend", "frontend", "docs", "scorecard"]) {');
  lines.push('    if (!audit[section]) failures.push({ section, reason: "missing-audit-section" });');
  lines.push('  }');
  lines.push('}');
  lines.push('');
  lines.push('if (isFile("docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md")) {');
  lines.push('  const evidence = read("docs/pack-e/PACK_E_IMPLEMENTATION_EVIDENCE.md");');
  lines.push('  if (!evidence.includes("PPIQ_PACK_E1_HISTORIAN_AUDIT_CLOSURE_MAP")) failures.push({ reason: "evidence-marker-missing" });');
  lines.push('}');
  lines.push('');
  lines.push('if (failures.length) {');
  lines.push('  console.error("Pack E-1 closure map validation failed.");');
  lines.push('  console.error(JSON.stringify(failures, null, 2));');
  lines.push('  process.exit(1);');
  lines.push('}');
  lines.push('');
  lines.push('console.log("Pack E-1 historian closure map validation passed.");');

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
    "    Run-Step \"Pack E-1 closure map validation\" {",
    "        node \".\\tools\\pack-e\\validate-pack-e-closure-map.cjs\"",
    "    }",
    "",
    "    if ($RunBuilds) {",
    "        Run-Step \"Backend build\" { dotnet build \".\\Backend\" }",
    "        Push-Location \".\\Frontend\\PlantProcess.Web\"",
    "        try { Run-Step \"Frontend build\" { npm run build } }",
    "        finally { Pop-Location }",
    "    }",
    "",
    "    Write-Host \"\"",
    "    Write-Host \"Pack E regression wrapper completed.\" -ForegroundColor Green",
    "}",
    "finally { Pop-Location }",
    ""
  ].join("\n");

  write(regressionWrapperPath, content);
}

function writeEvidence() {
  let evidence = isFile(evidencePath)
    ? normalize(read(evidencePath))
    : "# PlantProcess IQ Pack E Evidence\n";

  if (!evidence.includes("PPIQ_PACK_E1_HISTORIAN_AUDIT_CLOSURE_MAP")) {
    evidence += [
      "",
      "## Pack E-1 Historian audit and closure map",
      "",
      "- Marker: PPIQ_PACK_E1_HISTORIAN_AUDIT_CLOSURE_MAP.",
      "- Audited remaining Pack E tasks: T-060, T-063, T-064.",
      "- Created closure map with recommended order: backend connector, UI flow, tests/docs/regression.",
      "- Added Pack E closure-map validator.",
      "- Added Pack E regression wrapper.",
      "",
      "Generated artifacts:",
      "",
      "- docs/pack-e/PACK_E1_HISTORIAN_AUDIT.md",
      "- docs/pack-e/PACK_E1_HISTORIAN_AUDIT.json",
      "- docs/pack-e/PACK_E_CLOSURE_MAP.md",
      "- docs/pack-e/PACK_E_CLOSURE_MAP.json",
      "- tools/pack-e/validate-pack-e-closure-map.cjs",
      "- tools/pack-e/Invoke-PackE-Regression.ps1",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack E-1 — Historian connector audit + closure map");
console.log("=================================================================================================");

ensureDir(docsDir);
ensureDir(toolsDir);

const files = getTextFiles();

const audit = {
  generatedAtUtc: new Date().toISOString(),
  marker: "PPIQ_PACK_E1_HISTORIAN_AUDIT_CLOSURE_MAP",
  tasks: packETasks,
  scorecard: readScorecard(),
  backend: classifyHistorianBackend(files),
  frontend: classifyHistorianFrontend(files),
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
console.log("Pack E-1 audit summary:");
console.log(" - Backend historian signals      : " + audit.backend.allHistorianSignals.length);
console.log(" - Backend connector signals      : " + audit.backend.connectorSignals.length);
console.log(" - Backend API signals            : " + audit.backend.apiSignals.length);
console.log(" - Backend test signals           : " + audit.backend.testSignals.length);
console.log(" - Frontend historian signals     : " + audit.frontend.allHistorianSignals.length);
console.log(" - Frontend page/API signals      : " + (audit.frontend.pageSignals.length + audit.frontend.apiClientSignals.length));
console.log(" - Historian docs                 : " + audit.docs.historianDocs.length);
console.log(" - Connector honesty docs         : " + audit.docs.honestyDocs.length);

console.log("");
console.log("Recommended Pack E closure order:");
for (const item of closureMap.closureMap) {
  console.log(" - " + item.packStep + " / " + item.task + " / " + item.title);
}

run("node --check Pack E-1 validator", ["node", "--check", "tools/pack-e/validate-pack-e-closure-map.cjs"]);
run("Pack E-1 closure-map validation", ["node", "tools/pack-e/validate-pack-e-closure-map.cjs"]);

console.log("");
console.log("=================================================================================================");
console.log("Pack E-1 completed.");
console.log("Audit      : docs/pack-e/PACK_E1_HISTORIAN_AUDIT.md");
console.log("Closure map: docs/pack-e/PACK_E_CLOSURE_MAP.md");
console.log("Next       : Pack E-2 / T-060 — GA historian connector backend.");
console.log("=================================================================================================");