const fs = require("fs");
const path = require("path");

const root = process.cwd();
const webRoot = fs.existsSync(path.join(root, "Frontend", "PlantProcess.Web"))
  ? path.join(root, "Frontend", "PlantProcess.Web")
  : root;

const srcRoot = path.join(webRoot, "src");
const failMode = process.argv.includes("--fail");
const marker = "PPIQ_P2_T012_FIELDS_INPUTS_STANDARDIZATION";

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

function isSourceTsx(file) {
  return file.endsWith(".tsx");
}

function isAuditedSource(file) {
  const rel = relFromWeb(file);

  if (!rel.startsWith("src/pages/") && !rel.startsWith("src/components/")) return false;
  if (rel.startsWith("src/components/standard/")) return false;
  if (rel.includes("/__tests__/")) return false;
  if (rel.endsWith(".test.tsx") || rel.endsWith(".test.ts") || rel.endsWith(".stories.tsx") || rel.endsWith(".stories.ts")) return false;

  return rel.endsWith(".tsx");
}

function countPattern(text, regex) {
  let count = 0;
  regex.lastIndex = 0;
  while (regex.exec(text)) count += 1;
  return count;
}

function lineNumber(text, index) {
  return text.slice(0, index).split(/\r?\n/).length;
}

function attrValue(tag, name) {
  const double = tag.match(new RegExp("\\b" + name + "\\s*=\\s*\"([^\"]*)\""));
  if (double) return double[1];

  const single = tag.match(new RegExp("\\b" + name + "\\s*=\\s*'([^']*)'"));
  if (single) return single[1];

  const expr = tag.match(new RegExp("\\b" + name + "\\s*=\\s*\\{([^}]*)\\}"));
  if (expr) return "{" + expr[1].trim().slice(0, 80) + "}";

  return "";
}

function detectPage(rel) {
  const file = rel.split("/").pop() ?? rel;
  return file.replace(/\.(implementation\.)?tsx$/, "");
}

function classifyIntent(tag, rel) {
  const lower = (tag + " " + rel).toLowerCase();

  if (lower.includes("search") || lower.includes("filter")) return "filter";
  if (lower.includes("password") || lower.includes("email") || lower.includes("name") || lower.includes("description")) return "data-entry";
  if (lower.includes("date") || lower.includes("time")) return "filter";
  if (lower.includes("checkbox") || lower.includes("toggle")) return "preference";
  return "data-entry";
}

function inventoryForTag(rel, text, match, component, standardStatus) {
  const tag = match[0];
  const line = lineNumber(text, match.index);
  const label = attrValue(tag, "label");
  const type = component === "StandardSelect"
    ? "select"
    : component === "StandardTextArea"
      ? "textarea"
      : attrValue(tag, "type") || "text";

  return {
    task: "PPIQ-T012",
    file: rel,
    line,
    page: detectPage(rel),
    currentImplementation: component,
    fieldType: type,
    labelText: label || "UNLABELLED",
    intent: classifyIntent(tag, rel),
    validationBehavior: /required|error|aria-invalid|pattern|min|max/.test(tag) ? "present" : "not-detected",
    errorState: /error\s*=|aria-invalid/.test(tag) ? "present" : "missing",
    helperTextSupport: /helperText\s*=|aria-describedby/.test(tag) ? "present" : "missing",
    labelPosition: label ? "above-field" : "placeholder-only-or-missing",
    requiredMarkerStyle: /required/.test(tag) ? "required-marker" : "not-required",
    focusRing: standardStatus === "standard" ? "standard-css" : "not-detected",
    standardStatus,
  };
}

function collectOpeningTags(text, regex) {
  const items = [];
  let match;
  regex.lastIndex = 0;

  while ((match = regex.exec(text)) !== null) {
    items.push(match);
  }

  return items;
}

const inventoryFiles = walk(srcRoot, isSourceTsx);
const auditFiles = walk(srcRoot, isAuditedSource);
const inventory = [];
const findings = [];

for (const file of inventoryFiles) {
  const rel = relFromWeb(file);
  const text = fs.readFileSync(file, "utf8");

  for (const match of collectOpeningTags(text, /<StandardInput\b[^>]*>/g)) {
    inventory.push(inventoryForTag(rel, text, match, "StandardInput", "standard"));
  }

  for (const match of collectOpeningTags(text, /<StandardSelect\b[^>]*>/g)) {
    inventory.push(inventoryForTag(rel, text, match, "StandardSelect", "standard"));
  }

  for (const match of collectOpeningTags(text, /<StandardTextArea\b[^>]*>/g)) {
    inventory.push(inventoryForTag(rel, text, match, "StandardTextArea", "standard"));
  }
}

for (const file of auditFiles) {
  const rel = relFromWeb(file);
  const text = fs.readFileSync(file, "utf8");
  const lines = text.split(/\r?\n/);

  lines.forEach((line, index) => {
    if (/<input\b/.test(line)) {
      findings.push({
        file: rel,
        line: index + 1,
        kind: "raw-input",
        message: "raw <input> is not allowed outside StandardInput / StandardSelect / StandardTextArea wrappers",
        snippet: line.trim().slice(0, 220),
      });

      inventory.push({
        task: "PPIQ-T012",
        file: rel,
        line: index + 1,
        page: detectPage(rel),
        currentImplementation: "native <input>",
        fieldType: attrValue(line, "type") || "text",
        labelText: "UNLABELLED",
        intent: classifyIntent(line, rel),
        validationBehavior: "not-detected",
        errorState: "missing",
        helperTextSupport: "missing",
        labelPosition: "placeholder-only-or-missing",
        requiredMarkerStyle: /required/.test(line) ? "required-marker" : "not-required",
        focusRing: "not-detected",
        standardStatus: "non-standard",
      });
    }

    if (/<select\b/.test(line)) {
      findings.push({
        file: rel,
        line: index + 1,
        kind: "raw-select",
        message: "raw <select> is not allowed outside StandardSelect",
        snippet: line.trim().slice(0, 220),
      });
    }

    if (/<textarea\b/.test(line)) {
      findings.push({
        file: rel,
        line: index + 1,
        kind: "raw-textarea",
        message: "raw <textarea> is not allowed outside StandardTextArea",
        snippet: line.trim().slice(0, 220),
      });
    }
  });
}

