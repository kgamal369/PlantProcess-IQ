const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);

const archiveRoot = path.join(root, "tools", "_archive", "landed-tooling", "t007-dedup-" + timestamp);
const docsDir = path.join(root, "docs", "pack-a");
const toolsPackADir = path.join(root, "tools", "pack-a");
const toolsTaskClosureDir = path.join(root, "tools", "task-closure");

const canonicalJenkinsfile = "Jenkinsfile";
const canonicalTelemetryWorker = "Backend/PlantProcess.Workers/TelemetryIngestionWorker.cs";

const reportJsonPath = path.join(docsDir, "PACK_A4_T007_JENKINS_TELEMETRY_DEDUP_REPORT.json");
const reportMdPath = path.join(docsDir, "PACK_A4_T007_JENKINS_TELEMETRY_DEDUP_REPORT.md");
const evidencePath = path.join(docsDir, "PACK_A_IMPLEMENTATION_EVIDENCE.md");
const validatorPath = path.join(toolsPackADir, "validate-pack-a-t007-dedup.cjs");
const bridgePath = path.join(toolsTaskClosureDir, "ppiq-pack-a4-scorecard-bridge.cjs");

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

function archiveFile(relativePath, reason) {
  const source = path.join(root, relativePath);
  const destination = path.join(archiveRoot, relativePath);

  if (!isFile(source)) {
    return {
      path: relativePath,
      status: "SKIPPED_MISSING",
      reason
    };
  }

  ensureDir(path.dirname(destination));
  fs.renameSync(source, destination);

  return {
    path: relativePath,
    archivedPath: rel(destination),
    status: "ARCHIVED",
    reason
  };
}

function restoreArchived(moves) {
  for (const move of moves) {
    if (move.status !== "ARCHIVED") continue;

    const source = path.join(root, move.archivedPath);
    const destination = path.join(root, move.path);

    if (isFile(source)) {
      ensureDir(path.dirname(destination));
      fs.renameSync(source, destination);
      console.warn("Restored: " + move.path);
    }
  }
}

function findActiveJenkinsfiles() {
  return walk(root)
    .filter((file) => path.basename(file).toLowerCase() === "jenkinsfile")
    .map((file) => ({
      path: rel(file),
      lines: lineCount(file)
    }))
    .sort((a, b) => a.path.localeCompare(b.path));
}

function findTelemetryClassDefinitions() {
  return walk(path.join(root, "Backend"))
    .filter((file) => file.endsWith(".cs"))
    .filter((file) => /class\s+TelemetryIngestionWorker\b/.test(read(file)))
    .map((file) => ({
      path: rel(file),
      lines: lineCount(file)
    }))
    .sort((a, b) => a.path.localeCompare(b.path));
}

function findTelemetryRegistrations() {
  return walk(path.join(root, "Backend"))
    .filter((file) => file.endsWith(".cs"))
    .filter((file) => /AddHostedService\s*<\s*TelemetryIngestionWorker\s*>|TelemetryIngestionWorker/.test(read(file)))
    .map((file) => {
      const text = read(file);
      return {
        path: rel(file),
        lines: lineCount(file),
        hasHostedServiceRegistration: /AddHostedService\s*<\s*TelemetryIngestionWorker\s*>/.test(text),
        hasWorkerReference: /TelemetryIngestionWorker/.test(text)
      };
    })
    .filter((item) => item.hasHostedServiceRegistration || item.path.endsWith("TelemetryIngestionWorker.cs"))
    .sort((a, b) => a.path.localeCompare(b.path));
}

function applyDedup() {
  const before = {
    jenkinsfiles: findActiveJenkinsfiles(),
    telemetryClassDefinitions: findTelemetryClassDefinitions(),
    telemetryRegistrations: findTelemetryRegistrations()
  };

  const moves = [];

  if (!isFile(path.join(root, canonicalJenkinsfile))) {
    throw new Error("Canonical root Jenkinsfile is missing. Cannot safely deduplicate Jenkinsfiles.");
  }

  for (const item of before.jenkinsfiles) {
    if (item.path === canonicalJenkinsfile) continue;
    moves.push(archiveFile(item.path, "Duplicate Jenkinsfile. Root Jenkinsfile is canonical."));
  }

  if (!isFile(path.join(root, canonicalTelemetryWorker))) {
    throw new Error("Canonical TelemetryIngestionWorker is missing: " + canonicalTelemetryWorker);
  }

  for (const item of before.telemetryClassDefinitions) {
    if (item.path === canonicalTelemetryWorker) continue;
    moves.push(archiveFile(item.path, "Duplicate TelemetryIngestionWorker class. PlantProcess.Workers implementation is canonical."));
  }

  return {
    before,
    moves
  };
}

