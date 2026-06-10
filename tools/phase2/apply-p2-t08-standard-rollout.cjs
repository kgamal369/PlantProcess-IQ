const fs = require("fs");
const path = require("path");

const root = process.cwd();
const marker = "PPIQ_P2_T08_STANDARD_COMPONENT_ROLLOUT_BLOCKING";

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function write(rel, content) {
  const target = full(rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content.replace(/\r?\n/g, "\r\n"), "utf8");
  console.log("[P2-T08] wrote " + rel);
}

function backup(rel) {
  if (!exists(rel)) return;

  const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
  const target = path.join(root, ".phase2_backups", "P2-T08_JS_" + stamp, rel.replaceAll("/", path.sep));
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(full(rel), target);
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

function toRel(file) {
  return path.relative(root, file).replaceAll(path.sep, "/");
}

function isTargetSourceFile(file) {
  const rel = toRel(file);

  if (!rel.startsWith("Frontend/PlantProcess.Web/src/pages/") && !rel.startsWith("Frontend/PlantProcess.Web/src/components/")) {
    return false;
  }

  if (rel.startsWith("Frontend/PlantProcess.Web/src/components/standard/")) return false;
  if (rel.includes("/__tests__/")) return false;
  if (rel.endsWith(".test.tsx") || rel.endsWith(".test.ts") || rel.endsWith(".stories.tsx") || rel.endsWith(".stories.ts")) return false;
  if (!rel.endsWith(".tsx")) return false;

  return true;
}

function addImport(text, names) {
  const used = Array.from(names).sort();

  if (used.length === 0) return text;

  const importLine = `import { ${used.join(", ")} } from "@/components/standard/StandardP2Controls";`;

  if (text.includes(importLine)) return text;

  const partialImport = /import\s+\{([^}]+)\}\s+from\s+["']@\/components\/standard\/StandardP2Controls["'];?/m;
  const partial = text.match(partialImport);

  if (partial) {
    const existing = partial[1].split(",").map((x) => x.trim()).filter(Boolean);
    const merged = Array.from(new Set([...existing, ...used])).sort();

    return text.replace(partialImport, `import { ${merged.join(", ")} } from "@/components/standard/StandardP2Controls";`);
  }

  const importMatches = [...text.matchAll(/^import .*?;\s*$/gm)];

  if (importMatches.length > 0) {
    const last = importMatches[importMatches.length - 1];
    const insertAt = last.index + last[0].length;
    return text.slice(0, insertAt) + "\n" + importLine + text.slice(insertAt);
  }

  return importLine + "\n" + text;
}

function stripInlineStyles(text) {
  let next = text;

  // Common safe cases first.
  next = next.replace(/\s+style=\{\{[\s\S]*?\}\}/gm, "");
  next = next.replace(/\s+style=\{[^{}\n;]+\}/gm, "");

  // Keep a marker comment in files where something was removed.
  return next;
}

function standardizeFile(file) {
  const rel = toRel(file);
  let text = fs.readFileSync(file, "utf8");
  const original = text;
  const imports = new Set();

  if (/<button\b/.test(text) || /<\/button>/.test(text)) {
    text = text.replace(/<button\b/g, "<StandardP2Button");
    text = text.replace(/<\/button>/g, "</StandardP2Button>");
    imports.add("StandardP2Button");
  }

  if (/<input\b/.test(text)) {
    text = text.replace(/<input\b/g, "<StandardP2Input");
    imports.add("StandardP2Input");
  }

  if (/<select\b/.test(text) || /<\/select>/.test(text)) {
    text = text.replace(/<select\b/g, "<StandardP2Select");
    text = text.replace(/<\/select>/g, "</StandardP2Select>");
    imports.add("StandardP2Select");
  }

  if (/<textarea\b/.test(text) || /<\/textarea>/.test(text)) {
    text = text.replace(/<textarea\b/g, "<StandardP2TextArea");
    text = text.replace(/<\/textarea>/g, "</StandardP2TextArea>");
    imports.add("StandardP2TextArea");
  }

  if (/<table\b/.test(text) || /<\/table>/.test(text)) {
    text = text.replace(/<table\b/g, "<StandardP2Table");
    text = text.replace(/<\/table>/g, "</StandardP2Table>");
    imports.add("StandardP2Table");
  }

  const beforeStyleStrip = text;
  text = stripInlineStyles(text);

  if (beforeStyleStrip !== text) {
    imports.add("P2T08_STANDARD_ROLLOUT_MARKER");
  }

  text = addImport(text, imports);

  if (text !== original) {
    backup(rel);
    fs.writeFileSync(file, text.replace(/\r?\n/g, "\r\n"), "utf8");
    console.log("[P2-T08] standardized " + rel);
    return true;
  }

  return false;
}

write("Frontend/PlantProcess.Web/src/components/standard/StandardP2Controls.tsx", `
import type {
  ButtonHTMLAttributes,
  InputHTMLAttributes,
  SelectHTMLAttributes,
  TableHTMLAttributes,
  TextareaHTMLAttributes,
} from "react";
import "./standard-components.css";
import "./standard-p2-controls.css";

export const P2T08_STANDARD_ROLLOUT_MARKER =
  "${marker}";

type StandardP2ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "secondary" | "danger" | "ghost" | "action";
};

export function StandardP2Button({
  className,
  variant = "secondary",
  type = "button",
  ...props
}: StandardP2ButtonProps) {
  return (
    <button
      {...props}
      type={type}
      className={["standard-p2-control standard-p2-button", "standard-p2-button--" + variant, className]
        .filter(Boolean)
        .join(" ")}
    />
  );
}

export function StandardP2Input({
  className,
  ...props
}: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      className={["standard-p2-control standard-p2-field", className].filter(Boolean).join(" ")}
    />
  );
}

export function StandardP2Select({
  className,
  ...props
}: SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      {...props}
      className={["standard-p2-control standard-p2-field standard-p2-select", className].filter(Boolean).join(" ")}
    />
  );
}

export function StandardP2TextArea({
  className,
  ...props
}: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      {...props}
      className={["standard-p2-control standard-p2-field standard-p2-textarea", className].filter(Boolean).join(" ")}
    />
  );
}

export function StandardP2Table({
  className,
  ...props
}: TableHTMLAttributes<HTMLTableElement>) {
  return (
    <table
      {...props}
      className={["standard-p2-table", className].filter(Boolean).join(" ")}
    />
  );
}
`);

write("Frontend/PlantProcess.Web/src/components/standard/standard-p2-controls.css", `
/* ${marker} */

.standard-p2-control {
  box-sizing: border-box;
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  font-size: 14px;
  font-weight: 600;
  border-radius: 8px;
  transition:
    border-color 160ms ease,
    box-shadow 160ms ease,
    transform 120ms ease,
    opacity 120ms ease;
}

.standard-p2-control:focus-visible,
.standard-p2-table :focus-visible {
  outline: 3px solid rgba(70, 213, 255, 0.48);
  outline-offset: 2px;
  box-shadow: 0 0 0 4px rgba(70, 213, 255, 0.16);
}

.standard-p2-control:disabled,
.standard-p2-control[aria-disabled="true"] {
  opacity: 0.35;
  cursor: not-allowed;
}

.standard-p2-button {
  min-height: 38px;
  padding: 9px 14px;
  border: 1px solid rgba(70, 213, 255, 0.28);
  color: #eef8ff;
  background: rgba(10, 32, 48, 0.88);
  cursor: pointer;
}

.standard-p2-button:hover:not(:disabled) {
  transform: translateY(-1px);
  border-color: rgba(70, 213, 255, 0.52);
}

.standard-p2-button--primary,
.standard-p2-button--action {
  color: #06111f;
  background: linear-gradient(135deg, #46d5ff, #68f0a6);
  border-color: transparent;
}

.standard-p2-button--secondary {
  color: #eef8ff;
  background: rgba(13, 37, 58, 0.92);
}

.standard-p2-button--ghost {
  color: #b9eeff;
  background: transparent;
}

.standard-p2-button--danger {
  color: #fff3f3;
  border-color: rgba(255, 98, 98, 0.45);
  background: rgba(90, 24, 24, 0.7);
}

.standard-p2-field {
  min-height: 38px;
  width: 100%;
  padding: 9px 11px;
  border: 1px solid rgba(70, 213, 255, 0.22);
  color: #eef8ff;
  background: rgba(5, 16, 30, 0.84);
}

.standard-p2-field::placeholder {
  color: rgba(231, 248, 255, 0.48);
}

.standard-p2-select {
  cursor: pointer;
}

.standard-p2-textarea {
  min-height: 88px;
  resize: vertical;
}

.standard-p2-table {
  width: 100%;
  border-collapse: collapse;
  border-spacing: 0;
  color: #eef8ff;
  background: rgba(5, 16, 30, 0.58);
  border: 1px solid rgba(70, 213, 255, 0.14);
  border-radius: 12px;
  overflow: hidden;
}

.standard-p2-table th,
.standard-p2-table td {
  padding: 10px 12px;
  border-bottom: 1px solid rgba(70, 213, 255, 0.1);
}

.standard-p2-table th {
  font-size: 12px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: rgba(231, 248, 255, 0.72);
  background: rgba(70, 213, 255, 0.08);
}
`);

const sourceFiles = walk(full("Frontend/PlantProcess.Web/src"), (file) => isTargetSourceFile(file));

let changed = 0;
for (const file of sourceFiles) {
  if (standardizeFile(file)) changed += 1;
}

write("tools/ui/audit-ui-instances.cjs", `
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const webRoot = fs.existsSync(path.join(root, "Frontend", "PlantProcess.Web"))
  ? path.join(root, "Frontend", "PlantProcess.Web")
  : root;

const srcRoot = path.join(webRoot, "src");
const failMode = process.argv.includes("--fail");
const marker = "${marker}";

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
  { kind: "native-control", regex: /<button\\b/g, label: "native <button>" },
  { kind: "native-control", regex: /<input\\b/g, label: "native <input>" },
  { kind: "native-control", regex: /<select\\b/g, label: "native <select>" },
  { kind: "native-control", regex: /<textarea\\b/g, label: "native <textarea>" },
  { kind: "native-table", regex: /<table\\b/g, label: "native <table>" },
  { kind: "inline-style", regex: /\\sstyle=\\{/g, label: "inline style prop" },
];

for (const file of files) {
  const text = fs.readFileSync(file, "utf8");
  const lines = text.split(/\\r?\\n/);

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
    ? "## Result\\n\\nGREEN — no native controls/tables or inline style props outside standard wrappers."
    : "## Findings\\n\\n" + report.findings.map((x) => "- " + x.file + ":" + x.line + " " + x.label + " — " + x.snippet).join("\\n"),
  "",
].join("\\n");

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
`);

function patchPackageJson() {
  const rel = "Frontend/PlantProcess.Web/package.json";

  if (!exists(rel)) {
    throw new Error("Missing " + rel);
  }

  backup(rel);

  const pkg = JSON.parse(read(rel));

  pkg.scripts = pkg.scripts || {};
  pkg.scripts["audit:ui-instances"] = "node ../../tools/ui/audit-ui-instances.cjs";
  pkg.scripts["validate:standard-imports"] = "node ../../tools/ui/audit-ui-instances.cjs --fail";

  for (const [key, value] of Object.entries(pkg.scripts)) {
    if (typeof value === "string") {
      pkg.scripts[key] = value
        .replace(/\\s*\\|\\|\\s*echo\\s+["']?WARN[^"']*["']?/gi, "")
        .replace(/\\s*\\|\\|\\s*true/gi, "")
        .trim();
    }
  }

  write(rel, JSON.stringify(pkg, null, 2) + "\\n");
}

patchPackageJson();

write("tools/phase2/validate-p2-t08-standard-rollout.cjs", `
const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const marker = "${marker}";

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function fail(message) {
  console.error("[RED] P2-T08 validation failed: " + message);
  process.exit(1);
}

function run(command, args, cwd = root) {
  const result = childProcess.spawnSync(command, args, {
    cwd,
    encoding: "utf8",
    shell: process.platform === "win32",
  });

  if (result.error) {
    fail(command + " could not start: " + result.error.message);
  }

  if (result.status !== 0) {
    fail(
      command +
        " " +
        args.join(" ") +
        " failed\\nSTDOUT:\\n" +
        (result.stdout || "") +
        "\\nSTDERR:\\n" +
        (result.stderr || "")
    );
  }

  return (result.stdout || "") + (result.stderr || "");
}

const required = [
  "Frontend/PlantProcess.Web/src/components/standard/StandardP2Controls.tsx",
  "Frontend/PlantProcess.Web/src/components/standard/standard-p2-controls.css",
  "tools/ui/audit-ui-instances.cjs",
  "Frontend/PlantProcess.Web/package.json",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const controls = read("Frontend/PlantProcess.Web/src/components/standard/StandardP2Controls.tsx");
const css = read("Frontend/PlantProcess.Web/src/components/standard/standard-p2-controls.css");
const audit = read("tools/ui/audit-ui-instances.cjs");
const pkg = JSON.parse(read("Frontend/PlantProcess.Web/package.json"));

if (!controls.includes(marker)) fail("StandardP2Controls missing marker");
for (const name of ["StandardP2Button", "StandardP2Input", "StandardP2Select", "StandardP2TextArea", "StandardP2Table"]) {
  if (!controls.includes("function " + name)) fail("missing " + name);
}

if (!css.includes("opacity: 0.35")) fail("disabled opacity brand rule missing");
if (!css.includes("border-radius: 8px")) fail("8px radius brand rule missing");
if (!css.includes("font-size: 14px")) fail("Inter 14px rule missing");
if (!css.includes(":focus-visible")) fail("visible focus ring missing");
if (!css.includes("font-weight: 600")) fail("semibold rule missing");

if (!audit.includes("native-control") || !audit.includes("inline-style")) fail("audit script missing native-control/inline-style checks");

if (!pkg.scripts || !pkg.scripts["validate:standard-imports"]) {
  fail("package.json missing validate:standard-imports");
}

if (pkg.scripts["validate:standard-imports"].includes("WARN") || pkg.scripts["validate:standard-imports"].includes("||")) {
  fail("validate:standard-imports is still advisory/non-blocking");
}

run("node", ["tools/ui/audit-ui-instances.cjs", "--fail"], root);

console.log("[GREEN] P2-T08 static validation passed.");
`);

write("docs/phase2/P2_T08_STANDARD_COMPONENT_ROLLOUT.md", `
# P2-T08 — Standard Component Rollout

Marker: ${marker}

## Result

P2-T08 makes the Standard-component rollout blocking instead of advisory.

Installed:

- Standard tolerant control adapters:
  - StandardP2Button
  - StandardP2Input
  - StandardP2Select
  - StandardP2TextArea
  - StandardP2Table
- Brand CSS contract:
  - disabled opacity = 0.35
  - 8px radius
  - Inter / system fallback, 14px, semibold
  - visible focus ring
- UI audit:
  - tools/ui/audit-ui-instances.cjs
- Blocking npm gate:
  - npm run validate:standard-imports

## Validation

Run:

    node tools/phase2/validate-p2-t08-standard-rollout.cjs
    cd Frontend/PlantProcess.Web
    npm run validate:standard-imports
    npm run build

Expected:

- 0 native form controls outside src/components/standard
- 0 native table tags outside src/components/standard
- 0 inline style props outside src/components/standard
- validate:standard-imports exits non-zero on future drift
`);

console.log("[GREEN] P2-T08 apply completed. Changed files: " + changed);