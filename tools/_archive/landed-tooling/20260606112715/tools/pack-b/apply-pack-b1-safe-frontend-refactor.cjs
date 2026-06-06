const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".pack_b_backup", timestamp);

const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const srcRoot = path.join(frontendRoot, "src");
const docsDir = path.join(root, "docs", "pack-b");
const toolsDir = path.join(root, "tools", "pack-b");
const taskClosureDir = path.join(root, "tools", "task-closure");

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(filePath) {
  return fs.existsSync(filePath);
}

function isFile(filePath) {
  return exists(filePath) && fs.statSync(filePath).isFile();
}

function read(filePath) {
  return fs.readFileSync(filePath, "utf8");
}

function write(filePath, content) {
  ensureDir(path.dirname(filePath));
  fs.writeFileSync(filePath, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + rel(filePath));
}

function rel(filePath) {
  return path.relative(root, filePath).split(path.sep).join("/");
}

function backup(filePath) {
  if (!isFile(filePath)) return;
  const target = path.join(backupRoot, path.relative(root, filePath));
  ensureDir(path.dirname(target));
  fs.copyFileSync(filePath, target);
}

function patch(filePath, mutator) {
  if (!isFile(filePath)) return false;
  backup(filePath);
  const before = read(filePath).replace(/\r\n/g, "\n");
  const after = mutator(before);
  if (after !== before) {
    write(filePath, after);
    return true;
  }
  return false;
}

function walk(dir) {
  if (!exists(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(full) : [full];
  });
}

function lineCount(filePath) {
  if (!isFile(filePath)) return 0;
  return read(filePath).replace(/\r\n/g, "\n").split("\n").length;
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
    return { name, ok: true };
  } catch (error) {
    if (optional) {
      console.warn("[WARN] Optional step failed: " + name);
      console.warn(error.message || String(error));
      return { name, ok: false, optional: true, error: error.message || String(error) };
    }
    throw error;
  }
}

function writeJson(filePath, payload) {
  write(filePath, JSON.stringify(payload, null, 2) + "\n");
}

function updatePackageScripts() {
  const packagePath = path.join(frontendRoot, "package.json");
  if (!isFile(packagePath)) return;

  backup(packagePath);
  const pkg = JSON.parse(read(packagePath));
  pkg.scripts = pkg.scripts || {};
  pkg.scripts["pack-b:validate"] = "node ../../tools/pack-b/validate-pack-b-p05-closure.cjs";
  pkg.scripts["pack-b:brand-tokens"] = "node ../../tools/pack-b/validate-raw-brand-tokens.cjs";
  pkg.scripts["pack-b:split-plan"] = "node ../../tools/pack-b/analyze-pack-b-split-plan.cjs";
  write(packagePath, JSON.stringify(pkg, null, 2) + "\n");
}

function splitLegacyCss() {
  const legacyPath = path.join(srcRoot, "styles", "phase56", "phase56-migrated-legacy.css");
  if (!isFile(legacyPath)) {
    return {
      task: "T-038",
      status: "SKIPPED",
      reason: "phase56-migrated-legacy.css not found."
    };
  }

  const original = read(legacyPath).replace(/\r\n/g, "\n");
  const lines = original.split("\n");

  if (lines.length <= 500) {
    return {
      task: "T-038",
      status: "ALREADY_SMALL",
      file: rel(legacyPath),
      lines: lines.length
    };
  }

  backup(legacyPath);

  const chunkDir = path.join(srcRoot, "styles", "phase56", "legacy-chunks");
  ensureDir(chunkDir);

  for (const old of walk(chunkDir)) {
    if (isFile(old) && old.endsWith(".css")) {
      backup(old);
      fs.unlinkSync(old);
    }
  }

  const chunks = [];
  const maxChunkLines = 360;

  for (let index = 0; index < lines.length; index += maxChunkLines) {
    const chunkIndex = Math.floor(index / maxChunkLines) + 1;
    const chunkName = "legacy-" + String(chunkIndex).padStart(3, "0") + ".css";
    const chunkPath = path.join(chunkDir, chunkName);
    const chunkLines = [
      "/*",
      "  PlantProcess IQ Pack B extracted legacy CSS chunk.",
      "  Source: phase56-migrated-legacy.css",
      "  Generated: " + new Date().toISOString(),
      "*/",
      "",
      ...lines.slice(index, index + maxChunkLines)
    ];

    write(chunkPath, chunkLines.join("\n"));
    chunks.push({
      path: rel(chunkPath),
      lines: chunkLines.length
    });
  }

  const importLines = [
    "/*",
    "  PlantProcess IQ Pack B CSS retirement facade.",
    "  The original 2,900+ line migrated legacy file has been split into smaller token-compatible chunks.",
    "  Keep this file as the single import point until the final CSS-module migration removes the facade.",
    "*/",
    ""
  ];

  for (const chunk of chunks) {
    const base = path.basename(chunk.path);
    importLines.push("@import \"./legacy-chunks/" + base + "\";");
  }

  write(legacyPath, importLines.join("\n") + "\n");

  return {
    task: "T-038",
    status: "APPLIED",
    facade: rel(legacyPath),
    facadeLines: lineCount(legacyPath),
    chunks
  };
}

