#!/usr/bin/env node
// Flags StandardButton callers that are inert: no handler and not disabled
// (standard-looking dead controls). Scope mirrors audit-action-buttons.cjs.
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const webRoot = fs.existsSync(path.join(root, "Frontend", "PlantProcess.Web"))
  ? path.join(root, "Frontend", "PlantProcess.Web")
  : root;
const srcRoot = path.join(webRoot, "src");
const failMode = process.argv.includes("--fail");

function relFromWeb(file) {
  return path.relative(webRoot, file).split(path.sep).join("/");
}

function walk(dir, predicate) {
  const out = [];
  if (!fs.existsSync(dir)) return out;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const item = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (["node_modules", "dist", "coverage", "playwright-report", "test-results", "__snapshots__"].includes(entry.name)) continue;
      out.push(...walk(item, predicate));
    } else if (!predicate || predicate(item)) {
      out.push(item);
    }
  }
  return out;
}

function isAuditedSource(file) {
  const rel = relFromWeb(file);
  if (!rel.startsWith("src/pages/") && !rel.startsWith("src/components/")) return false;
  if (rel.startsWith("src/components/standard/")) return false;
  if (rel.includes("/__tests__/")) return false;
  if (rel.endsWith(".test.tsx") || rel.endsWith(".test.ts") || rel.endsWith(".stories.tsx") || rel.endsWith(".stories.ts")) return false;
  return rel.endsWith(".tsx");
}

function findOpeningTagEnd(text, start) {
  let quote = null;
  let depth = 0;
  for (let i = start; i < text.length; i += 1) {
    const ch = text[i];
    if (quote) {
      if (ch === "\\" && i + 1 < text.length) { i += 1; continue; }
      if (ch === quote) quote = null;
      continue;
    }
    if (ch === '"' || ch === "'" || ch === "`") { quote = ch; continue; }
    if (ch === "{") { depth += 1; continue; }
    if (ch === "}") { if (depth > 0) depth -= 1; continue; }
    if (ch === ">" && depth === 0) return i;
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
    tags.push({ tag: text.slice(start, end + 1), line: text.slice(0, start).split(/\r?\n/).length });
    index = end + 1;
  }
  return tags;
}

const files = walk(srcRoot, isAuditedSource);
const findings = [];

for (const file of files) {
  const rel = relFromWeb(file);
  const text = fs.readFileSync(file, "utf8");
  for (const item of collectStandardButtonTags(text)) {
    const tag = item.tag;
    const wired =
      /\bonClick\b/.test(tag) ||
      /\bhref\b/.test(tag) ||
      /\bto\s*=/.test(tag) ||
      /\bonSubmit\b/.test(tag) ||
      /\btype\s*=\s*["']submit["']/.test(tag) ||
      /\{\s*\.\.\./.test(tag);
    const inert = !wired && !/\bisDisabled\b/.test(tag) && !/\bisLoading\b/.test(tag);
    if (inert) {
      findings.push({
        file: rel,
        line: item.line,
        kind: "inert-standard-button",
        message: "StandardButton has no handler and is not disabled (dead control). Wire a handler, or set isDisabled with a data-disabled-reason.",
        snippet: tag.replace(/\s+/g, " ").slice(0, 200),
      });
    }
  }
}

const summary = { auditedFiles: files.length, findingCount: findings.length };
console.log(JSON.stringify(summary, null, 2));
for (const f of findings) {
  console.log("  " + f.file + ":" + f.line + "  " + f.snippet);
}

const outDir = path.join(webRoot, "docs", "ui-standards");
try {
  fs.mkdirSync(outDir, { recursive: true });
  fs.writeFileSync(path.join(outDir, "inert-standard-buttons.json"), JSON.stringify({ summary, findings }, null, 2) + "\n", "utf8");
} catch (e) {}

if (findings.length === 0) {
  console.log("[GREEN] No inert StandardButton controls.");
} else {
  console.log("[" + (failMode ? "RED" : "WARN") + "] " + findings.length + " inert StandardButton control(s).");
}

process.exit(failMode && findings.length > 0 ? 1 : 0);