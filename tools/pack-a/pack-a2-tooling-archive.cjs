const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);

const archiveBase = path.join(root, "tools", "_archive", "landed-tooling");
const archiveRunDir = path.join(archiveBase, timestamp);
const docsDir = path.join(root, "docs", "pack-a");
const toolsDir = path.join(root, "tools", "pack-a");

const reportJsonPath = path.join(docsDir, "PACK_A2_RUN_ONCE_TOOLING_ARCHIVE_REPORT.json");
const reportMdPath = path.join(docsDir, "PACK_A2_RUN_ONCE_TOOLING_ARCHIVE_REPORT.md");
const evidencePath = path.join(docsDir, "PACK_A_IMPLEMENTATION_EVIDENCE.md");
const archiveManifestJsonPath = path.join(archiveRunDir, "MANIFEST.json");
const archiveManifestMdPath = path.join(archiveRunDir, "MANIFEST.md");
const archiveIndexPath = path.join(archiveBase, "ARCHIVE_INDEX.md");
const validatorPath = path.join(toolsDir, "validate-pack-a-run-once-archive.cjs");

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
        ".pack_d_backup",
        ".pack_a_backup",
        "_archive"
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

function isTextToolingFile(file) {
  const relative = rel(file);
  const ext = path.extname(file).toLowerCase();

  if (!relative.startsWith("tools/")) return false;

  return [
    ".ps1",
    ".cjs",
    ".js",
    ".ts",
    ".tsx",
    ".cmd",
    ".bat",
    ".sh"
  ].includes(ext);
}

function isRunOnceCandidate(file) {
  const relative = rel(file);
  const base = path.basename(file).toLowerCase();

  if (!isTextToolingFile(file)) return false;
  if (relative.includes("/_archive/")) return false;
  if (relative.includes("/archive/")) return false;
  if (relative.includes("/archived/")) return false;

  // Keep reusable validators and wrappers active.
  if (/^validate-/i.test(base)) return false;
  if (/^invoke-/i.test(base)) return false;
  if (/^create-/i.test(base)) return false;
  if (/^pack-a2-tooling-archive/i.test(base)) return false;

  return (
    /^(apply|repair|continue|fix|patch)-/i.test(base) ||
    /-(apply|repair|continue|fix|patch)-/i.test(base) ||
    /run-once/i.test(base) ||
    /generated/i.test(base)
  );
}

function findCandidates() {
  return walk(path.join(root, "tools"))
    .filter(isRunOnceCandidate)
    .map((file) => ({
      absolute: file,
      path: rel(file),
      lines: lineCount(file),
      sizeBytes: fs.statSync(file).size,
      extension: path.extname(file).replace(".", ""),
      archivedPath: rel(path.join(archiveRunDir, rel(file)))
    }))
    .sort((a, b) => a.path.localeCompare(b.path));
}

function moveCandidate(item) {
  const source = path.join(root, item.path);
  const destination = path.join(archiveRunDir, item.path);

  ensureDir(path.dirname(destination));

  if (!isFile(source)) {
    return {
      ...item,
      status: "SKIPPED_SOURCE_MISSING"
    };
  }

  if (isFile(destination)) {
    return {
      ...item,
      status: "SKIPPED_ALREADY_ARCHIVED"
    };
  }

  fs.renameSync(source, destination);

  return {
    ...item,
    status: "ARCHIVED"
  };
}