function normalizeBrandHex() {
  const replacements = [
    { from: /#050B18/gi, to: "var(--ppiq-color-bg-deep)" },
    { from: /#00D4FF/gi, to: "var(--ppiq-color-accent-cyan)" }
  ];

  const allow = [
    "/design-system/phase56/phase56Tokens.ts",
    "/styles/phase56/phase56-tokens.css",
    "/docs/",
    "/public/",
    "/brand/"
  ];

  const targets = walk(srcRoot).filter((file) => {
    if (!isFile(file)) return false;
    const r = rel(file).replace(/\\/g, "/");
    if (allow.some((allowed) => r.includes(allowed))) return false;
    return /\.(ts|tsx|css)$/i.test(file);
  });

  const changed = [];

  for (const file of targets) {
    let before = read(file).replace(/\r\n/g, "\n");
    let after = before;

    for (const replacement of replacements) {
      after = after.replace(replacement.from, replacement.to);
    }

    if (after !== before) {
      backup(file);
      write(file, after);
      changed.push(rel(file));
    }
  }

  return {
    task: "T-039",
    status: changed.length ? "APPLIED" : "NO_RAW_CORE_BRAND_HEX_FOUND",
    changed
  };
}

function writeBrandTokenValidator() {
  const validatorPath = path.join(toolsDir, "validate-raw-brand-tokens.cjs");
  const content = `const fs = require("fs");
const path = require("path");

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const srcRoot = path.join(frontendRoot, "src");

const forbidden = ["#050B18", "#00D4FF"];
const allowFragments = [
  "/design-system/phase56/phase56Tokens.ts",
  "/styles/phase56/phase56-tokens.css",
  "/docs/",
  "/public/",
  "/brand/"
];

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function rel(file) { return path.relative(root, file).split(path.sep).join("/"); }
function walk(dir) {
  if (!exists(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(full) : [full];
  });
}

const findings = [];

for (const file of walk(srcRoot)) {
  if (!isFile(file)) continue;
  if (!/\\.(ts|tsx|css)$/i.test(file)) continue;

  const relative = rel(file);
  const normalized = relative.replace(/\\\\/g, "/");

  if (allowFragments.some((fragment) => normalized.includes(fragment))) continue;

  const text = fs.readFileSync(file, "utf8");
  for (const color of forbidden) {
    if (text.toLowerCase().includes(color.toLowerCase())) {
      findings.push({ file: relative, color });
    }
  }
}

if (findings.length) {
  console.error("Raw core brand hex values found outside token modules:");
  console.error(JSON.stringify(findings, null, 2));
  process.exit(1);
}

console.log("Pack B brand-token raw-hex gate passed.");
`;
  write(validatorPath, content);
}

function writeSplitPlanAnalyzer() {
  const analyzerPath = path.join(toolsDir, "analyze-pack-b-split-plan.cjs");

  const content = `const fs = require("fs");
const path = require("path");

const root = process.cwd();
const docsDir = path.join(root, "docs", "pack-b");

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function read(file) { return fs.readFileSync(file, "utf8"); }
function ensureDir(dir) { fs.mkdirSync(dir, { recursive: true }); }
function rel(file) { return path.relative(root, file).split(path.sep).join("/"); }
function lineCount(file) { return isFile(file) ? read(file).replace(/\\r\\n/g, "\\n").split("\\n").length : 0; }

function findTopLevelDeclarationStarts(lines) {
  const starts = [];
  const regex = /^\\s*(export\\s+)?(default\\s+)?(function|const|let|var|class|interface|type|enum)\\s+([A-Za-z0-9_]+)/;

  for (let i = 0; i < lines.length; i += 1) {
    const match = lines[i].match(regex);
    if (match) {
      starts.push({
        line: i + 1,
        kind: match[3],
        name: match[4]
      });
    }
  }

  return starts;
}

const targets = [
  {
    path: "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizard.implementation.tsx",
    max: 400,
    task: "T-036"
  },
  {
    path: "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizardContent.implementation.tsx",
    max: 400,
    task: "T-036"
  },
  {
    path: "Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts",
    max: 450,
    task: "T-037"
  },
  {
    path: "Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.implementation.tsx",
    max: 450,
    task: "T-037"
  },
  {
    path: "Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx",
    max: 450,
    task: "T-037"
  }
];

const report = [];

for (const target of targets) {
  const absolute = path.join(root, target.path);
  if (!isFile(absolute)) {
    report.push({
      ...target,
      exists: false,
      currentLines: 0,
      status: "MISSING"
    });
    continue;
  }

  const text = read(absolute).replace(/\\r\\n/g, "\\n");
  const lines = text.split("\\n");
  const declarations = findTopLevelDeclarationStarts(lines);

  report.push({
    ...target,
    exists: true,
    currentLines: lines.length,
    status: lines.length <= target.max ? "OK" : "NEEDS_SPLIT",
    topLevelDeclarations: declarations
  });
}

ensureDir(docsDir);
fs.writeFileSync(
  path.join(docsDir, "PACK_B_SPLIT_PLAN.json"),
  JSON.stringify({ generatedAtUtc: new Date().toISOString(), targets: report }, null, 2) + "\\n",
  "utf8"
);

const md = [];
md.push("# Pack B Split Plan");
md.push("");
md.push("Generated: " + new Date().toISOString());
md.push("");
md.push("| Task | File | Lines | Limit | Status |");
md.push("|---|---|---:|---:|---|");

for (const item of report) {
  md.push("| " + item.task + " | \`" + item.path + "\` | " + item.currentLines + " | " + item.max + " | **" + item.status + "** |");
}

md.push("");
md.push("## Top-level declaration hints");
md.push("");

for (const item of report) {
  md.push("### " + item.path);
  md.push("");
  if (!item.exists) {
    md.push("- File missing.");
    md.push("");
    continue;
  }

  if (!item.topLevelDeclarations.length) {
    md.push("- No simple top-level declarations detected by the lightweight scanner.");
    md.push("");
    continue;
  }

  for (const declaration of item.topLevelDeclarations.slice(0, 80)) {
    md.push("- Line " + declaration.line + ": " + declaration.kind + " " + declaration.name);
  }

  md.push("");
}

fs.writeFileSync(path.join(docsDir, "PACK_B_SPLIT_PLAN.md"), md.join("\\n") + "\\n", "utf8");
console.log("Pack B split plan written:");
console.log(" - docs/pack-b/PACK_B_SPLIT_PLAN.md");
console.log(" - docs/pack-b/PACK_B_SPLIT_PLAN.json");
`;
  write(analyzerPath, content);
}

function writeClosureValidator() {
  const validatorPath = path.join(toolsDir, "validate-pack-b-p05-closure.cjs");

  const content = `const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function read(file) { return fs.readFileSync(file, "utf8"); }
function lines(file) { return isFile(file) ? read(file).replace(/\\r\\n/g, "\\n").split("\\n").length : 0; }

function command(name, args) {
  console.log("");
  console.log("---- " + name);
  cp.execFileSync(args[0], args.slice(1), { cwd: root, stdio: "inherit", shell: false });
}

const limits = [
  {
    task: "T-036",
    path: "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizard.implementation.tsx",
    max: 400
  },
  {
    task: "T-036",
    path: "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizardContent.implementation.tsx",
    max: 400
  },
  {
    task: "T-037",
    path: "Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts",
    max: 450
  },
  {
    task: "T-037",
    path: "Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.implementation.tsx",
    max: 450
  },
  {
    task: "T-037",
    path: "Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx",
    max: 450
  },
  {
    task: "T-038",
    path: "Frontend/PlantProcess.Web/src/styles/phase56/phase56-migrated-legacy.css",
    max: 120
  }
];

const failures = [];

for (const limit of limits) {
  const absolute = path.join(root, limit.path);
  const count = lines(absolute);

  if (!isFile(absolute)) {
    failures.push({ ...limit, actual: 0, reason: "missing" });
    continue;
  }

  if (count > limit.max) {
    failures.push({ ...limit, actual: count, reason: "too-large" });
  }
}

const chunkDir = path.join(root, "Frontend/PlantProcess.Web/src/styles/phase56/legacy-chunks");
if (exists(chunkDir)) {
  for (const entry of fs.readdirSync(chunkDir)) {
    if (!entry.endsWith(".css")) continue;
    const file = path.join(chunkDir, entry);
    const count = lines(file);
    if (count > 450) {
      failures.push({
        task: "T-038",
        path: path.relative(root, file).split(path.sep).join("/"),
        max: 450,
        actual: count,
        reason: "legacy-css-chunk-too-large"
      });
    }
  }
}

command("Pack B brand-token gate", ["node", "tools/pack-b/validate-raw-brand-tokens.cjs"]);

if (!isFile(path.join(root, "tools/task-closure/Invoke-Frontend-Regression.ps1"))) {
  failures.push({
    task: "T-040",
    path: "tools/task-closure/Invoke-Frontend-Regression.ps1",
    max: 1,
    actual: 0,
    reason: "missing regression wrapper"
  });
}

if (failures.length) {
  console.error("");
  console.error("Pack B P05 closure gate failed. Remaining non-DONE evidence:");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack B P05 closure gate passed.");
`;
  write(validatorPath, content);
}

function writeRegressionWrapper() {
  ensureDir(taskClosureDir);

  const wrapperPath = path.join(taskClosureDir, "Invoke-Frontend-Regression.ps1");
  const wrapper = `[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunE2E,
    [switch]$RunVisual
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
    Push-Location ".\\Frontend\\PlantProcess.Web"
    try {
        Run-Step "npm run build" {
            npm run build
        }

        $packageJson = Get-Content ".\\package.json" -Raw | ConvertFrom-Json

        if ($packageJson.scripts.PSObject.Properties.Name -contains "test") {
            Run-Step "npm run test" {
                npm run test
            }
        } else {
            Write-Host "No npm test script found. Build remains the mandatory baseline." -ForegroundColor Yellow
        }

        if ($RunE2E) {
            if ($packageJson.scripts.PSObject.Properties.Name -contains "e2e") {
                Run-Step "npm run e2e" {
                    npm run e2e
                }
            } else {
                Write-Host "No npm e2e script found. Skipping E2E." -ForegroundColor Yellow
            }
        }

        if ($RunVisual) {
            if ($packageJson.scripts.PSObject.Properties.Name -contains "test:visual:phase56") {
                Run-Step "npm run test:visual:phase56" {
                    npm run test:visual:phase56
                }
            } else {
                Write-Host "No visual phase56 script found. Skipping visual run." -ForegroundColor Yellow
            }
        }
    }
    finally {
        Pop-Location
    }

    Write-Host ""
    Write-Host "Frontend regression wrapper completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
`;
  write(wrapperPath, wrapper);

  const p05GatePath = path.join(taskClosureDir, "validate-p05-no-tracked-warnings.cjs");
  const p05Gate = `const cp = require("child_process");
cp.execFileSync("node", ["tools/pack-b/validate-pack-b-p05-closure.cjs"], {
  cwd: process.cwd(),
  stdio: "inherit",
  shell: false
});
`;
  write(p05GatePath, p05Gate);
}

function writePackBEvidence(results) {
  const evidencePath = path.join(docsDir, "PACK_B_IMPLEMENTATION_EVIDENCE.md");

  const md = [];
  md.push("# PlantProcess IQ Pack B — Frontend Refactor Closure Evidence");
  md.push("");
  md.push("Generated: " + new Date().toISOString());
  md.push("");
  md.push("## Scope");
  md.push("");
  md.push("- T-036: Split WidgetBuilderWizard god-files.");
  md.push("- T-037: Refactor product core API client and large admin/material pages.");
  md.push("- T-038: Retire/split migrated legacy CSS.");
  md.push("- T-039: Centralize raw brand tokens.");
  md.push("- T-040: Frontend regression wrapper.");
  md.push("- T-041: Frontend file-size and no-tracked-warning gate.");
  md.push("");
  md.push("## Automatic actions applied");
  md.push("");

  for (const result of results) {
    md.push("- `" + result.task + "` — " + result.status + (result.reason ? " — " + result.reason : ""));
  }

  md.push("");
  md.push("## Important boundary");
  md.push("");
  md.push("This pack does not fake semantic React decomposition. If T-036 or T-037 files remain above their limits, the closure gate fails and the generated split plan must be used for the next targeted component split.");
  md.push("");
  md.push("## Generated artifacts");
  md.push("");
  md.push("- `docs/pack-b/PACK_B_SPLIT_PLAN.md`");
  md.push("- `docs/pack-b/PACK_B_SPLIT_PLAN.json`");
  md.push("- `tools/pack-b/validate-pack-b-p05-closure.cjs`");
  md.push("- `tools/pack-b/validate-raw-brand-tokens.cjs`");
  md.push("- `tools/task-closure/Invoke-Frontend-Regression.ps1`");
  md.push("- `tools/task-closure/validate-p05-no-tracked-warnings.cjs`");
  md.push("");

  write(evidencePath, md.join("\n") + "\n");
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack B-1 — Safe Frontend Refactor Closure Pack");
console.log("=================================================================================================");
console.log("Project root : " + root);
console.log("Backup folder: " + backupRoot);

ensureDir(backupRoot);
ensureDir(docsDir);
ensureDir(toolsDir);
ensureDir(taskClosureDir);

const results = [];
results.push(splitLegacyCss());
results.push(normalizeBrandHex());

writeBrandTokenValidator();
writeSplitPlanAnalyzer();
writeClosureValidator();
writeRegressionWrapper();
updatePackageScripts();

run("node --check brand-token validator", ["node", "--check", "tools/pack-b/validate-raw-brand-tokens.cjs"]);
run("node --check split-plan analyzer", ["node", "--check", "tools/pack-b/analyze-pack-b-split-plan.cjs"]);
run("node --check Pack B closure validator", ["node", "--check", "tools/pack-b/validate-pack-b-p05-closure.cjs"]);

run("Generate Pack B split plan", ["node", "tools/pack-b/analyze-pack-b-split-plan.cjs"]);
writePackBEvidence(results);

run("Pack B brand-token gate", ["node", "tools/pack-b/validate-raw-brand-tokens.cjs"]);

run("Frontend npm build", ["npm", "run", "build"], frontendRoot);

if (isFile(path.join(root, "tools", "phase56", "validate-phase56.cjs"))) {
  run("Phase 5/6 regression validation", ["node", "tools/phase56/validate-phase56.cjs"]);
}

const closure = run(
  "Pack B P05 closure gate",
  ["node", "tools/pack-b/validate-pack-b-p05-closure.cjs"],
  root,
  true
);

console.log("");
console.log("=================================================================================================");
if (closure.ok) {
  console.log("Pack B-1 completed and Pack B closure gate is GREEN.");
} else {
  console.log("Pack B-1 completed, but Pack B closure gate is still RED.");
  console.log("This is expected if T-036/T-037 giant TSX files are not yet semantically split.");
  console.log("Open docs/pack-b/PACK_B_SPLIT_PLAN.md for the exact next split targets.");
}
console.log("Evidence : docs/pack-b/PACK_B_IMPLEMENTATION_EVIDENCE.md");
console.log("Plan     : docs/pack-b/PACK_B_SPLIT_PLAN.md");
console.log("Backup   : " + backupRoot);
console.log("=================================================================================================");