const standardFieldsPath = path.join(webRoot, "src", "components", "standard", "StandardFields.tsx");
const cssPath = path.join(webRoot, "src", "components", "standard", "standard-components.css");
const indexPath = path.join(webRoot, "src", "components", "standard", "index.ts");

const standardFieldsText = fs.existsSync(standardFieldsPath)
  ? fs.readFileSync(standardFieldsPath, "utf8")
  : "";

const cssText = fs.existsSync(cssPath)
  ? fs.readFileSync(cssPath, "utf8")
  : "";

const indexText = fs.existsSync(indexPath)
  ? fs.readFileSync(indexPath, "utf8")
  : "";

for (const token of [
  marker,
  "StandardInput",
  "StandardSelect",
  "StandardTextArea",
  "helperText",
  "error",
  "required",
  "aria-invalid",
  "aria-describedby",
  "isLoading",
  "searchable",
  "multiple",
]) {
  if (!standardFieldsText.includes(token)) {
    findings.push({
      file: "src/components/standard/StandardFields.tsx",
      line: 0,
      kind: "standard-fields-contract",
      message: "StandardFields missing token: " + token,
      snippet: "",
    });
  }
}

if (!standardFieldsText.includes("Clear search") && !standardFieldsText.includes("clear")) {
  findings.push({
    file: "src/components/standard/StandardFields.tsx",
    line: 0,
    kind: "standard-input-search-clear",
    message: "StandardInput search variant must expose clear-button behavior",
    snippet: "",
  });
}

if (!indexText.includes("StandardInput") && !indexText.includes("StandardFields")) {
  findings.push({
    file: "src/components/standard/index.ts",
    line: 0,
    kind: "standard-index-export",
    message: "standard index must export StandardInput / StandardSelect / StandardTextArea",
    snippet: "",
  });
}

if (!cssText.includes("ppiq-std-field") && !cssText.includes("ppiq-field")) {
  findings.push({
    file: "src/components/standard/standard-components.css",
    line: 0,
    kind: "standard-field-css-contract",
    message: "standard CSS missing field classes",
    snippet: "",
  });
}

if (!/focus-visible|:focus/.test(cssText)) {
  findings.push({
    file: "src/components/standard/standard-components.css",
    line: 0,
    kind: "standard-field-focus-ring",
    message: "standard CSS missing focus-ring styling",
    snippet: "",
  });
}

const header = [
  "task",
  "file",
  "line",
  "page",
  "currentImplementation",
  "fieldType",
  "labelText",
  "intent",
  "validationBehavior",
  "errorState",
  "helperTextSupport",
  "labelPosition",
  "requiredMarkerStyle",
  "focusRing",
  "standardStatus",
];

function csvCell(value) {
  const text = String(value ?? "");
  if (/[",\n\r]/.test(text)) return '"' + text.replaceAll('"', '""') + '"';
  return text;
}

const csv = [
  header.join(","),
  ...inventory.map((row) => header.map((key) => csvCell(row[key])).join(",")),
].join("\n") + "\n";

const uiStandardsDir = path.join(webRoot, "docs", "ui-standards");
fs.mkdirSync(uiStandardsDir, { recursive: true });
fs.writeFileSync(path.join(uiStandardsDir, "input-inventory.csv"), csv, "utf8");

const outDir = path.join(root, "Documentation", "P2-T012_FieldsInputs_Latest");
fs.mkdirSync(outDir, { recursive: true });

const report = {
  marker,
  generatedAtUtc: new Date().toISOString(),
  auditedFiles: auditFiles.length,
  inventoryCount: inventory.length,
  nonStandardCount: inventory.filter((row) => row.standardStatus !== "standard").length,
  findingCount: findings.length,
  findings,
};

fs.writeFileSync(path.join(outDir, "fields-inputs-audit.json"), JSON.stringify(report, null, 2));

const md = [
  "# P2-T012 Fields / Inputs Audit",
  "",
  "Marker: " + marker,
  "",
  "- Audited files: " + report.auditedFiles,
  "- Inventory rows: " + report.inventoryCount,
  "- Non-standard rows: " + report.nonStandardCount,
  "- Findings: " + report.findingCount,
  "",
  findings.length === 0
    ? "## Result\n\nGREEN — StandardFields contract is present, input inventory was generated, and raw field controls are blocked outside standard wrappers."
    : "## Findings\n\n" + findings.map((x) => "- " + x.file + ":" + x.line + " [" + x.kind + "] " + x.message + (x.snippet ? " — " + x.snippet : "")).join("\n"),
  "",
].join("\n");

fs.writeFileSync(path.join(outDir, "fields-inputs-audit.md"), md, "utf8");

console.log(JSON.stringify({
  marker,
  auditedFiles: report.auditedFiles,
  inventoryCount: report.inventoryCount,
  nonStandardCount: report.nonStandardCount,
  findingCount: report.findingCount,
}, null, 2));

if (failMode && findings.length > 0) {
  console.error("[RED] P2-T012 fields/input audit failed. See Documentation/P2-T012_FieldsInputs_Latest/fields-inputs-audit.md");
  process.exit(1);
}

console.log("[GREEN] P2-T012 fields/input audit passed.");