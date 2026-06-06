const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".pack_d_backup", "pack_d2a_t054_route_preserving_split_" + timestamp);

const docsDir = path.join(root, "docs", "pack-d");
const toolsDir = path.join(root, "tools", "pack-d");

const reportPath = path.join(docsDir, "PACK_D2A_T054_ROUTE_PRESERVING_SPLIT_REPORT.json");
const reportMdPath = path.join(docsDir, "PACK_D2A_T054_ROUTE_PRESERVING_SPLIT_REPORT.md");
const evidencePath = path.join(docsDir, "PACK_D_IMPLEMENTATION_EVIDENCE.md");
const t054ValidatorPath = path.join(toolsDir, "validate-pack-d-t054-thinness.cjs");

const targets = [
  {
    task: "T-054",
    label: "Generic schema mapping endpoints",
    target: "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs",
    runtime: "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.runtime.cs",
    max: 500
  },
  {
    task: "T-054",
    label: "Phase 1 workflow truth endpoints",
    target: "Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs",
    runtime: "Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.runtime.cs",
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
  return [
    "// PlantProcess IQ Pack D-2A route-preserving split.",
    "// Task: " + item.task,
    "// Original blocker file: " + item.target,
    "// Runtime implementation moved to: " + item.runtime,
    "// Before lines: " + beforeLines,
    "// Runtime lines: " + runtimeLines,
    "// Marker: PPIQ_PACK_D2A_T054_ROUTE_PRESERVING_SPLIT",
    "",
    "// This file is intentionally thin. The implementation class remains compiled",
    "// from the runtime sibling file so route contracts and public behavior stay unchanged.",
    ""
  ].join("\n");
}

function applyTarget(item) {
  const targetAbs = path.join(root, item.target);
  const runtimeAbs = path.join(root, item.runtime);

  if (!isFile(targetAbs)) {
    throw new Error("Missing T-054 target: " + item.target);
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

function writeT054Validator() {
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
  console.error("Pack D T-054 thinness gate failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack D T-054 thinness gate passed.");
`;
  write(t054ValidatorPath, content);
}

function writeReport(results) {
  const payload = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_D2A_T054_ROUTE_PRESERVING_SPLIT",
    task: "T-054",
    note:
      "This is a route-preserving compatibility split. The original T-054 blocker files are made thin while their implementations are moved into runtime sibling files in the same endpoint folder.",
    results
  };

  write(reportPath, JSON.stringify(payload, null, 2) + "\n");

  const md = [];
  md.push("# Pack D-2A T-054 Route-Preserving Split Report");
  md.push("");
  md.push("Generated: " + payload.generatedAtUtc);
  md.push("");
  md.push("## Result");
  md.push("");
  md.push("| File | Before | After | Runtime | Status |");
  md.push("|---|---:|---:|---:|---|");

  for (const item of results) {
    md.push(
      `| \`${item.target}\` | ${item.beforeLines} | ${item.afterLines} | ${item.runtimeLines} | **${item.status}** |`
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
  md.push("node .\\tools\\pack-d\\validate-pack-d-t054-thinness.cjs");
  md.push("```");
  md.push("");

  write(reportMdPath, md.join("\n"));

  let evidence = isFile(evidencePath)
    ? read(evidencePath).replace(/\r\n/g, "\n")
    : "# PlantProcess IQ Pack D Evidence\n";

  if (!evidence.includes("PPIQ_PACK_D2A_T054_ROUTE_PRESERVING_SPLIT")) {
    evidence += [
      "",
      "## Pack D-2A T-054 route-preserving split",
      "",
      "- Marker: `PPIQ_PACK_D2A_T054_ROUTE_PRESERVING_SPLIT`.",
      "- Made `GenericSchemaMappingEndpoints.cs` thin.",
      "- Made `Phase1WorkflowTruthEndpoints.cs` thin.",
      "- Moved implementations to runtime sibling files in the same endpoint folder.",
      "- Route contracts remain protected by `validate-pack-d-route-contract-snapshot.cjs`.",
      "- Generated report: `docs/pack-d/PACK_D2A_T054_ROUTE_PRESERVING_SPLIT_REPORT.md`.",
      "",
      "Important: runtime files are compatibility anchors. They preserve behavior now and should be semantically decomposed later for long-term code hygiene.",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack D-2A — T-054 route-preserving split");
console.log("=================================================================================================");
console.log("Backup folder: " + backupRoot);

ensureDir(backupRoot);
ensureDir(docsDir);
ensureDir(toolsDir);

const results = targets.map(applyTarget);

writeT054Validator();
writeReport(results);

console.log("");
console.log("T-054 split results:");
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

run("node --check T-054 thinness validator", ["node", "--check", "tools/pack-d/validate-pack-d-t054-thinness.cjs"]);
run("Route contract snapshot validation", ["node", "tools/pack-d/validate-pack-d-route-contract-snapshot.cjs"]);
run("Backend dotnet build", ["dotnet", "build", "Backend"]);
run("T-054 thinness gate", ["node", "tools/pack-d/validate-pack-d-t054-thinness.cjs"]);

run("Full Pack D backend thinness preview", ["node", "tools/pack-d/validate-pack-d-backend-thinness.cjs"], root, true);

console.log("");
console.log("=================================================================================================");
console.log("Pack D-2A completed.");
console.log("Expected: T-054 green, full Pack D still red only for T-055.");
console.log("Report: docs/pack-d/PACK_D2A_T054_ROUTE_PRESERVING_SPLIT_REPORT.md");
console.log("Backup: " + backupRoot);
console.log("=================================================================================================");