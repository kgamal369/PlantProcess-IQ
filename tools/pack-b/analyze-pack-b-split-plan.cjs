const fs = require("fs");
const path = require("path");

const root = process.cwd();
const docsDir = path.join(root, "docs", "pack-b");

function exists(file) { return fs.existsSync(file); }
function isFile(file) { return exists(file) && fs.statSync(file).isFile(); }
function read(file) { return fs.readFileSync(file, "utf8"); }
function ensureDir(dir) { fs.mkdirSync(dir, { recursive: true }); }
function rel(file) { return path.relative(root, file).split(path.sep).join("/"); }
function lineCount(file) { return isFile(file) ? read(file).replace(/\r\n/g, "\n").split("\n").length : 0; }

function findTopLevelDeclarationStarts(lines) {
  const starts = [];
  const regex = /^\s*(export\s+)?(default\s+)?(function|const|let|var|class|interface|type|enum)\s+([A-Za-z0-9_]+)/;

  for (let i = 0; i < lines.length; i += 1) {
    const match = lines[i].match(regex);
    if (match) {
      starts.push({
        line: i + 1,
        kind: match[3],
        name: match[4]
      });
    }
  }

  return starts;
}

const targets = [
  {
    path: "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizard.implementation.tsx",
    max: 400,
    task: "T-036"
  },
  {
    path: "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizardContent.implementation.tsx",
    max: 400,
    task: "T-036"
  },
  {
    path: "Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts",
    max: 450,
    task: "T-037"
  },
  {
    path: "Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.implementation.tsx",
    max: 450,
    task: "T-037"
  },
  {
    path: "Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx",
    max: 450,
    task: "T-037"
  }
];

const report = [];

for (const target of targets) {
  const absolute = path.join(root, target.path);
  if (!isFile(absolute)) {
    report.push({
      ...target,
      exists: false,
      currentLines: 0,
      status: "MISSING"
    });
    continue;
  }

  const text = read(absolute).replace(/\r\n/g, "\n");
  const lines = text.split("\n");
  const declarations = findTopLevelDeclarationStarts(lines);

  report.push({
    ...target,
    exists: true,
    currentLines: lines.length,
    status: lines.length <= target.max ? "OK" : "NEEDS_SPLIT",
    topLevelDeclarations: declarations
  });
}

ensureDir(docsDir);
fs.writeFileSync(
  path.join(docsDir, "PACK_B_SPLIT_PLAN.json"),
  JSON.stringify({ generatedAtUtc: new Date().toISOString(), targets: report }, null, 2) + "\n",
  "utf8"
);

const md = [];
md.push("# Pack B Split Plan");
md.push("");
md.push("Generated: " + new Date().toISOString());
md.push("");
md.push("| Task | File | Lines | Limit | Status |");
md.push("|---|---|---:|---:|---|");

for (const item of report) {
  md.push("| " + item.task + " | `" + item.path + "` | " + item.currentLines + " | " + item.max + " | **" + item.status + "** |");
}

md.push("");
md.push("## Top-level declaration hints");
md.push("");

for (const item of report) {
  md.push("### " + item.path);
  md.push("");
  if (!item.exists) {
    md.push("- File missing.");
    md.push("");
    continue;
  }

  if (!item.topLevelDeclarations.length) {
    md.push("- No simple top-level declarations detected by the lightweight scanner.");
    md.push("");
    continue;
  }

  for (const declaration of item.topLevelDeclarations.slice(0, 80)) {
    md.push("- Line " + declaration.line + ": " + declaration.kind + " " + declaration.name);
  }

  md.push("");
}

fs.writeFileSync(path.join(docsDir, "PACK_B_SPLIT_PLAN.md"), md.join("\n") + "\n", "utf8");
console.log("Pack B split plan written:");
console.log(" - docs/pack-b/PACK_B_SPLIT_PLAN.md");
console.log(" - docs/pack-b/PACK_B_SPLIT_PLAN.json");
