const fs = require("fs");
const path = require("path");

const root = process.cwd();
const webRoot = fs.existsSync(path.join(root, "Frontend", "PlantProcess.Web"))
  ? path.join(root, "Frontend", "PlantProcess.Web")
  : root;

const srcRoot = path.join(webRoot, "src");
const failMode = process.argv.includes("--fail");
const marker = "PPIQ_P2_T09_ACTION_BUTTON_STANDARDIZATION";

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

function findOpeningTagEnd(text, start) {
  let quote = null;
  let braceDepth = 0;

  for (let i = start; i < text.length; i += 1) {
    const ch = text[i];

    if (quote) {
      if (ch === "\\" && i + 1 < text.length) {
        i += 1;
        continue;
      }

      if (ch === quote) {
        quote = null;
      }

      continue;
    }

    if (ch === "\"" || ch === "'" || ch === "`") {
      quote = ch;
      continue;
    }

    if (ch === "{") {
      braceDepth += 1;
      continue;
    }

    if (ch === "}") {
      if (braceDepth > 0) braceDepth -= 1;
      continue;
    }

    if (ch === ">" && braceDepth === 0) {
      return i;
    }
  }

  return -1;
}

function collectStandardButtonTags(text) {
  const tags = [];
  let index = 0;

  while (index < text.length) {
    const start = text.indexOf("<StandardButton", index);

    if (start < 0) break;

    const end = findOpeningTagEnd(text, start);

    if (end < 0) break;

    tags.push({
      tag: text.slice(start, end + 1),
      line: text.slice(0, start).split(/\r?\n/).length,
    });

    index = end + 1;
  }

  return tags;
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
    if (/<button\b/.test(line)) {
      findings.push({
        file: rel,
        line: index + 1,
        kind: "raw-button",
        message: "raw <button> is not allowed outside canonical StandardButton wrappers",
        snippet: line.trim().slice(0, 220),
      });
    }

    if (/StandardP2Button/.test(line) && rel.includes("MaterialInvestigation")) {
      findings.push({
        file: rel,
        line: index + 1,
        kind: "material-standard-p2-button",
        message: "Material Investigation must use canonical StandardButton, not StandardP2Button",
        snippet: line.trim().slice(0, 220),
      });
    }
  });

  for (const item of collectStandardButtonTags(text)) {
    if (/\sdisabled(\s|=|\/?>)/.test(item.tag)) {
      findings.push({
        file: rel,
        line: item.line,
        kind: "standard-button-disabled-prop",
        message: "StandardButton callers must use isDisabled, not disabled",
        snippet: item.tag.replace(/\s+/g, " ").slice(0, 220),
      });
    }
  }
}

const standardButtonPath = path.join(webRoot, "src", "components", "standard", "StandardButton.tsx");
const standardCssPath = path.join(webRoot, "src", "components", "standard", "standard-components.css");
const visualSpecPath = path.join(webRoot, "e2e", "p2-t09-action-button-visual.spec.ts");

const buttonText = fs.existsSync(standardButtonPath) ? fs.readFileSync(standardButtonPath, "utf8") : "";
const cssText = fs.existsSync(standardCssPath) ? fs.readFileSync(standardCssPath, "utf8") : "";
const visualText = fs.existsSync(visualSpecPath) ? fs.readFileSync(visualSpecPath, "utf8") : "";

for (const token of [
  marker,
  "isLoading",
  "isDisabled",
  "loadingLabel",
  "aria-busy",
  "data-loading",
  "ppiq-std-button__spinner",
  "StandardButtonVariant",
  "action",
]) {
  if (!buttonText.includes(token)) {
    findings.push({
      file: "src/components/standard/StandardButton.tsx",
      line: 0,
      kind: "standard-button-contract",
      message: "StandardButton missing token: " + token,
      snippet: "",
    });
  }
}

