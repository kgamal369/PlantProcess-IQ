import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const srcRoot = path.join(root, "Frontend", "PlantProcess.Web", "src");
const reportDir = path.join(root, "Documentation", "v5");

fs.mkdirSync(reportDir, { recursive: true });

function exists(file) {
  return fs.existsSync(file);
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, text) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text, "utf8");
}

function toPosix(value) {
  return value.replaceAll(path.sep, "/");
}

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "build", "coverage", ".vite", "bin", "obj"].includes(entry.name)) continue;
      walk(full, output);
    } else if (/\.(ts|tsx|js|jsx|css)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

function cleanEmptyDirs(start) {
  let current = path.dirname(start);

  while (current.startsWith(srcRoot) && current !== srcRoot) {
    if (fs.existsSync(current) && fs.readdirSync(current).length === 0) {
      fs.rmdirSync(current);
      current = path.dirname(current);
    } else {
      break;
    }
  }
}

const mappings = [
  ["Phase1WorkflowTruthPanel", "WorkflowTruthPanel"],

  ["Phase56Pages", "MaterialAnalyticsPages"],
  ["Phase56", "MaterialAnalytics"],

  ["Phase78Pages", "DemoAnalyticsPages"],
  ["Phase78", "DemoAnalytics"],

  ["Phase910Pages", "CommercialReadinessPages"],
  ["Phase910", "CommercialReadiness"],

  ["Phase1112Pages", "ActionInternationalizationPages"],
  ["Phase1112", "ActionInternationalization"],

  ["Phase1314Pages", "DeploymentCompliancePages"],
  ["Phase1314", "DeploymentCompliance"],

  ["phase2", "inspection"],
  ["Phase2", "Inspection"],

  ["P03P04", "MappingFoundation"]
];

function applyMappings(value) {
  let next = value;

  for (const [from, to] of mappings) {
    next = next.split(from).join(to);
  }

  // Generic fallback for any missed phase-style path segment.
  next = next.replace(/Phase(\d+)/g, "ProductModule$1");
  next = next.replace(/phase(\d+)/g, "product-module-$1");
  next = next.replace(/P(\d{2})P(\d{2})/g, "ProductModule$1$2");

  return next;
}

const moved = [];
const conflicts = [];
const deleted = [];

// A) Rename remaining phase-named file paths under frontend src.
const filesBeforeMove = walk(srcRoot);

for (const oldFull of filesBeforeMove) {
  const oldRelative = toPosix(path.relative(root, oldFull));
  const newRelative = applyMappings(oldRelative);

  if (newRelative === oldRelative) continue;

  const newFull = path.join(root, newRelative);

  if (!exists(oldFull)) continue;

  if (!exists(newFull)) {
    fs.mkdirSync(path.dirname(newFull), { recursive: true });
    fs.renameSync(oldFull, newFull);
    moved.push({ from: oldRelative, to: newRelative, mode: "move" });
    cleanEmptyDirs(oldFull);
    continue;
  }

  const oldText = read(oldFull);
  const newText = read(newFull);

  if (oldText === newText) {
    fs.unlinkSync(oldFull);
    deleted.push(oldRelative);
    cleanEmptyDirs(oldFull);
    continue;
  }

  const ext = path.extname(newFull);
  const base = path.basename(newFull, ext);
  const dir = path.dirname(newFull);
  let migratedFull = path.join(dir, `${base}.migrated${ext}`);
  let i = 1;

  while (exists(migratedFull)) {
    migratedFull = path.join(dir, `${base}.migrated${i}${ext}`);
    i += 1;
  }

  fs.renameSync(oldFull, migratedFull);
  moved.push({
    from: oldRelative,
    to: toPosix(path.relative(root, migratedFull)),
    mode: "target-existed-migrated"
  });
  conflicts.push({
    oldPath: oldRelative,
    existingTarget: newRelative,
    migratedCopy: toPosix(path.relative(root, migratedFull))
  });
  cleanEmptyDirs(oldFull);
}

// B) Rewrite remaining import identifiers and module paths.
const changedFiles = [];

for (const file of walk(srcRoot)) {
  let text = read(file);
  const original = text;

  text = applyMappings(text);

  // Safety cleanup for common slash aliases.
  text = text
    .replaceAll("@/components/inspection/", "@/components/inspection/")
    .replaceAll("/inspection/", "/inspection/")
    .replaceAll("pages/MaterialAnalytics/MaterialAnalyticsPages", "pages/MaterialAnalytics/MaterialAnalyticsPages")
    .replaceAll("pages/DemoAnalytics/DemoAnalyticsPages", "pages/DemoAnalytics/DemoAnalyticsPages")
    .replaceAll("pages/CommercialReadiness/CommercialReadinessPages", "pages/CommercialReadiness/CommercialReadinessPages")
    .replaceAll("pages/ActionInternationalization/ActionInternationalizationPages", "pages/ActionInternationalization/ActionInternationalizationPages")
    .replaceAll("pages/DeploymentCompliance/DeploymentCompliancePages", "pages/DeploymentCompliance/DeploymentCompliancePages");

  if (text !== original) {
    write(file, text);
    changedFiles.push(toPosix(path.relative(root, file)));
  }
}

// C) Re-scan exactly like the C2 validator.
const pathOffenders = [];
const importOffenders = [];
const contentFindings = [];

for (const file of walk(srcRoot)) {
  const relative = toPosix(path.relative(root, file));
  const text = read(file);

  if (/(^|\/)(Phase\d+|phase\d+|P\d{2}P\d{2}|P03P04)(\/|\.|$)/.test(relative)) {
    pathOffenders.push(relative);
  }

  for (const pattern of [
    "Phase1WorkflowTruthPanel",
    "Phase56Pages",
    "Phase78Pages",
    "Phase910Pages",
    "Phase1112Pages",
    "Phase1314Pages",
    "@/components/phase2",
    "/phase2/"
  ]) {
    if (text.includes(pattern)) {
      importOffenders.push({ file: relative, pattern });
    }
  }

  const matches = text.match(/\b(Phase\d+|phase\d+|P\d{2}P\d{2}|P03P04)\b/g) ?? [];
  if (matches.length > 0) {
    contentFindings.push({
      file: relative,
      matches: [...new Set(matches)].sort()
    });
  }
}

const report = {
  generatedAtUtc: new Date().toISOString(),
  moved,
  deleted,
  conflicts,
  changedFiles: [...new Set(changedFiles)].sort(),
  pathOffenders,
  importOffenders,
  contentFindings,
  note: "Path/import offenders must be zero for C2. Content findings are allowed and belong to C4."
};

write(
  path.join(reportDir, "pack-c2-hotfix01-complete-phase-cleanup-report.json"),
  JSON.stringify(report, null, 2)
);

console.log(JSON.stringify({
  moved: moved.length,
  deleted: deleted.length,
  conflicts: conflicts.length,
  changedFiles: [...new Set(changedFiles)].length,
  pathOffenders: pathOffenders.length,
  importOffenders: importOffenders.length,
  contentFindings: contentFindings.length,
  report: "Documentation/v5/pack-c2-hotfix01-complete-phase-cleanup-report.json"
}, null, 2));

if (pathOffenders.length > 0 || importOffenders.length > 0) {
  console.error("C2 Hotfix01 still has path/import offenders.");
  process.exit(3);
}

console.log("[pack-c2-hotfix01] complete phase cleanup passed.");