function writeArchiveDocs(results) {
  const payload = {
    generatedAtUtc: new Date().toISOString(),
    marker: "PPIQ_PACK_A2_RUN_ONCE_TOOLING_ARCHIVE",
    task: "T-010",
    archiveRunDir: rel(archiveRunDir),
    archivedCount: results.filter((item) => item.status === "ARCHIVED").length,
    skippedCount: results.filter((item) => item.status !== "ARCHIVED").length,
    note:
      "Archived landed one-off apply/repair/continue/fix/patch tooling scripts. Reusable validators and Invoke wrappers remain active.",
    results
  };

  write(archiveManifestJsonPath, JSON.stringify(payload, null, 2) + "\n");
  write(reportJsonPath, JSON.stringify(payload, null, 2) + "\n");

  const md = [];

  md.push("# Pack A-2 Run-once Tooling Archive Manifest");
  md.push("");
  md.push("Generated: " + payload.generatedAtUtc);
  md.push("");
  md.push("- Marker: `PPIQ_PACK_A2_RUN_ONCE_TOOLING_ARCHIVE`");
  md.push("- Task: **T-010**");
  md.push("- Archive folder: `" + payload.archiveRunDir + "`");
  md.push("- Archived files: **" + payload.archivedCount + "**");
  md.push("- Skipped files: **" + payload.skippedCount + "**");
  md.push("");
  md.push("## Archived files");
  md.push("");
  md.push("| Original path | Archived path | Lines | Status |");
  md.push("|---|---|---:|---|");

  for (const item of results) {
    md.push(
      `| \`${item.path}\` | \`${item.archivedPath}\` | ${item.lines} | **${item.status}** |`
    );
  }

  md.push("");
  md.push("## Active tooling rule after Pack A-2");
  md.push("");
  md.push("- Active `tools/**` should keep reusable validators, wrappers, and generators.");
  md.push("- Landed one-off scripts named `apply-*`, `repair-*`, `continue-*`, `fix-*`, or `patch-*` should live under `tools/_archive/landed-tooling/**`.");
  md.push("- Future pack scripts should use neutral names like `pack-a3-ci-certification.cjs` instead of `apply-*` when they are meant to stay active during the session.");
  md.push("");

  write(archiveManifestMdPath, md.join("\n"));
  write(reportMdPath, md.join("\n"));

  let index = isFile(archiveIndexPath)
    ? normalize(read(archiveIndexPath))
    : "# PlantProcess IQ Landed Tooling Archive Index\n\n";

  const indexMarker = `PPIQ_PACK_A2_RUN_${timestamp}`;

  if (!index.includes(indexMarker)) {
    index += [
      "",
      "## " + timestamp,
      "",
      "- Marker: `" + indexMarker + "`",
      "- Pack marker: `PPIQ_PACK_A2_RUN_ONCE_TOOLING_ARCHIVE`",
      "- Manifest: `" + rel(archiveManifestMdPath) + "`",
      "- Archived files: **" + payload.archivedCount + "**",
      ""
    ].join("\n");

    write(archiveIndexPath, index);
  }

  let evidence = isFile(evidencePath)
    ? normalize(read(evidencePath))
    : "# PlantProcess IQ Pack A Evidence\n";

  if (!evidence.includes("PPIQ_PACK_A2_RUN_ONCE_TOOLING_ARCHIVE")) {
    evidence += [
      "",
      "## Pack A-2 Run-once tooling archive",
      "",
      "- Marker: `PPIQ_PACK_A2_RUN_ONCE_TOOLING_ARCHIVE`.",
      "- Task: `T-010`.",
      "- Archived landed one-off apply/repair/continue/fix/patch tooling scripts.",
      "- Kept reusable validators and Invoke wrappers active.",
      "- Created archive manifest and active tooling rule.",
      "",
      "Generated artifacts:",
      "",
      "- `docs/pack-a/PACK_A2_RUN_ONCE_TOOLING_ARCHIVE_REPORT.md`",
      "- `docs/pack-a/PACK_A2_RUN_ONCE_TOOLING_ARCHIVE_REPORT.json`",
      "- `" + rel(archiveManifestMdPath) + "`",
      "- `" + rel(archiveIndexPath) + "`",
      "- `tools/pack-a/validate-pack-a-run-once-archive.cjs`",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

function writeValidator() {
  const validator = `const fs = require("fs");
const path = require("path");

const root = process.cwd();

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function read(file) { return fs.readFileSync(file, "utf8"); }
function rel(file) { return path.relative(root, file).split(path.sep).join("/"); }

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
        ".pack_d_backup",
        ".pack_a_backup",
        "_archive"
      ].includes(entry.name)) {
        return [];
      }

      return walk(full);
    }

    return [full];
  });
}

function isTextToolingFile(file) {
  const relative = rel(file);
  const ext = path.extname(file).toLowerCase();

  if (!relative.startsWith("tools/")) return false;

  return [
    ".ps1",
    ".cjs",
    ".js",
    ".ts",
    ".tsx",
    ".cmd",
    ".bat",
    ".sh"
  ].includes(ext);
}

function isRunOnceCandidate(file) {
  const relative = rel(file);
  const base = path.basename(file).toLowerCase();

  if (!isTextToolingFile(file)) return false;
  if (relative.includes("/_archive/")) return false;
  if (relative.includes("/archive/")) return false;
  if (relative.includes("/archived/")) return false;

  if (/^validate-/i.test(base)) return false;
  if (/^invoke-/i.test(base)) return false;
  if (/^create-/i.test(base)) return false;
  if (/^pack-a2-tooling-archive/i.test(base)) return false;

  return (
    /^(apply|repair|continue|fix|patch)-/i.test(base) ||
    /-(apply|repair|continue|fix|patch)-/i.test(base) ||
    /run-once/i.test(base) ||
    /generated/i.test(base)
  );
}

const requiredFiles = [
  "docs/pack-a/PACK_A2_RUN_ONCE_TOOLING_ARCHIVE_REPORT.json",
  "docs/pack-a/PACK_A2_RUN_ONCE_TOOLING_ARCHIVE_REPORT.md",
  "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md",
  "tools/_archive/landed-tooling/ARCHIVE_INDEX.md"
];

const failures = [];

for (const file of requiredFiles) {
  if (!isFile(path.join(root, file))) {
    failures.push({ file, reason: "missing" });
  }
}

const activeCandidates = walk(path.join(root, "tools"))
  .filter(isRunOnceCandidate)
  .map((file) => rel(file))
  .sort();

if (activeCandidates.length) {
  failures.push({
    reason: "active-run-once-candidates-remain",
    count: activeCandidates.length,
    files: activeCandidates
  });
}

if (isFile(path.join(root, "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"))) {
  const evidence = read(path.join(root, "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md"));
  if (!evidence.includes("PPIQ_PACK_A2_RUN_ONCE_TOOLING_ARCHIVE")) {
    failures.push({ file: "docs/pack-a/PACK_A_IMPLEMENTATION_EVIDENCE.md", reason: "missing-marker" });
  }
}

if (failures.length) {
  console.error("Pack A-2 run-once tooling archive validation failed.");
  console.error(JSON.stringify(failures, null, 2));
  process.exit(1);
}

console.log("Pack A-2 run-once tooling archive validation passed.");
console.log("Active landed one-off tooling candidates remaining: 0");
`;

  write(validatorPath, validator);
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack A-2 — Archive landed run-once tooling scripts");
console.log("=================================================================================================");

ensureDir(archiveRunDir);
ensureDir(docsDir);
ensureDir(toolsDir);

const candidates = findCandidates();

console.log("");
console.log("Active landed run-once tooling candidates found: " + candidates.length);

const results = candidates.map(moveCandidate);

writeArchiveDocs(results);
writeValidator();

console.log("");
console.log("Archive result:");
console.log(" - Archived: " + results.filter((item) => item.status === "ARCHIVED").length);
console.log(" - Skipped : " + results.filter((item) => item.status !== "ARCHIVED").length);
console.log(" - Archive : " + rel(archiveRunDir));

if (results.length) {
  console.log("");
  console.log("Archived files:");
  for (const item of results) {
    console.log(" - " + item.path + " -> " + item.archivedPath + " [" + item.status + "]");
  }
}

run("node --check Pack A-2 validator", ["node", "--check", "tools/pack-a/validate-pack-a-run-once-archive.cjs"]);
run("Pack A-2 archive validation", ["node", "tools/pack-a/validate-pack-a-run-once-archive.cjs"]);

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
console.log("Pack A-2 completed.");
console.log("Expected: T-010 should improve or close in the task-level scorecard.");
console.log("Report : docs/pack-a/PACK_A2_RUN_ONCE_TOOLING_ARCHIVE_REPORT.md");
console.log("Archive: " + rel(archiveRunDir));
console.log("Next   : Pack A-3 wire taskClosure + routeContract into CI certification.");
console.log("=================================================================================================");