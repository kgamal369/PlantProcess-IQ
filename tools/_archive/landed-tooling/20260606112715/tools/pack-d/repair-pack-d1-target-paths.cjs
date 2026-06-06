const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const docsDir = path.join(root, "docs", "pack-d");
const toolsDir = path.join(root, "tools", "pack-d");

const correctedTargets = [
  {
    task: "T-054",
    name: "GenericSchemaMappingEndpoints",
    max: 500,
    path: "Backend/PlantProcess.Api/Endpoints/Admin/GenericSchemaMappingEndpoints.cs"
  },
  {
    task: "T-054",
    name: "Phase1WorkflowTruthEndpoints",
    max: 500,
    path: "Backend/PlantProcess.Api/Endpoints/Admin/Phase1WorkflowTruthEndpoints.cs"
  },
  {
    task: "T-055",
    name: "WorkflowEndpoints",
    max: 500,
    path: "Backend/PlantProcess.Api/Endpoints/Workflow/WorkflowEndpoints.cs"
  },
  {
    task: "T-055",
    name: "ConnectorConfigurationService",
    max: 500,
    path: "Backend/PlantProcess.Application/Integration/Services/Connectors/ConnectorConfigurationService.cs"
  }
];

const targetRegistryPath = path.join(docsDir, "PACK_D_TARGETS.json");
const correctionMdPath = path.join(docsDir, "PACK_D1_TARGET_PATH_CORRECTION.md");
const correctionJsonPath = path.join(docsDir, "PACK_D1_TARGET_PATH_CORRECTION.json");
const thinnessValidatorPath = path.join(toolsDir, "validate-pack-d-backend-thinness.cjs");
const evidencePath = path.join(docsDir, "PACK_D_IMPLEMENTATION_EVIDENCE.md");

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

function lineCount(relativePath) {
  const absolute = path.join(root, relativePath);
  if (!isFile(absolute)) return 0;
  return read(absolute).replace(/\r\n/g, "\n").split("\n").length;
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

function writeThinnessValidator() {
  const serializedTargets = JSON.stringify(
    correctedTargets.map((target) => ({
      task: target.task,
      max: target.max,
      path: target.path
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
  console.error("Pack D backend thinness gate failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack D backend thinness gate passed.");
`;

  write(thinnessValidatorPath, content);
}

function writeCorrectionDocs() {
  const results = correctedTargets.map((target) => ({
    ...target,
    exists: isFile(path.join(root, target.path)),
    lines: lineCount(target.path),
    status:
      !isFile(path.join(root, target.path))
        ? "MISSING"
        : lineCount(target.path) > target.max
          ? "SPLIT_REQUIRED"
          : "OK"
  }));

  const payload = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_D1B_TARGET_PATH_CORRECTION",
    reason:
      "Pack D-1 used two stale target paths. This correction aligns Pack D validators with the actual repository locations.",
    correctedTargets: results
  };

  write(targetRegistryPath, JSON.stringify(correctedTargets, null, 2) + "\n");
  write(correctionJsonPath, JSON.stringify(payload, null, 2) + "\n");

  const md = [];
  md.push("# Pack D-1B Target Path Correction");
  md.push("");
  md.push("Generated: " + payload.generatedAtUtc);
  md.push("");
  md.push("## Why this correction exists");
  md.push("");
  md.push("Pack D-1 captured the route snapshot correctly, but two target paths were stale:");
  md.push("");
  md.push("- `Phase1WorkflowTruthEndpoints.cs` is under `Endpoints/Admin`, not `Endpoints/Workflow`.");
  md.push("- `ConnectorConfigurationService.cs` is under `PlantProcess.Application/Integration/Services/Connectors`, not `PlantProcess.Infrastructure/Configuration`.");
  md.push("");
  md.push("## Correct Pack D target registry");
  md.push("");
  md.push("| Task | Name | File | Lines | Limit | Status |");
  md.push("|---|---|---|---:|---:|---|");

  for (const item of results) {
    md.push(
      `| ${item.task} | ${item.name} | \`${item.path}\` | ${item.lines} | ${item.max} | **${item.status}** |`
    );
  }

  md.push("");
  md.push("## Next step");
  md.push("");
  md.push("Run Pack D-2 against T-054:");
  md.push("");
  md.push("1. `GenericSchemaMappingEndpoints.cs`");
  md.push("2. `Phase1WorkflowTruthEndpoints.cs`");
  md.push("");
  md.push("After every split, validate route contracts:");
  md.push("");
  md.push("```powershell");
  md.push("node .\\tools\\pack-d\\validate-pack-d-route-contract-snapshot.cjs");
  md.push("dotnet build .\\Backend");
  md.push("```");
  md.push("");

  write(correctionMdPath, md.join("\n"));
}

function writeEvidence() {
  let evidence = isFile(evidencePath)
    ? read(evidencePath).replace(/\r\n/g, "\n")
    : "# PlantProcess IQ Pack D Evidence\n";

  if (!evidence.includes("PPIQ_PACK_D1B_TARGET_PATH_CORRECTION")) {
    evidence += [
      "",
      "## Pack D-1B Target path correction",
      "",
      "- Marker: `PPIQ_PACK_D1B_TARGET_PATH_CORRECTION`.",
      "- Corrected `Phase1WorkflowTruthEndpoints.cs` path from `Endpoints/Workflow` to `Endpoints/Admin`.",
      "- Corrected `ConnectorConfigurationService.cs` path from `PlantProcess.Infrastructure/Configuration` to `PlantProcess.Application/Integration/Services/Connectors`.",
      "- Rewrote Pack D backend thinness validator using the corrected target registry.",
      "- Generated `docs/pack-d/PACK_D_TARGETS.json`.",
      "- Generated `docs/pack-d/PACK_D1_TARGET_PATH_CORRECTION.md`.",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack D-1B — Target path correction");
console.log("=================================================================================================");

ensureDir(docsDir);
ensureDir(toolsDir);

writeThinnessValidator();
writeCorrectionDocs();
writeEvidence();

console.log("");
console.log("Corrected Pack D targets:");
for (const target of correctedTargets) {
  const lines = lineCount(target.path);
  console.log(
    " - " +
      target.task +
      " " +
      target.path +
      ": " +
      lines +
      " lines" +
      (lines > target.max ? " [SPLIT REQUIRED]" : " [OK]")
  );
}

run("node --check corrected thinness validator", ["node", "--check", "tools/pack-d/validate-pack-d-backend-thinness.cjs"]);
run("Route contract snapshot validation", ["node", "tools/pack-d/validate-pack-d-route-contract-snapshot.cjs"]);
run("Backend dotnet build", ["dotnet", "build", "Backend"]);
run("Corrected Pack D backend thinness preview", ["node", "tools/pack-d/validate-pack-d-backend-thinness.cjs"], root, true);

console.log("");
console.log("=================================================================================================");
console.log("Pack D-1B completed.");
console.log("Next: Pack D-2 split T-054 using corrected target paths.");
console.log("Targets: docs/pack-d/PACK_D_TARGETS.json");
console.log("=================================================================================================");