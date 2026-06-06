const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const backendRoot = path.join(root, "Backend");
const docsDir = path.join(root, "docs", "pack-d");
const toolsDir = path.join(root, "tools", "pack-d");

const auditJsonPath = path.join(docsDir, "PACK_D1_BACKEND_SPLIT_AUDIT.json");
const auditMdPath = path.join(docsDir, "PACK_D1_BACKEND_SPLIT_AUDIT.md");
const routeJsonPath = path.join(docsDir, "PACK_D1_ROUTE_CONTRACT_SNAPSHOT.json");
const routeMdPath = path.join(docsDir, "PACK_D1_ROUTE_CONTRACT_SNAPSHOT.md");
const serviceJsonPath = path.join(docsDir, "PACK_D1_SERVICE_SURFACE_SNAPSHOT.json");
const evidencePath = path.join(docsDir, "PACK_D_IMPLEMENTATION_EVIDENCE.md");
const validatorPath = path.join(toolsDir, "validate-pack-d-route-contract-snapshot.cjs");
const lineGatePath = path.join(toolsDir, "validate-pack-d-backend-thinness.cjs");
const wrapperPath = path.join(toolsDir, "Invoke-PackD-BackendRegression.ps1");

const targetFiles = [
  {
    task: "T-054",
    group: "Schema mapping endpoints",
    role: "Primary backend god-file blocker",
    maxLines: 500,
    path: "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs"
  },
  {
    task: "T-054",
    group: "Workflow truth endpoints",
    role: "Primary backend god-file blocker",
    maxLines: 500,
    path: "Backend/PlantProcess.Api/Endpoints/Workflow/Phase1WorkflowTruthEndpoints.cs"
  },
  {
    task: "T-055",
    group: "Workflow endpoints",
    role: "Primary backend god-file blocker",
    maxLines: 500,
    path: "Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs"
  },
  {
    task: "T-055",
    group: "Connector configuration service",
    role: "Primary backend god-service blocker",
    maxLines: 500,
    path: "Backend/PlantProcess.Infrastructure/Configuration/ConnectorConfigurationService.cs"
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
      if (["bin", "obj", ".git", ".vs"].includes(entry.name)) return [];
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

function getCsFiles() {
  return walk(backendRoot).filter((file) => file.endsWith(".cs"));
}

function extractClassNames(text) {
  const results = [];
  const regex = /\b(public|internal|private|protected)?\s*(static\s+)?(partial\s+)?(sealed\s+|abstract\s+)?class\s+([A-Za-z0-9_]+)/g;
  let match;

  while ((match = regex.exec(text)) !== null) {
    results.push(match[5]);
  }

  return results;
}

function extractPublicMethods(text) {
  const results = [];
  const lines = normalize(text).split("\n");

  const regex =
    /^\s*(public|internal)\s+(static\s+)?(async\s+)?([A-Za-z0-9_<>,\.\?\[\]\s]+)\s+([A-Za-z0-9_]+)\s*\(/;

  for (let i = 0; i < lines.length; i += 1) {
    const match = lines[i].match(regex);
    if (!match) continue;

    results.push({
      line: i + 1,
      visibility: match[1],
      isStatic: Boolean(match[2]),
      isAsync: Boolean(match[3]),
      returnType: match[4].trim().replace(/\s+/g, " "),
      name: match[5],
      signatureLine: lines[i].trim()
    });
  }

  return results;
}

function extractRegions(text) {
  const results = [];
  const regex = /^\s*#region\s+(.+)$/gm;
  let match;

  while ((match = regex.exec(text)) !== null) {
    results.push(match[1].trim());
  }

  return results;
}

function extractRouteContractsFromFile(file) {
  const text = normalize(read(file));
  const lines = text.split("\n");
  const routes = [];

  const routeRegex =
    /\.(MapGet|MapPost|MapPut|MapDelete|MapPatch|MapMethods)\s*\(\s*(?:[A-Za-z0-9_]+\s*,\s*)?(@?["'])([^"']+)\2/g;

  let match;

  while ((match = routeRegex.exec(text)) !== null) {
    const before = text.slice(0, match.index);
    const line = before.split("\n").length;
    const context = lines.slice(Math.max(0, line - 4), Math.min(lines.length, line + 8)).join("\n");

    const withName = context.match(/\.WithName\s*\(\s*(@?["'])([^"']+)\1\s*\)/);
    const tag = context.match(/\.WithTags\s*\(\s*(@?["'])([^"']+)\1\s*\)/);
    const group = context.match(/\bMapGroup\s*\(\s*(@?["'])([^"']+)\1\s*\)/);

    routes.push({
      file: rel(file),
      line,
      method: match[1].replace("Map", "").toUpperCase(),
      route: match[3],
      withName: withName ? withName[2] : null,
      tag: tag ? tag[2] : null,
      nearestGroup: group ? group[2] : null
    });
  }

  return routes;
}

function extractMapGroupContractsFromFile(file) {
  const text = normalize(read(file));
  const lines = text.split("\n");
  const groups = [];
  const regex = /\bMapGroup\s*\(\s*(@?["'])([^"']+)\1\s*\)/g;
  let match;

  while ((match = regex.exec(text)) !== null) {
    const before = text.slice(0, match.index);
    const line = before.split("\n").length;
    groups.push({
      file: rel(file),
      line,
      groupRoute: match[2],
      sourceLine: lines[line - 1].trim()
    });
  }

  return groups;
}

function analyzeTargetFile(target) {
  const absolute = path.join(root, target.path);
  const existsFlag = isFile(absolute);
  const text = existsFlag ? normalize(read(absolute)) : "";

  return {
    ...target,
    exists: existsFlag,
    lines: existsFlag ? lineCount(absolute) : 0,
    overLimit: existsFlag ? lineCount(absolute) > target.maxLines : true,
    classes: existsFlag ? extractClassNames(text) : [],
    publicMethods: existsFlag ? extractPublicMethods(text) : [],
    regions: existsFlag ? extractRegions(text) : [],
    routes: existsFlag ? extractRouteContractsFromFile(absolute) : [],
    groups: existsFlag ? extractMapGroupContractsFromFile(absolute) : []
  };
}

function findLargeBackendFiles() {
  return getCsFiles()
    .map((file) => ({
      path: rel(file),
      lines: lineCount(file)
    }))
    .filter((item) => item.lines > 500)
    .sort((a, b) => b.lines - a.lines);
}

function buildRouteSnapshot() {
  const routeFiles = getCsFiles().filter((file) => {
    const relative = rel(file);
    return (
      relative.includes("/Endpoints/") ||
      relative.endsWith("/Program.cs") ||
      relative.includes("\\Endpoints\\")
    );
  });

  const routes = [];
  const groups = [];

  for (const file of routeFiles) {
    const text = read(file);
    if (!text.includes("Map")) continue;

    routes.push(...extractRouteContractsFromFile(file));
    groups.push(...extractMapGroupContractsFromFile(file));
  }

  routes.sort((a, b) => {
    const keyA = `${a.method}|${a.nearestGroup || ""}|${a.route}|${a.file}|${a.line}`;
    const keyB = `${b.method}|${b.nearestGroup || ""}|${b.route}|${b.file}|${b.line}`;
    return keyA.localeCompare(keyB);
  });

  groups.sort((a, b) => {
    const keyA = `${a.groupRoute}|${a.file}|${a.line}`;
    const keyB = `${b.groupRoute}|${b.file}|${b.line}`;
    return keyA.localeCompare(keyB);
  });

  return {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_D1_ROUTE_CONTRACT_SNAPSHOT",
    routeCount: routes.length,
    groupCount: groups.length,
    routes,
    groups
  };
}

function buildServiceSurfaceSnapshot() {
  const serviceFiles = getCsFiles().filter((file) => rel(file).includes("/Services/") || rel(file).includes("/Configuration/"));

  const services = serviceFiles
    .map((file) => {
      const text = normalize(read(file));
      const methods = extractPublicMethods(text);
      if (!methods.length) return null;

      return {
        file: rel(file),
        lines: lineCount(file),
        classes: extractClassNames(text),
        publicMethods: methods
      };
    })
    .filter(Boolean)
    .sort((a, b) => b.lines - a.lines);

  return {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_D1_SERVICE_SURFACE_SNAPSHOT",
    serviceCount: services.length,
    services
  };
}

function writeAuditMarkdown(audit) {
  const md = [];

  md.push("# Pack D-1 Backend Split Audit");
  md.push("");
  md.push("Generated: " + audit.generatedAtUtc);
  md.push("");
  md.push("## Purpose");
  md.push("");
  md.push("Pack D-1 is audit-only. It prepares Pack D-2/D-3 by recording current backend god-files, route contracts, and service public method surfaces before any source refactor.");
  md.push("");
  md.push("## Target blocker files");
  md.push("");
  md.push("| Task | File | Lines | Limit | Status | Routes | Public/internal methods |");
  md.push("|---|---|---:|---:|---|---:|---:|");

  for (const item of audit.targets) {
    md.push(
      `| ${item.task} | \`${item.path}\` | ${item.lines} | ${item.maxLines} | ${item.overLimit ? "**SPLIT REQUIRED**" : "OK"} | ${item.routes.length} | ${item.publicMethods.length} |`
    );
  }

  md.push("");
  md.push("## Recommended split order");
  md.push("");
  md.push("1. `GenericSchemaMappingEndpoints.cs` — split by mapping metadata, dictionary, version validation, preview/proof, and route registration.");
  md.push("2. `Phase1WorkflowTruthEndpoints.cs` — split by lifecycle proof, evidence reporting, and route registration.");
  md.push("3. `WorkflowEndpoints.cs` — split by command/query/proof routes.");
  md.push("4. `ConnectorConfigurationService.cs` — split by provider registry, connection testing, schema discovery, persistence, and DTO mapping.");
  md.push("");
  md.push("## Large backend files found");
  md.push("");
  md.push("| File | Lines |");
  md.push("|---|---:|");

  for (const item of audit.largeBackendFiles.slice(0, 50)) {
    md.push(`| \`${item.path}\` | ${item.lines} |`);
  }

  md.push("");
  md.push("## Route-contract guard");
  md.push("");
  md.push("Route snapshot written to:");
  md.push("");
  md.push("- `docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.json`");
  md.push("- `docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.md`");
  md.push("");
  md.push("Before and after each Pack D split, run:");
  md.push("");
  md.push("```powershell");
  md.push("node .\\tools\\pack-d\\validate-pack-d-route-contract-snapshot.cjs");
  md.push("```");
  md.push("");

  write(auditMdPath, md.join("\n"));
}

function writeRouteMarkdown(snapshot) {
  const md = [];

  md.push("# Pack D-1 Route Contract Snapshot");
  md.push("");
  md.push("Generated: " + snapshot.generatedAtUtc);
  md.push("");
  md.push("- Route count: **" + snapshot.routeCount + "**");
  md.push("- Group count: **" + snapshot.groupCount + "**");
  md.push("");
  md.push("## Routes");
  md.push("");
  md.push("| Method | Route | Name | Tag | Group | File | Line |");
  md.push("|---|---|---|---|---|---|---:|");

  for (const route of snapshot.routes) {
    md.push(
      `| ${route.method} | \`${route.route}\` | ${route.withName || ""} | ${route.tag || ""} | ${route.nearestGroup || ""} | \`${route.file}\` | ${route.line} |`
    );
  }

  md.push("");
  md.push("## Groups");
  md.push("");
  md.push("| Group route | File | Line |");
  md.push("|---|---|---:|");

  for (const group of snapshot.groups) {
    md.push(`| \`${group.groupRoute}\` | \`${group.file}\` | ${group.line} |`);
  }

  md.push("");

  write(routeMdPath, md.join("\n"));
}

function writeValidator() {
  const validator = `const fs = require("fs");
const path = require("path");

const root = process.cwd();
const snapshotPath = path.join(root, "docs", "pack-d", "PACK_D1_ROUTE_CONTRACT_SNAPSHOT.json");
const backendRoot = path.join(root, "Backend");

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function read(file) { return fs.readFileSync(file, "utf8"); }
function rel(file) { return path.relative(root, file).split(path.sep).join("/"); }
function walk(dir) {
  if (!exists(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (["bin", "obj", ".git", ".vs"].includes(entry.name)) return [];
      return walk(full);
    }
    return [full];
  });
}

function extractRouteContractsFromFile(file) {
  const text = read(file).replace(/\\r\\n/g, "\\n");
  const lines = text.split("\\n");
  const routes = [];
  const routeRegex = /\\.(MapGet|MapPost|MapPut|MapDelete|MapPatch|MapMethods)\\s*\\(\\s*(?:[A-Za-z0-9_]+\\s*,\\s*)?(@?["'])([^"']+)\\2/g;
  let match;

  while ((match = routeRegex.exec(text)) !== null) {
    const before = text.slice(0, match.index);
    const line = before.split("\\n").length;
    const context = lines.slice(Math.max(0, line - 4), Math.min(lines.length, line + 8)).join("\\n");
    const withName = context.match(/\\.WithName\\s*\\(\\s*(@?["'])([^"']+)\\1\\s*\\)/);
    const tag = context.match(/\\.WithTags\\s*\\(\\s*(@?["'])([^"']+)\\1\\s*\\)/);
    const group = context.match(/\\bMapGroup\\s*\\(\\s*(@?["'])([^"']+)\\1\\s*\\)/);

    routes.push({
      method: match[1].replace("Map", "").toUpperCase(),
      route: match[3],
      withName: withName ? withName[2] : null,
      tag: tag ? tag[2] : null,
      nearestGroup: group ? group[2] : null
    });
  }

  return routes;
}

function buildCurrentRouteKeys() {
  const files = walk(backendRoot).filter((file) => file.endsWith(".cs"));
  const routes = [];

  for (const file of files) {
    const relative = rel(file);
    if (!relative.includes("/Endpoints/") && !relative.endsWith("/Program.cs")) continue;
    if (!read(file).includes("Map")) continue;
    routes.push(...extractRouteContractsFromFile(file));
  }

  return routes.map((route) => {
    return [
      route.method,
      route.route,
      route.withName || "",
      route.tag || "",
      route.nearestGroup || ""
    ].join("|");
  }).sort();
}

if (!isFile(snapshotPath)) {
  console.error("Missing route snapshot: " + snapshotPath);
  process.exit(1);
}

const baseline = JSON.parse(read(snapshotPath));
const baselineKeys = baseline.routes.map((route) => {
  return [
    route.method,
    route.route,
    route.withName || "",
    route.tag || "",
    route.nearestGroup || ""
  ].join("|");
}).sort();

const currentKeys = buildCurrentRouteKeys();

const baselineSet = new Set(baselineKeys);
const currentSet = new Set(currentKeys);

const missing = baselineKeys.filter((key) => !currentSet.has(key));
const added = currentKeys.filter((key) => !baselineSet.has(key));

if (missing.length || added.length) {
  console.error("Pack D route-contract snapshot mismatch.");
  console.error(JSON.stringify({ missing, added }, null, 2));
  process.exit(1);
}

console.log("Pack D route-contract snapshot validation passed.");
console.log("Routes checked: " + baselineKeys.length);
`;

  write(validatorPath, validator);
}

function writeLineGate() {
  const lineGate = `const fs = require("fs");
const path = require("path");

const root = process.cwd();

const targets = [
  { task: "T-054", max: 500, path: "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs" },
  { task: "T-054", max: 500, path: "Backend/PlantProcess.Api/Endpoints/Workflow/Phase1WorkflowTruthEndpoints.cs" },
  { task: "T-055", max: 500, path: "Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs" },
  { task: "T-055", max: 500, path: "Backend/PlantProcess.Infrastructure/Configuration/ConnectorConfigurationService.cs" }
];

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function read(file) { return fs.readFileSync(file, "utf8"); }
function lines(file) { return isFile(file) ? read(file).replace(/\\r\\n/g, "\\n").split("\\n").length : 0; }

const failures = [];

for (const target of targets) {
  const absolute = path.join(root, target.path);
  const count = lines(absolute);

  if (!isFile(absolute)) {
    failures.push({ ...target, actual: 0, reason: "missing" });
    continue;
  }

  if (count > target.max) {
    failures.push({ ...target, actual: count, reason: "too-large" });
  }
}

if (failures.length) {
  console.error("Pack D backend thinness gate failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack D backend thinness gate passed.");
`;

  write(lineGatePath, lineGate);
}

function writeRegressionWrapper() {
  const wrapper = `[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunTests
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
    Run-Step "dotnet build Backend" {
        dotnet build ".\\Backend"
    }

    Run-Step "Pack D route-contract snapshot validation" {
        node ".\\tools\\pack-d\\validate-pack-d-route-contract-snapshot.cjs"
    }

    if ($RunTests) {
        Push-Location ".\\Backend"
        try {
            Run-Step "dotnet test" {
                dotnet test
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "Pack D backend regression wrapper completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
`;

  write(wrapperPath, wrapper);
}

function writeEvidence() {
  let evidence = isFile(evidencePath)
    ? normalize(read(evidencePath))
    : "# PlantProcess IQ Pack D Evidence\n";

  if (!evidence.includes("PPIQ_PACK_D1_BACKEND_SPLIT_AUDIT_ROUTE_SNAPSHOT")) {
    evidence += [
      "",
      "## Pack D-1 Backend split audit + route contract snapshot",
      "",
      "- Marker: `PPIQ_PACK_D1_BACKEND_SPLIT_AUDIT_ROUTE_SNAPSHOT`.",
      "- Captured backend god-file split audit for T-054 and T-055.",
      "- Captured route contract snapshot before backend endpoint refactor.",
      "- Captured service public-method surface snapshot before service split.",
      "- Added route-contract validator.",
      "- Added backend thinness validator.",
      "- Added backend regression wrapper.",
      "",
      "Generated artifacts:",
      "",
      "- `docs/pack-d/PACK_D1_BACKEND_SPLIT_AUDIT.md`",
      "- `docs/pack-d/PACK_D1_BACKEND_SPLIT_AUDIT.json`",
      "- `docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.md`",
      "- `docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.json`",
      "- `docs/pack-d/PACK_D1_SERVICE_SURFACE_SNAPSHOT.json`",
      "- `tools/pack-d/validate-pack-d-route-contract-snapshot.cjs`",
      "- `tools/pack-d/validate-pack-d-backend-thinness.cjs`",
      "- `tools/pack-d/Invoke-PackD-BackendRegression.ps1`",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack D-1 — Backend split audit + route contract snapshot");
console.log("=================================================================================================");

ensureDir(docsDir);
ensureDir(toolsDir);

const targetAudit = targetFiles.map(analyzeTargetFile);
const largeBackendFiles = findLargeBackendFiles();
const routeSnapshot = buildRouteSnapshot();
const serviceSnapshot = buildServiceSurfaceSnapshot();

const audit = {
  generatedAtUtc: new Date().toISOString(),
  marker: "PPIQ_PACK_D1_BACKEND_SPLIT_AUDIT_ROUTE_SNAPSHOT",
  targets: targetAudit,
  largeBackendFiles
};

write(auditJsonPath, JSON.stringify(audit, null, 2) + "\n");
writeAuditMarkdown(audit);

write(routeJsonPath, JSON.stringify(routeSnapshot, null, 2) + "\n");
writeRouteMarkdown(routeSnapshot);

write(serviceJsonPath, JSON.stringify(serviceSnapshot, null, 2) + "\n");

writeValidator();
writeLineGate();
writeRegressionWrapper();
writeEvidence();

console.log("");
console.log("Pack D-1 target blockers:");
for (const item of targetAudit) {
  console.log(
    " - " +
      item.task +
      " " +
      item.path +
      ": " +
      item.lines +
      " lines, routes=" +
      item.routes.length +
      ", methods=" +
      item.publicMethods.length +
      (item.overLimit ? " [SPLIT REQUIRED]" : " [OK]")
  );
}

console.log("");
console.log("Route contracts captured: " + routeSnapshot.routeCount);
console.log("Route groups captured   : " + routeSnapshot.groupCount);
console.log("Service surfaces captured: " + serviceSnapshot.serviceCount);

run("node --check route validator", ["node", "--check", "tools/pack-d/validate-pack-d-route-contract-snapshot.cjs"]);
run("node --check thinness validator", ["node", "--check", "tools/pack-d/validate-pack-d-backend-thinness.cjs"]);
run("Route contract snapshot validation", ["node", "tools/pack-d/validate-pack-d-route-contract-snapshot.cjs"]);

run("Backend dotnet build", ["dotnet", "build", "Backend"], root);

run("Pack D backend thinness preview", ["node", "tools/pack-d/validate-pack-d-backend-thinness.cjs"], root, true);

console.log("");
console.log("=================================================================================================");
console.log("Pack D-1 completed.");
console.log("Audit   : docs/pack-d/PACK_D1_BACKEND_SPLIT_AUDIT.md");
console.log("Routes  : docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.md");
console.log("Services: docs/pack-d/PACK_D1_SERVICE_SURFACE_SNAPSHOT.json");
console.log("Next    : Pack D-2 split T-054 while validating route contract after each change.");
console.log("=================================================================================================");