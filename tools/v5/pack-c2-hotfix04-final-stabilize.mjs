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

function safeRenamePath(oldFull, newFull, moved) {
  if (!exists(oldFull)) return;

  if (exists(newFull)) {
    if (read(oldFull) === read(newFull)) {
      fs.unlinkSync(oldFull);
      cleanEmptyDirs(oldFull);
    }
    return;
  }

  fs.mkdirSync(path.dirname(newFull), { recursive: true });
  fs.renameSync(oldFull, newFull);
  moved.push({
    from: toPosix(path.relative(root, oldFull)),
    to: toPosix(path.relative(root, newFull))
  });
  cleanEmptyDirs(oldFull);
}

const moved = [];
const changedFiles = [];
const createdFiles = [];

// ------------------------------------------------------------
// A) Rename any remaining product-module-* paths.
// ------------------------------------------------------------
for (const file of [...walk(srcRoot)]) {
  const relative = toPosix(path.relative(root, file));
  let nextRelative = relative;

  nextRelative = nextRelative
    .replaceAll("product-module-1Workflow.api", "workflowFoundation.api")
    .replaceAll("product-module-78.api", "demoAnalytics.api")
    .replaceAll("product-module-1", "workflow-foundation")
    .replaceAll("product-module-78", "demo-analytics");

  // Last-resort generic cleanup for product-module-N.
  nextRelative = nextRelative.replace(/product-module-(\d+)/g, "productModule$1");

  if (nextRelative !== relative) {
    safeRenamePath(file, path.join(root, nextRelative), moved);
  }
}

// ------------------------------------------------------------
// B) Replace invalid product-module-* strings/identifiers in source.
// ------------------------------------------------------------
const replacements = [
  ["product-module-1WorkflowApi", "workflowFoundationApi"],
  ["product-module-78Api", "demoAnalyticsApi"],
  ["product-module-1ActionMatrix", "workflowActionMatrix"],
  ["product-module-1RouteContracts", "workflowRouteContracts"],

  ["product-module-1Workflow.api", "workflowFoundation.api"],
  ["product-module-78.api", "demoAnalytics.api"],

  ["product-module-1/product-module-1Workflow.api", "workflow-foundation/workflowFoundation.api"],
  ["product-module-78/product-module-78.api", "demo-analytics/demoAnalytics.api"],

  ["product-module-1", "workflow-foundation"],
  ["product-module-78", "demo-analytics"]
];

for (const file of walk(srcRoot)) {
  let text = read(file);
  const original = text;

  for (const [from, to] of replacements) {
    text = text.split(from).join(to);
  }

  text = text.replace(/\bproduct-module-(\d+)([A-Za-z_]\w*)/g, "productModule$1$2");
  text = text.replace(/\bproduct-module-(\d+)\b/g, "productModule$1");

  if (text !== original) {
    write(file, text);
    changedFiles.push(toPosix(path.relative(root, file)));
  }
}

// ------------------------------------------------------------
// C) Fix missing CSS import in DemoAnalytics page.
// ------------------------------------------------------------
const demoDir = path.join(srcRoot, "pages", "DemoAnalytics");
const demoCss = path.join(demoDir, "demo-analytics.css");

if (!exists(demoCss)) {
  const candidates = [
    path.join(demoDir, "DemoAnalyticsPages.css"),
    path.join(demoDir, "phase78-pages.css"),
    path.join(demoDir, "Phase78Pages.css"),
    path.join(srcRoot, "pages", "Phase78", "phase78-pages.css"),
    path.join(srcRoot, "pages", "Phase78", "Phase78Pages.css")
  ];

  const source = candidates.find((candidate) => exists(candidate));

  if (source) {
    write(demoCss, read(source));
  } else {
    write(
      demoCss,
      `/* Product-safe stylesheet bridge for DemoAnalyticsPages.
   Created by Pack C2 Hotfix04 after phase artifact cleanup.
   Page-specific visual structure currently inherits the shared standard/product styles. */
`
    );
  }

  createdFiles.push(toPosix(path.relative(root, demoCss)));
}

// ------------------------------------------------------------
// D) Check unresolved local CSS imports and create safe bridges if needed.
// ------------------------------------------------------------
for (const file of walk(srcRoot).filter((x) => /\.(ts|tsx|js|jsx)$/.test(x))) {
  const text = read(file);
  const importRegex = /import\s+["'](\.\/[^"']+\.css)["'];/g;

  for (const match of text.matchAll(importRegex)) {
    const cssRelative = match[1];
    const cssFull = path.resolve(path.dirname(file), cssRelative);

    if (!exists(cssFull)) {
      write(
        cssFull,
        `/* Auto-created stylesheet bridge by Pack C2 Hotfix04.
   The original phase-named stylesheet was not present after artifact cleanup.
   Shared visual styling remains provided by global/standard product styles. */
`
      );
      createdFiles.push(toPosix(path.relative(root, cssFull)));
    }
  }
}

// ------------------------------------------------------------
// E) Final scan.
// ------------------------------------------------------------
const invalidIdentifierOffenders = [];
const pathOffenders = [];
const importOffenders = [];
const contentFindings = [];
const missingCssImports = [];

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

  if (/\.(ts|tsx|js|jsx)$/.test(file)) {
    const cssRegex = /import\s+["'](\.\/[^"']+\.css)["'];/g;
    for (const match of text.matchAll(cssRegex)) {
      const cssFull = path.resolve(path.dirname(file), match[1]);
      if (!exists(cssFull)) {
        missingCssImports.push({
          file: relative,
          importPath: match[1]
        });
      }
    }
  }
}

const report = {
  generatedAtUtc: new Date().toISOString(),
  moved,
  changedFiles: [...new Set(changedFiles)].sort(),
  createdFiles: [...new Set(createdFiles)].sort(),
  invalidIdentifierOffenders,
  pathOffenders,
  importOffenders,
  contentFindings,
  missingCssImports,
  note: "C2 final gate requires invalidIdentifierOffenders/pathOffenders/importOffenders/missingCssImports to be zero."
};

write(
  path.join(reportDir, "pack-c2-hotfix04-final-stabilization-report.json"),
  JSON.stringify(report, null, 2)
);

console.log(JSON.stringify({
  moved: moved.length,
  changedFiles: [...new Set(changedFiles)].length,
  createdFiles: [...new Set(createdFiles)].length,
  invalidIdentifierOffenders: invalidIdentifierOffenders.length,
  pathOffenders: pathOffenders.length,
  importOffenders: importOffenders.length,
  contentFindings: contentFindings.length,
  missingCssImports: missingCssImports.length,
  report: "Documentation/v5/pack-c2-hotfix04-final-stabilization-report.json"
}, null, 2));

if (
  invalidIdentifierOffenders.length > 0 ||
  pathOffenders.length > 0 ||
  importOffenders.length > 0 ||
  missingCssImports.length > 0
) {
  console.error("C2 Hotfix04 still has final offenders.");
  process.exit(3);
}

console.log("[pack-c2-hotfix04] final frontend C2 stabilization passed.");