const fs = require("fs");
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
