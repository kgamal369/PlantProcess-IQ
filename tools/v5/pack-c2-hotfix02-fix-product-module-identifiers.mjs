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

function moveFile(oldRelative, newRelative) {
  const oldFull = path.join(root, oldRelative);
  const newFull = path.join(root, newRelative);

  if (!exists(oldFull)) {
    return { from: oldRelative, to: newRelative, moved: false, reason: "source-missing" };
  }

  if (exists(newFull)) {
    return { from: oldRelative, to: newRelative, moved: false, reason: "target-exists" };
  }

  fs.mkdirSync(path.dirname(newFull), { recursive: true });
  fs.renameSync(oldFull, newFull);
  cleanEmptyDirs(oldFull);

  return { from: oldRelative, to: newRelative, moved: true, reason: "moved" };
}

const moved = [];

moved.push(moveFile(
  "Frontend/PlantProcess.Web/src/api/product-module-1/product-module-1Workflow.api.ts",
  "Frontend/PlantProcess.Web/src/api/workflow-foundation/workflowFoundation.api.ts"
));

moved.push(moveFile(
  "Frontend/PlantProcess.Web/src/api/product-module-78/product-module-78.api.ts",
  "Frontend/PlantProcess.Web/src/api/demo-analytics/demoAnalytics.api.ts"
));

const replacements = [
  ["product-module-1WorkflowApi", "workflowFoundationApi"],
  ["product-module-78Api", "demoAnalyticsApi"],
  ["product-module-1ActionMatrix", "workflowActionMatrix"],
  ["product-module-1RouteContracts", "workflowRouteContracts"],

  ["product-module-1Workflow.api", "workflowFoundation.api"],
  ["product-module-78.api", "demoAnalytics.api"],

  ["product-module-1/product-module-1Workflow.api", "workflow-foundation/workflowFoundation.api"],
  ["product-module-78/product-module-78.api", "demo-analytics/demoAnalytics.api"],

  ["@/api/product-module-1/product-module-1Workflow.api", "@/api/workflow-foundation/workflowFoundation.api"],
  ["@/api/product-module-78/product-module-78.api", "@/api/demo-analytics/demoAnalytics.api"],

  ["../api/product-module-1/product-module-1Workflow.api", "../api/workflow-foundation/workflowFoundation.api"],
  ["../api/product-module-78/product-module-78.api", "../api/demo-analytics/demoAnalytics.api"],

  ["../../api/product-module-1/product-module-1Workflow.api", "../../api/workflow-foundation/workflowFoundation.api"],
  ["../../api/product-module-78/product-module-78.api", "../../api/demo-analytics/demoAnalytics.api"],

  ["./api/product-module-1/product-module-1Workflow.api", "./api/workflow-foundation/workflowFoundation.api"],
  ["./api/product-module-78/product-module-78.api", "./api/demo-analytics/demoAnalytics.api"],

  // final generic cleanup for leftover strings/comments
  ["product-module-1", "workflow-foundation"],
  ["product-module-78", "demo-analytics"]
];

const changedFiles = [];

for (const file of walk(srcRoot)) {
  let text = read(file);
  const original = text;

  for (const [from, to] of replacements) {
    text = text.split(from).join(to);
  }

  if (text !== original) {
    write(file, text);
    changedFiles.push(toPosix(path.relative(root, file)));
  }
}

const invalidIdentifierOffenders = [];
const pathOffenders = [];
const importOffenders = [];
const contentFindings = [];

for (const file of walk(srcRoot)) {
  const relative = toPosix(path.relative(root, file));
  const text = read(file);

  const invalidMatches = text.match(/\bproduct-module-\d+\w*/g) ?? [];
  if (invalidMatches.length > 0) {
    invalidIdentifierOffenders.push({
      file: relative,
      matches: [...new Set(invalidMatches)].sort()
    });
  }

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
  changedFiles: [...new Set(changedFiles)].sort(),
  invalidIdentifierOffenders,
  pathOffenders,
  importOffenders,
  contentFindings,
  note: "C2 requires invalidIdentifierOffenders/pathOffenders/importOffenders to be zero. contentFindings are allowed only for C4."
};

write(
  path.join(reportDir, "pack-c2-hotfix02-fix-product-module-identifiers-report.json"),
  JSON.stringify(report, null, 2)
);

console.log(JSON.stringify({
  moved,
  changedFiles: [...new Set(changedFiles)].length,
  invalidIdentifierOffenders: invalidIdentifierOffenders.length,
  pathOffenders: pathOffenders.length,
  importOffenders: importOffenders.length,
  contentFindings: contentFindings.length,
  report: "Documentation/v5/pack-c2-hotfix02-fix-product-module-identifiers-report.json"
}, null, 2));

if (invalidIdentifierOffenders.length > 0 || pathOffenders.length > 0 || importOffenders.length > 0) {
  console.error("C2 Hotfix02 still has invalid identifier/path/import offenders.");
  process.exit(3);
}

console.log("[pack-c2-hotfix02] product-module identifier cleanup is green.");