const fs = require("fs");
const path = require("path");

const root = process.cwd();
const webRoot = fs.existsSync(path.join(root, "Frontend", "PlantProcess.Web"))
  ? path.join(root, "Frontend", "PlantProcess.Web")
  : root;

const srcRoot = path.join(webRoot, "src");
const failMode = process.argv.includes("--fail");
const marker = "PPIQ_P2_T010_TABLE_STANDARDIZATION";

function relFromWeb(file) {
  return path.relative(webRoot, file).replaceAll(path.sep, "/");
}

function walk(dir, predicate) {
  const output = [];

  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const item = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (
        entry.name === "node_modules" ||
        entry.name === "dist" ||
        entry.name === "coverage" ||
        entry.name === "playwright-report" ||
        entry.name === "test-results" ||
        entry.name === "__snapshots__"
      ) {
        continue;
      }

      output.push(...walk(item, predicate));
    } else if (!predicate || predicate(item)) {
      output.push(item);
    }
  }

  return output;
}

function isAuditedSource(file) {
  const rel = relFromWeb(file);

  if (!rel.startsWith("src/pages/") && !rel.startsWith("src/components/")) return false;
  if (rel.startsWith("src/components/standard/")) return false;
  if (rel.includes("/__tests__/")) return false;
  if (rel.endsWith(".test.tsx") || rel.endsWith(".test.ts") || rel.endsWith(".stories.tsx") || rel.endsWith(".stories.ts")) return false;

  return rel.endsWith(".tsx");
}

const files = walk(srcRoot, isAuditedSource);
const findings = [];

for (const file of files) {
  const rel = relFromWeb(file);
  const text = fs.readFileSync(file, "utf8");
  const lines = text.split(/\r?\n/);

  lines.forEach((line, index) => {
    if (/<table\b/.test(line)) {
      findings.push({
        file: rel,
        line: index + 1,
        kind: "raw-table-root",
        message: "raw <table> is not allowed outside canonical standard table wrappers",
        snippet: line.trim().slice(0, 220),
      });
    }
  });
}

const standardDataTablePath = path.join(webRoot, "src", "components", "standard", "StandardDataTable.tsx");
const sortableDataTablePath = path.join(webRoot, "src", "components", "SortableDataTable.tsx");
const standardCssPath = path.join(webRoot, "src", "components", "standard", "standard-components.css");
const standardIndexPath = path.join(webRoot, "src", "components", "standard", "index.ts");

const standardDataTableText = fs.existsSync(standardDataTablePath)
  ? fs.readFileSync(standardDataTablePath, "utf8")
  : "";

const sortableDataTableText = fs.existsSync(sortableDataTablePath)
  ? fs.readFileSync(sortableDataTablePath, "utf8")
  : "";

const cssText = fs.existsSync(standardCssPath)
  ? fs.readFileSync(standardCssPath, "utf8")
  : "";

const indexText = fs.existsSync(standardIndexPath)
  ? fs.readFileSync(standardIndexPath, "utf8")
  : "";

for (const token of [
  marker,
  "StandardDataTableColumn",
  "isLoading",
  "error",
  "emptyText",
  "loadingText",
  "caption",
  "ariaLabel",
  "aria-sort",
  "aria-busy",
  "density",
  "rowKey",
]) {
  if (!standardDataTableText.includes(token)) {
    findings.push({
      file: "src/components/standard/StandardDataTable.tsx",
      line: 0,
      kind: "standard-data-table-contract",
      message: "StandardDataTable missing token: " + token,
      snippet: "",
    });
  }
}

for (const token of [
  marker,
  "StandardDataTable",
  "P2T010_SORTABLE_TABLE_STANDARDIZATION_MARKER",
  "isLoading",
  "error",
  "emptyText",
  "ariaLabel",
]) {
  if (!sortableDataTableText.includes(token)) {
    findings.push({
      file: "src/components/SortableDataTable.tsx",
      line: 0,
      kind: "sortable-table-contract",
      message: "SortableDataTable missing token: " + token,
      snippet: "",
    });
  }
}

if (/<table\b/.test(sortableDataTableText)) {
  findings.push({
    file: "src/components/SortableDataTable.tsx",
    line: 0,
    kind: "sortable-table-raw-markup",
    message: "SortableDataTable must delegate to StandardDataTable and not render raw <table>",
    snippet: "",
  });
}

if (/style=\{/.test(sortableDataTableText)) {
  findings.push({
    file: "src/components/SortableDataTable.tsx",
    line: 0,
    kind: "sortable-table-inline-style",
    message: "SortableDataTable must not use inline style props",
    snippet: "",
  });
}

if (!indexText.includes("StandardDataTable")) {
  findings.push({
    file: "src/components/standard/index.ts",
    line: 0,
    kind: "standard-index-export",
    message: "standard index must export StandardDataTable",
    snippet: "",
  });
}

for (const token of [
  marker,
  "ppiq-std-data-table-shell",
  "ppiq-std-data-table__cell",
  "ppiq-std-data-table__state--loading",
  "ppiq-std-data-table__state--empty",
  "ppiq-std-data-table__state--error",
  "focus-visible",
]) {
  if (!cssText.includes(token)) {
    findings.push({
      file: "src/components/standard/standard-components.css",
      line: 0,
      kind: "standard-table-css-contract",
      message: "Standard table CSS missing token: " + token,
      snippet: "",
    });
  }
}

const report = {
  marker,
  generatedAtUtc: new Date().toISOString(),
  auditedFiles: files.length,
  findingCount: findings.length,
  findings,
};

const outDir = path.join(root, "Documentation", "P2-T010_TableStandardization_Latest");
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, "table-audit.json"), JSON.stringify(report, null, 2));

const md = [
  "# P2-T010 Table Standardization Audit",
  "",
  "Marker: " + marker,
  "",
  "- Audited files: " + report.auditedFiles,
  "- Findings: " + report.findingCount,
  "",
  findings.length === 0
    ? "## Result\n\nGREEN — raw table roots are clean, SortableDataTable delegates to StandardDataTable, and the table state/accessibility contract is present."
    : "## Findings\n\n" + findings.map((x) => "- " + x.file + ":" + x.line + " [" + x.kind + "] " + x.message + (x.snippet ? " — " + x.snippet : "")).join("\n"),
  "",
].join("\n");

fs.writeFileSync(path.join(outDir, "table-audit.md"), md);

console.log(JSON.stringify({
  marker,
  auditedFiles: report.auditedFiles,
  findingCount: report.findingCount,
}, null, 2));

if (failMode && findings.length > 0) {
  console.error("[RED] P2-T010 table audit failed. See Documentation/P2-T010_TableStandardization_Latest/table-audit.md");
  process.exit(1);
}

console.log("[GREEN] P2-T010 table audit passed.");