for (const token of [
  marker,
  "ppiq-std-button--action",
  "ppiq-std-button--is-loading",
  "opacity: 0.35",
  "focus-visible",
  "ppiq-p2t09-material-button",
]) {
  if (!cssText.includes(token)) {
    findings.push({
      file: "src/components/standard/standard-components.css",
      line: 0,
      kind: "button-css-contract",
      message: "Standard button CSS missing token: " + token,
      snippet: "",
    });
  }
}

const materialFiles = files.filter((file) => relFromWeb(file).includes("MaterialInvestigation"));
const materialText = materialFiles.map((file) => fs.readFileSync(file, "utf8")).join("\n");

if (materialFiles.length === 0) {
  findings.push({
    file: "src/pages/MaterialInvestigation",
    line: 0,
    kind: "material-button-contract",
    message: "Material Investigation source files were not found",
    snippet: "",
  });
} else {
  if (!materialText.includes("StandardButton")) {
    findings.push({
      file: "src/pages/MaterialInvestigation",
      line: 0,
      kind: "material-button-contract",
      message: "Material Investigation must use StandardButton",
      snippet: "",
    });
  }

  if (materialText.includes("<StandardP2Button")) {
    findings.push({
      file: "src/pages/MaterialInvestigation",
      line: 0,
      kind: "material-button-contract",
      message: "Material Investigation still uses StandardP2Button",
      snippet: "",
    });
  }

  if (!materialText.includes("ppiq-p2t09-material-button")) {
    findings.push({
      file: "src/pages/MaterialInvestigation",
      line: 0,
      kind: "material-button-styling",
      message: "Material Investigation must use P2-T09 material button styling class",
      snippet: "",
    });
  }

  if (!materialText.includes("variant=\"primary\"") || !materialText.includes("variant=\"secondary\"")) {
    findings.push({
      file: "src/pages/MaterialInvestigation",
      line: 0,
      kind: "material-button-hierarchy",
      message: "Material Investigation must contain primary and secondary StandardButton hierarchy",
      snippet: "",
    });
  }
}

if (!visualText.includes("representativeRoutes") || !visualText.includes("Material Investigation")) {
  findings.push({
    file: "e2e/p2-t09-action-button-visual.spec.ts",
    line: 0,
    kind: "visual-regression-contract",
    message: "P2-T09 visual spec must cover Material Investigation and representative routes",
    snippet: "",
  });
}

const report = {
  marker,
  generatedAtUtc: new Date().toISOString(),
  auditedFiles: files.length,
  materialFiles: materialFiles.map(relFromWeb),
  findingCount: findings.length,
  findings,
};

const outDir = path.join(root, "Documentation", "P2-T09_ActionButtons_Latest");
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, "action-button-audit.json"), JSON.stringify(report, null, 2));

const md = [
  "# P2-T09 Action Button Audit",
  "",
  "Marker: " + marker,
  "",
  "- Audited files: " + report.auditedFiles,
  "- Material files: " + report.materialFiles.length,
  "- Findings: " + report.findingCount,
  "",
  findings.length === 0
    ? "## Result\n\nGREEN — action-button hierarchy, Material Investigation styling, loading/disabled semantics, and raw-button guard are clean."
    : "## Findings\n\n" + findings.map((x) => "- " + x.file + ":" + x.line + " [" + x.kind + "] " + x.message + (x.snippet ? " — " + x.snippet : "")).join("\n"),
  "",
].join("\n");

fs.writeFileSync(path.join(outDir, "action-button-audit.md"), md);

console.log(JSON.stringify({
  marker,
  auditedFiles: report.auditedFiles,
  materialFiles: report.materialFiles.length,
  findingCount: report.findingCount,
}, null, 2));

if (failMode && findings.length > 0) {
  console.error("[RED] P2-T09 action button audit failed. See Documentation/P2-T09_ActionButtons_Latest/action-button-audit.md");
  process.exit(1);
}

console.log("[GREEN] P2-T09 action button audit passed.");