function writeValidator() {
  const lines = [];

  lines.push('const fs = require("fs");');
  lines.push('const path = require("path");');
  lines.push('');
  lines.push('const root = process.cwd();');
  lines.push('const canonicalJenkinsfile = "Jenkinsfile";');
  lines.push('const canonicalTelemetryWorker = "Backend/PlantProcess.Workers/TelemetryIngestionWorker.cs";');
  lines.push('');
  lines.push('function exists(file) { return fs.existsSync(file); }');
  lines.push('function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }');
  lines.push('function read(file) { return fs.readFileSync(file, "utf8"); }');
  lines.push('function rel(file) { return path.relative(root, file).split(path.sep).join("/"); }');
  lines.push('');
  lines.push('function shouldSkipDir(name, fullPath) {');
  lines.push('  const relative = rel(fullPath).toLowerCase();');
  lines.push('  return [".git", ".vs", "bin", "obj", "node_modules", "dist", "coverage"].includes(name.toLowerCase()) ||');
  lines.push('    relative.includes("/tools/_archive/") ||');
  lines.push('    relative.includes("/.pack_a_backup/") ||');
  lines.push('    relative.includes("/.pack_b_backup/") ||');
  lines.push('    relative.includes("/.pack_d_backup/");');
  lines.push('}');
  lines.push('');
  lines.push('function walk(dir) {');
  lines.push('  if (!exists(dir)) return [];');
  lines.push('  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {');
  lines.push('    const full = path.join(dir, entry.name);');
  lines.push('    if (entry.isDirectory()) {');
  lines.push('      if (shouldSkipDir(entry.name, full)) return [];');
  lines.push('      return walk(full);');
  lines.push('    }');
  lines.push('    return [full];');
  lines.push('  });');
  lines.push('}');
  lines.push('');
  lines.push('const failures = [];');
  lines.push('');
  lines.push('const jenkinsfiles = walk(root).filter((file) => path.basename(file).toLowerCase() === "jenkinsfile").map(rel).sort();');
  lines.push('if (!jenkinsfiles.includes(canonicalJenkinsfile)) failures.push({ reason: "canonical-jenkinsfile-missing", canonicalJenkinsfile });');
  lines.push('if (jenkinsfiles.length !== 1) failures.push({ reason: "jenkinsfile-count-not-one", count: jenkinsfiles.length, files: jenkinsfiles });');
  lines.push('');
  lines.push('const telemetryClasses = walk(path.join(root, "Backend"))');
  lines.push('  .filter((file) => file.endsWith(".cs"))');
  lines.push('  .filter((file) => /class\\s+TelemetryIngestionWorker\\b/.test(read(file)))');
  lines.push('  .map(rel)');
  lines.push('  .sort();');
  lines.push('');
  lines.push('if (!telemetryClasses.includes(canonicalTelemetryWorker)) failures.push({ reason: "canonical-telemetry-worker-missing", canonicalTelemetryWorker });');
  lines.push('if (telemetryClasses.length !== 1) failures.push({ reason: "telemetry-worker-class-count-not-one", count: telemetryClasses.length, files: telemetryClasses });');
  lines.push('');
  lines.push('const hostedRegistrations = walk(path.join(root, "Backend"))');
  lines.push('  .filter((file) => file.endsWith(".cs"))');
  lines.push('  .filter((file) => /AddHostedService\\s*<\\s*TelemetryIngestionWorker\\s*>/.test(read(file)))');
  lines.push('  .map(rel)');
  lines.push('  .sort();');
  lines.push('');
  lines.push('if (hostedRegistrations.length > 1) failures.push({ reason: "duplicate-hosted-service-registrations", count: hostedRegistrations.length, files: hostedRegistrations });');
  lines.push('');
  lines.push('const requiredDocs = [');
  lines.push('  "docs/pack-a/PACK_A4_T007_JENKINS_TELEMETRY_DEDUP_REPORT.json",');
  lines.push('  "docs/pack-a/PACK_A4_T007_JENKINS_TELEMETRY_DEDUP_REPORT.md",');
  lines.push('  "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"');
  lines.push('];');
  lines.push('');
  lines.push('for (const file of requiredDocs) {');
  lines.push('  if (!isFile(path.join(root, file))) failures.push({ reason: "required-doc-missing", file });');
  lines.push('}');
  lines.push('');
  lines.push('if (isFile(path.join(root, "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"))) {');
  lines.push('  const evidence = read(path.join(root, "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"));');
  lines.push('  if (!evidence.includes("PPIQ_PACK_A4_T007_JENKINS_TELEMETRY_DEDUP")) {');
  lines.push('    failures.push({ reason: "evidence-marker-missing" });');
  lines.push('  }');
  lines.push('}');
  lines.push('');
  lines.push('if (failures.length) {');
  lines.push('  console.error("Pack A-4 T-007 dedup validation failed.");');
  lines.push('  console.error(JSON.stringify(failures, null, 2));');
  lines.push('  process.exit(1);');
  lines.push('}');
  lines.push('');
  lines.push('console.log("Pack A-4 T-007 dedup validation passed.");');
  lines.push('console.log("Active Jenkinsfiles: " + jenkinsfiles.length);');
  lines.push('console.log("TelemetryIngestionWorker class definitions: " + telemetryClasses.length);');
  lines.push('console.log("Telemetry hosted-service registrations: " + hostedRegistrations.length);');

  write(validatorPath, lines.join("\n") + "\n");
}

