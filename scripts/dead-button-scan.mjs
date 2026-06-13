/* P4-T02: flag dead/stub interactive controls in the HMI - handlers that are
 * empty, console-only, or alert-only, plus visible "coming soon" stubs. Writes a
 * Markdown inventory and exits non-zero if any are found (a blocking lint gate). */
import { readFileSync, writeFileSync, readdirSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const srcRoot = process.argv[2] || "src";
const OUT = process.argv[3] || "DEAD_BUTTON_INVENTORY.md";

function walk(dir) {
  const out = [];
  for (const name of readdirSync(dir)) {
    if (name === "node_modules" || name.startsWith(".")) continue;
    const full = join(dir, name);
    const st = statSync(full);
    if (st.isDirectory()) out.push(...walk(full));
    else if (/\.(tsx|ts|jsx|js)$/.test(name) && !/\.(test|spec)\./.test(name)) out.push(full);
  }
  return out;
}

const RULES = [
  { id: "empty-handler",   re: /on[A-Z]\w*=\{\s*\(\s*\)\s*=>\s*\{\s*\}\s*\}/g, why: "empty handler () => {}" },
  { id: "console-only",    re: /on[A-Z]\w*=\{\s*\(\s*[^)]*\)\s*=>\s*console\.\w+\([^}]*\)\s*\}/g, why: "console-only handler" },
  { id: "alert-only",      re: /on[A-Z]\w*=\{\s*\(\s*[^)]*\)\s*=>\s*alert\([^}]*\)\s*\}/g, why: "alert-only handler" },
  { id: "coming-soon",     re: /coming soon/gi, why: "visible 'coming soon' stub" },
  { id: "todo-handler",    re: /on[A-Z]\w*=\{[^}]*\/\/\s*TODO[^}]*\}/g, why: "TODO placeholder in handler" },
];

const files = walk(srcRoot);
const findings = [];
for (const file of files) {
  const text = readFileSync(file, "utf8");
  const lines = text.split(/\r?\n/);
  for (const rule of RULES) {
    let m;
    const re = new RegExp(rule.re.source, rule.re.flags);
    while ((m = re.exec(text)) !== null) {
      const line = text.slice(0, m.index).split(/\r?\n/).length;
      findings.push({ file: relative(process.cwd(), file).replace(/\\/g, "/"), line, rule: rule.id, why: rule.why, snippet: (lines[line - 1] || "").trim().slice(0, 120) });
    }
  }
}

let md = "# Dead / stub control inventory (P4-T02)\n\n";
md += `Scanned ${files.length} files under \`${srcRoot}\`. Found ${findings.length} candidate(s).\n\n`;
if (findings.length) {
  md += "| File | Line | Rule | Snippet |\n|---|---|---|---|\n";
  for (const f of findings) md += `| ${f.file} | ${f.line} | ${f.rule} | \`${f.snippet.replace(/\|/g, "\\|")}\` |\n`;
} else {
  md += "No dead/stub controls detected. P4-T02 acceptance (1) satisfied.\n";
}
writeFileSync(OUT, md, "utf8");

if (findings.length) {
  console.error(`[dead-button-scan] ${findings.length} dead/stub control(s) across ${new Set(findings.map((f) => f.file)).size} file(s). See ${OUT}.`);
  for (const f of findings.slice(0, 25)) console.error(`  ${f.file}:${f.line}  ${f.why}  ${f.snippet}`);
  process.exit(1);
} else {
  console.log(`[dead-button-scan] PASS - no dead/stub controls. Scanned ${files.length} files. Wrote ${OUT}.`);
}