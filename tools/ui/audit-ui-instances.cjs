
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const webRoot = fs.existsSync(path.join(root, "Frontend", "PlantProcess.Web"))
  ? path.join(root, "Frontend", "PlantProcess.Web")
  : root;

const srcRoot = path.join(webRoot, "src");
const failMode = process.argv.includes("--fail");
const marker = "PPIQ_P2_T08_STANDARD_COMPONENT_ROLLOUT_BLOCKING";

function rel(file) {
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

function isAuditedFile(file) {
  const r = rel(file);

  if (!r.startsWith("src/pages/") && !r.startsWith("src/components/")) return false;
  if (r.startsWith("src/components/standard/")) return false;
  if (r.includes("/__tests__/")) return false;
  if (r.endsWith(".test.tsx") || r.endsWith(".test.ts") || r.endsWith(".stories.tsx") || r.endsWith(".stories.ts")) return false;

  return r.endsWith(".tsx");
}

const files = walk(srcRoot, (file) => isAuditedFile(file));
const findings = [];

const checks = [
  { kind: "native-control", regex: /<button\b/g, label: "native <button>" },
  { kind: "native-control", regex: /<input\b/g, label: "native <input>" },
  { kind: "native-control", regex: /<select\b/g, label: "native <select>" },
  { kind: "native-control", regex: /<textarea\b/g, label: "native <textarea>" },
  { kind: "native-table", regex: /<table\b/g, label: "native <table>" },
  { kind: "inline-style", regex: /\sstyle=\{/g, label: "inline style prop" },
];

for (const file of files) {
  const text = fs.readFileSync(file, "utf8");
  const lines = text.split(/\r?\n/);

  for (const check of checks) {
    for (let lineIndex = 0; lineIndex < lines.length; lineIndex += 1) {
      if (check.regex.test(lines[lineIndex])) {
        findings.push({
          file: rel(file),
          line: lineIndex + 1,
          kind: check.kind,
          label: check.label,
          snippet: lines[lineIndex].trim().slice(0, 220),
        });
      }

      check.regex.lastIndex = 0;
    }
  }
}

const report = {
  marker,
  generatedAtUtc: new Date().toISOString(),
  auditedFiles: files.length,
  findingCount: findings.length,
  nativeControlCount: findings.filter((x) => x.kind === "native-control").length,
  nativeTableCount: findings.filter((x) => x.kind === "native-table").length,
  inlineStyleCount: findings.filter((x) => x.kind === "inline-style").length,
  findings,
};

const docsDir = path.join(root, "Documentation", "P2-T08_StandardRollout_Latest");
fs.mkdirSync(docsDir, { recursive: true });
fs.writeFileSync(path.join(docsDir, "ui-standard-audit.json"), JSON.stringify(report, null, 2));

const md = [
  "# P2-T08 UI Standard Rollout Audit",
  "",
  "Marker: " + marker,
  "",
  "- Audited files: " + report.auditedFiles,
  "- Findings: " + report.findingCount,
  "- Native form controls: " + report.nativeControlCount,
  "- Native table tags: " + report.nativeTableCount,
  "- Inline style props: " + report.inlineStyleCount,
  "",
  report.findings.length === 0
    ? "## Result\n\nGREEN — no native controls/tables or inline style props outside standard wrappers."
    : "## Findings\n\n" + report.findings.map((x) => "- " + x.file + ":" + x.line + " " + x.label + " — " + x.snippet).join("\n"),
  "",
].join("\n");

fs.writeFileSync(path.join(docsDir, "ui-standard-audit.md"), md);

console.log(JSON.stringify({
  marker,
  auditedFiles: report.auditedFiles,
  findingCount: report.findingCount,
  nativeControlCount: report.nativeControlCount,
  nativeTableCount: report.nativeTableCount,
  inlineStyleCount: report.inlineStyleCount,
}, null, 2));

if (failMode && findings.length > 0) {
  console.error("[RED] P2-T08 UI standard audit failed. See Documentation/P2-T08_StandardRollout_Latest/ui-standard-audit.md");
  process.exit(1);
}

console.log("[GREEN] P2-T08 UI standard audit passed.");