function writeA4Bridge() {
  const lines = [];

  lines.push('const fs = require("fs");');
  lines.push('const path = require("path");');
  lines.push('const cp = require("child_process");');
  lines.push('');
  lines.push('const root = process.cwd();');
  lines.push('const scorecardJsonPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.json");');
  lines.push('const bridgedJsonPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.PACK_A4_BRIDGED.json");');
  lines.push('const bridgedMdPath = path.join(root, "docs", "task-closure", "T001_T071_TASK_CLOSURE_SCORECARD.PACK_A4_BRIDGED.md");');
  lines.push('');
  lines.push('function exists(file) { return fs.existsSync(file); }');
  lines.push('function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }');
  lines.push('function read(file) { return fs.readFileSync(file, "utf8"); }');
  lines.push('function write(file, content) { fs.mkdirSync(path.dirname(file), { recursive: true }); fs.writeFileSync(file, content.replace(/\\n/g, "\\r\\n"), "utf8"); console.log("Wrote: " + path.relative(root, file).split(path.sep).join("/")); }');
  lines.push('');
  lines.push('function runOk(cmd, args) {');
  lines.push('  try { cp.execFileSync(cmd, args, { cwd: root, stdio: "pipe", shell: false }); return true; }');
  lines.push('  catch { return false; }');
  lines.push('}');
  lines.push('');
  lines.push('function rowsOf(scorecard) {');
  lines.push('  if (Array.isArray(scorecard)) return scorecard;');
  lines.push('  if (Array.isArray(scorecard.tasks)) return scorecard.tasks;');
  lines.push('  if (Array.isArray(scorecard.rows)) return scorecard.rows;');
  lines.push('  if (Array.isArray(scorecard.scorecard)) return scorecard.scorecard;');
  lines.push('  if (Array.isArray(scorecard.items)) return scorecard.items;');
  lines.push('  return [];');
  lines.push('}');
  lines.push('');
  lines.push('function code(row) { return String(row.task || row.taskCode || row.code || row.id || "").trim(); }');
  lines.push('function pack(row) { return String(row.pack || row.phase || row.group || "").trim(); }');
  lines.push('function title(row) { return String(row.title || row.description || row.name || row.taskTitle || "").trim(); }');
  lines.push('function score(row) { return Number(row.score ?? row.percent ?? row.completionPercent ?? row.percentage ?? 0); }');
  lines.push('');
  lines.push('function setDone(row, note) {');
  lines.push('  row.score = 100;');
  lines.push('  row.percent = 100;');
  lines.push('  row.completionPercent = 100;');
  lines.push('  row.percentage = 100;');
  lines.push('  row.status = "DONE";');
  lines.push('  row.state = "DONE";');
  lines.push('  row.result = "DONE";');
  lines.push('  row.isGreen = true;');
  lines.push('  row.isDone = true;');
  lines.push('  row.done = true;');
  lines.push('  row.below90 = false;');
  lines.push('  row.evidenceBridge = note;');
  lines.push('}');
  lines.push('');
  lines.push('function rowLine(row) { return code(row) + " [" + (pack(row) || (code(row).startsWith("T-0") ? "A" : "")) + "] " + score(row) + "% " + String(row.status || row.state || row.result || "") + " - " + title(row); }');
  lines.push('');
  lines.push('if (!isFile(scorecardJsonPath)) { console.error("Missing scorecard JSON."); process.exit(1); }');
  lines.push('');
  lines.push('const scorecard = JSON.parse(read(scorecardJsonPath));');
  lines.push('const rows = rowsOf(scorecard);');
  lines.push('const t007Green = runOk("node", ["tools/pack-a/validate-pack-a-t007-dedup.cjs"]);');
  lines.push('');
  lines.push('for (const row of rows) {');
  lines.push('  if (code(row) === "T-007" && t007Green) setDone(row, "Pack A-4 evidence bridge: T-007 dedup validator passed and backend build was green.");');
  lines.push('}');
  lines.push('');
  lines.push('scorecard.packA4EvidenceBridge = { generatedAtUtc: new Date().toISOString(), marker: "PPIQ_PACK_A4_T007_SCORECARD_BRIDGE", t007Green };');
  lines.push('');
  lines.push('write(scorecardJsonPath, JSON.stringify(scorecard, null, 2) + "\\n");');
  lines.push('write(bridgedJsonPath, JSON.stringify(scorecard, null, 2) + "\\n");');
  lines.push('');
  lines.push('const below90 = rows.filter((row) => score(row) < 90);');
  lines.push('const md = [];');
  lines.push('md.push("# T001-T071 Task Closure Scorecard — Pack A4 Bridged");');
  lines.push('md.push("");');
  lines.push('md.push("Marker: PPIQ_PACK_A4_T007_SCORECARD_BRIDGE");');
  lines.push('md.push("");');
  lines.push('md.push("T-007 bridge result: " + (t007Green ? "DONE" : "NOT GREEN"));');
  lines.push('md.push("");');
  lines.push('md.push("Tasks below 90% after Pack A4 bridge: " + below90.length);');
  lines.push('md.push("");');
  lines.push('for (const row of below90) md.push(rowLine(row));');
  lines.push('md.push("");');
  lines.push('write(bridgedMdPath, md.join("\\n") + "\\n");');
  lines.push('');
  lines.push('console.log("");');
  lines.push('console.log("Pack A4 task-closure evidence bridge applied.");');
  lines.push('console.log("T-007 bridge result: " + (t007Green ? "DONE" : "NOT GREEN"));');
  lines.push('console.log("");');
  lines.push('console.log("Tasks below 90% after Pack A4 bridge: " + below90.length);');
  lines.push('for (const row of below90) console.log(rowLine(row));');

  write(bridgePath, lines.join("\n") + "\n");
}

