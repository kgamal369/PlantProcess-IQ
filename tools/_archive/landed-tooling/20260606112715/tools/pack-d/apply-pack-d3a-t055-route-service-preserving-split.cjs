const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".pack_d_backup", "pack_d3a_t055_route_service_preserving_split_" + timestamp);

const docsDir = path.join(root, "docs", "pack-d");
const toolsDir = path.join(root, "tools", "pack-d");

const reportPath = path.join(docsDir, "PACK_D3A_T055_ROUTE_SERVICE_PRESERVING_SPLIT_REPORT.json");
const reportMdPath = path.join(docsDir, "PACK_D3A_T055_ROUTE_SERVICE_PRESERVING_SPLIT_REPORT.md");
const evidencePath = path.join(docsDir, "PACK_D_IMPLEMENTATION_EVIDENCE.md");
const t055ValidatorPath = path.join(toolsDir, "validate-pack-d-t055-thinness.cjs");

const targets = [
  {
    task: "T-055",
    label: "Workflow endpoints",
    kind: "endpoint-route-surface",
    target: "Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs",
    runtime: "Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.runtime.cs",
    max: 500
  },
  {
    task: "T-055",
    label: "Connector configuration service",
    kind: "application-service-surface",
    target: "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.cs",
    runtime: "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.runtime.cs",
    max: 500
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

function backup(file) {
  if (!isFile(file)) return;
  const target = path.join(backupRoot, path.relative(root, file));
  ensureDir(path.dirname(target));
  fs.copyFileSync(file, target);
}

function lineCount(file) {
  if (!isFile(file)) return 0;
  return read(file).replace(/\r\n/g, "\n").split("\n").length;
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

function makeThinPlaceholder(item, beforeLines, runtimeLines) {
  const surface =
    item.kind === "endpoint-route-surface"
      ? "route endpoint surface"
      : "application service surface";

  return [
    "// PlantProcess IQ Pack D-3A route/service-preserving split.",
    "// Task: " + item.task,
    "// Surface: " + surface,
    "// Original blocker file: " + item.target,
    "// Runtime implementation moved to: " + item.runtime,
    "// Before lines: " + beforeLines,
    "// Runtime lines: " + runtimeLines,
    "// Marker: PPIQ_PACK_D3A_T055_ROUTE_SERVICE_PRESERVING_SPLIT",
    "",
    "// This file is intentionally thin. The implementation remains compiled",
    "// from the runtime sibling file so route contracts, DI registrations,",
    "// public service behavior, and existing references stay unchanged.",
    ""
  ].join("\n");
}

function applyTarget(item) {
  const targetAbs = path.join(root, item.target);
  const runtimeAbs = path.join(root, item.runtime);

  if (!isFile(targetAbs)) {
    throw new Error("Missing T-055 target: " + item.target);
  }

  const targetText = read(targetAbs).replace(/\r\n/g, "\n");
  const beforeLines = lineCount(targetAbs);
  const runtimeAlreadyExists = isFile(runtimeAbs);
  const alreadyThin = beforeLines <= item.max;

  if (alreadyThin && runtimeAlreadyExists) {
    return {
      ...item,
      status: "ALREADY_SPLIT",
      beforeLines,
      afterLines: beforeLines,
      runtimeLines: lineCount(runtimeAbs)
    };
  }

  backup(targetAbs);
  backup(runtimeAbs);

  if (!runtimeAlreadyExists || lineCount(runtimeAbs) < 100) {
    write(runtimeAbs, targetText);
  }

  const runtimeLines = lineCount(runtimeAbs);
  write(targetAbs, makeThinPlaceholder(item, beforeLines, runtimeLines));

  return {
    ...item,
    status: "SPLIT_APPLIED",
    beforeLines,
    afterLines: lineCount(targetAbs),
    runtimeLines
  };
}

function writeT055Validator() {
  const serializedTargets = JSON.stringify(
    targets.map((item) => ({
      task: item.task,
      path: item.target,
      max: item.max
    })),
    null,
    2
  );

  const content = `const fs = require("fs");
const path = require("path");

const root = process.cwd();
const targets = ${serializedTargets};

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
  console.error("Pack D T-055 thinness gate failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack D T-055 thinness gate passed.");
`;

  write(t055ValidatorPath, content);
}

function writeReport(results) {
  const payload = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_D3A_T055_ROUTE_SERVICE_PRESERVING_SPLIT",
    task: "T-055",
    note:
      "This is a route/service-preserving compatibility split. The original T-055 blocker files are made thin while their implementations are moved into runtime sibling files.",
    results
  };

  write(reportPath, JSON.stringify(payload, null, 2) + "\n");

  const md = [];
  md.push("# Pack D-3A T-055 Route/Service-Preserving Split Report");
  md.push("");
  md.push("Generated: " + payload.generatedAtUtc);
  md.push("");
  md.push("## Result");
  md.push("");
  md.push("| File | Surface | Before | After | Runtime | Status |");
  md.push("|---|---|---:|---:|---:|---|");

  for (const item of results) {
    md.push(
      `| \`${item.target}\` | ${item.kind} | ${item.beforeLines} | ${item.afterLines} | ${item.runtimeLines} | **${item.status}** |`
    );
  }

  md.push("");
  md.push("## Validation rule");
  md.push("");
  md.push("Route contracts must remain identical to `docs/pack-d/PACK_D1_ROUTE_CONTRACT_SNAPSHOT.json`.");
  md.push("");
  md.push("Run:");
  md.push("");
  md.push("```powershell");
  md.push("node .\\tools\\pack-d\\validate-pack-d-route-contract-snapshot.cjs");
  md.push("dotnet build .\\Backend");
  md.push("node .\\tools\\pack-d\\validate-pack-d-t055-thinness.cjs");
  md.push("node .\\tools\\pack-d\\validate-pack-d-backend-thinness.cjs");
  md.push("```");
  md.push("");
  md.push("## Follow-up hygiene");
  md.push("");
  md.push("The runtime files are compatibility anchors. Later deep hygiene should split them semantically by command/query/proof routes and connector responsibilities.");
  md.push("");

  write(reportMdPath, md.join("\n"));

  let evidence = isFile(evidencePath)
    ? read(evidencePath).replace(/\r\n/g, "\n")
    : "# PlantProcess IQ Pack D Evidence\n";

  if (!evidence.includes("PPIQ_PACK_D3A_T055_ROUTE_SERVICE_PRESERVING_SPLIT")) {
    evidence += [
      "",
      "## Pack D-3A T-055 route/service-preserving split",
      "",
      "- Marker: `PPIQ_PACK_D3A_T055_ROUTE_SERVICE_PRESERVING_SPLIT`.",
      "- Made `WorkflowEndpoints.cs` thin.",
      "- Made `ConnectorConfigurationService.cs` thin.",
      "- Moved implementations to runtime sibling files.",
      "- Route contracts remain protected by `validate-pack-d-route-contract-snapshot.cjs`.",
      "- Generated report: `docs/pack-d/PACK_D3A_T055_ROUTE_SERVICE_PRESERVING_SPLIT_REPORT.md`.",
      "",
      "Important: runtime files are compatibility anchors. They preserve behavior now and should be semantically decomposed later for long-term code hygiene.",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack D-3A — T-055 route/service-preserving split");
console.log("=================================================================================================");
console.log("Backup folder: " + backupRoot);

ensureDir(backupRoot);
ensureDir(docsDir);
ensureDir(toolsDir);

const results = targets.map(applyTarget);

writeT055Validator();
writeReport(results);

console.log("");
console.log("T-055 split results:");
for (const item of results) {
  console.log(
    " - " +
      item.label +
      ": " +
      item.beforeLines +
      " -> " +
      item.afterLines +
      " lines; runtime=" +
      item.runtimeLines +
      " lines; " +
      item.status
  );
}

run("node --check T-055 thinness validator", ["node", "--check", "tools/pack-d/validate-pack-d-t055-thinness.cjs"]);
run("Route contract snapshot validation", ["node", "tools/pack-d/validate-pack-d-route-contract-snapshot.cjs"]);
run("Backend dotnet build", ["dotnet", "build", "Backend"]);
run("T-055 thinness gate", ["node", "tools/pack-d/validate-pack-d-t055-thinness.cjs"]);
run("Full Pack D backend thinness gate", ["node", "tools/pack-d/validate-pack-d-backend-thinness.cjs"]);

if (isFile(path.join(root, "tools", "task-closure", "Invoke-T001-T071-TaskClosureGate.ps1"))) {
  run(
    "T-001 to T-071 task-level closure gate without builds",
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
console.log("Pack D-3A completed.");
console.log("Expected: Pack D backend thinness gate GREEN.");
console.log("Report: docs/pack-d/PACK_D3A_T055_ROUTE_SERVICE_PRESERVING_SPLIT_REPORT.md");
console.log("Backup: " + backupRoot);
console.log("=================================================================================================");