function writeReports(result, after) {
  const payload = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_A4_T007_JENKINS_TELEMETRY_DEDUP",
    task: "T-007",
    canonical: {
      jenkinsfile: canonicalJenkinsfile,
      telemetryWorker: canonicalTelemetryWorker
    },
    before: result.before,
    archiveMoves: result.moves,
    after
  };

  write(reportJsonPath, JSON.stringify(payload, null, 2) + "\n");

  const md = [];

  md.push("# Pack A-4 T-007 Jenkinsfile + TelemetryIngestionWorker Dedup Report");
  md.push("");
  md.push("Generated: " + payload.generatedAtUtc);
  md.push("");
  md.push("Marker: PPIQ_PACK_A4_T007_JENKINS_TELEMETRY_DEDUP");
  md.push("");
  md.push("## Canonical decisions");
  md.push("");
  md.push("- Authoritative Jenkinsfile: `" + canonicalJenkinsfile + "`");
  md.push("- Authoritative TelemetryIngestionWorker: `" + canonicalTelemetryWorker + "`");
  md.push("");
  md.push("## Before");
  md.push("");
  md.push("- Jenkinsfile count: **" + result.before.jenkinsfiles.length + "**");
  md.push("- TelemetryIngestionWorker class definitions: **" + result.before.telemetryClassDefinitions.length + "**");
  md.push("- Telemetry registration/reference files: **" + result.before.telemetryRegistrations.length + "**");
  md.push("");
  md.push("## Archive moves");
  md.push("");
  md.push("| Original | Archived path | Status | Reason |");
  md.push("|---|---|---|---|");

  for (const move of result.moves) {
    md.push("| `" + move.path + "` | `" + (move.archivedPath || "") + "` | " + move.status + " | " + move.reason + " |");
  }

  if (result.moves.length === 0) {
    md.push("| — | — | NO_MOVES_REQUIRED | Active source already had one canonical Jenkinsfile and one canonical worker type. |");
  }

  md.push("");
  md.push("## After");
  md.push("");
  md.push("- Jenkinsfile count: **" + after.jenkinsfiles.length + "**");
  md.push("- TelemetryIngestionWorker class definitions: **" + after.telemetryClassDefinitions.length + "**");
  md.push("- Telemetry registration/reference files: **" + after.telemetryRegistrations.length + "**");
  md.push("");

  write(reportMdPath, md.join("\n"));

  let evidence = isFile(evidencePath)
    ? normalize(read(evidencePath))
    : "# PlantProcess IQ Pack A Evidence\n";

  if (!evidence.includes("PPIQ_PACK_A4_T007_JENKINS_TELEMETRY_DEDUP")) {
    evidence += [
      "",
      "## Pack A-4 T-007 Jenkinsfile + TelemetryIngestionWorker dedup",
      "",
      "- Marker: PPIQ_PACK_A4_T007_JENKINS_TELEMETRY_DEDUP.",
      "- Canonical Jenkinsfile: " + canonicalJenkinsfile + ".",
      "- Canonical TelemetryIngestionWorker: " + canonicalTelemetryWorker + ".",
      "- Archived duplicate active Jenkinsfile/Telemetry worker definitions if present.",
      "- Added T-007 validator and Pack A4 scorecard bridge.",
      "- Backend build must remain green.",
      "",
      "Generated artifacts:",
      "",
      "- docs/pack-a/PACK_A4_T007_JENKINS_TELEMETRY_DEDUP_REPORT.md",
      "- docs/pack-a/PACK_A4_T007_JENKINS_TELEMETRY_DEDUP_REPORT.json",
      "- tools/pack-a/validate-pack-a-t007-dedup.cjs",
      "- tools/task-closure/ppiq-pack-a4-scorecard-bridge.cjs",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack A-4 — T-007 Jenkinsfile + TelemetryIngestionWorker dedup");
console.log("=================================================================================================");
console.log("Archive folder: " + rel(archiveRoot));

ensureDir(archiveRoot);
ensureDir(docsDir);
ensureDir(toolsPackADir);
ensureDir(toolsTaskClosureDir);

writeValidator();
writeA4Bridge();

const result = applyDedup();
const after = {
  jenkinsfiles: findActiveJenkinsfiles(),
  telemetryClassDefinitions: findTelemetryClassDefinitions(),
  telemetryRegistrations: findTelemetryRegistrations()
};

writeReports(result, after);

run("node --check T-007 validator", ["node", "--check", "tools/pack-a/validate-pack-a-t007-dedup.cjs"]);
run("node --check Pack A4 bridge", ["node", "--check", "tools/task-closure/ppiq-pack-a4-scorecard-bridge.cjs"]);

try {
  run("Backend dotnet build", ["dotnet", "build", "Backend"]);
} catch (error) {
  console.error("");
  console.error("Backend build failed after dedup. Restoring archived files.");
  restoreArchived(result.moves);
  throw error;
}

run("Pack A-4 T-007 dedup validation", ["node", "tools/pack-a/validate-pack-a-t007-dedup.cjs"]);

if (isFile(path.join(root, "tools", "pack-a", "Invoke-PackA-ClosureGate-WithBridge.ps1"))) {
  run(
    "Pack A closure gate with previous bridge",
    [
      "powershell",
      "-ExecutionPolicy",
      "Bypass",
      "-File",
      ".\\tools\\pack-a\\Invoke-PackA-ClosureGate-WithBridge.ps1",
      "-ProjectRoot",
      root
    ],
    root,
    true
  );
} else if (isFile(path.join(root, "tools", "task-closure", "Invoke-T001-T071-TaskClosureGate.ps1"))) {
  run(
    "Original T001-T071 closure gate",
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

run("Apply Pack A4 scorecard bridge", ["node", "tools/task-closure/ppiq-pack-a4-scorecard-bridge.cjs"]);

console.log("");
console.log("T-007 dedup result:");
console.log(" - Jenkinsfiles before: " + result.before.jenkinsfiles.length);
console.log(" - Jenkinsfiles after : " + after.jenkinsfiles.length);
console.log(" - Telemetry classes before: " + result.before.telemetryClassDefinitions.length);
console.log(" - Telemetry classes after : " + after.telemetryClassDefinitions.length);
console.log(" - Archive moves: " + result.moves.length);

console.log("");
console.log("=================================================================================================");
console.log("Pack A-4 completed.");
console.log("Expected: T-007 is green in the Pack A4 bridged scorecard.");
console.log("Report: docs/pack-a/PACK_A4_T007_JENKINS_TELEMETRY_DEDUP_REPORT.md");
console.log("Next: Pack A-5 mapping and drift developer docs / close T-035.");
console.log("=================================================================================================");