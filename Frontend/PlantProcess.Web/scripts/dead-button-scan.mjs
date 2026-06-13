#!/usr/bin/env node
/* PPIQ Phase-4 dead-button scan. Walks src/pages + src/components and flags
 * interactive handlers that do nothing real, then writes DEAD_BUTTON_INVENTORY.md.
 * Exit 1 if any are found (so it gates P4-T02 / "lint shows zero").
 * Usage: node scripts/dead-button-scan.mjs [srcRoot=src] [out=DEAD_BUTTON_INVENTORY.md]
 */
import { readdirSync, readFileSync, writeFileSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";

const SRC = process.argv[2] || "src";
const OUT = process.argv[3] || "DEAD_BUTTON_INVENTORY.md";
const SCAN = ["pages", "components", "features"].map((d) => join(SRC, d));

function walk(dir, acc = []) {
  let names; try { names = readdirSync(dir); } catch { return acc; }
  for (const name of names) {
    if (name === "node_modules") continue;
    const full = join(dir, name);
    let st; try { st = statSync(full); } catch { continue; }
    if (st.isDirectory()) walk(full, acc);
    else if (/\.tsx$/.test(name) && !/\.test\.tsx$|\.stories\.tsx$/.test(name) && !full.includes(`${sep}__tests__${sep}`)) acc.push(full);
  }
  return acc;
}
const rel = (f) => relative(process.cwd(), f).split(sep).join("/");

// classification patterns over a single handler attribute occurrence
const PATTERNS = [
  { kind: "empty handler", re: /on(?:Click|Submit|Change)\s*=\s*\{\s*\(\s*[^)]*\)\s*=>\s*\{\s*\}\s*\}/g },
  { kind: "noop arrow", re: /on(?:Click|Submit)\s*=\s*\{\s*\(\s*[^)]*\)\s*=>\s*(?:undefined|null|void 0)\s*\}/g },
  { kind: "log-only", re: /on(?:Click|Submit)\s*=\s*\{\s*\(\s*[^)]*\)\s*=>\s*console\.(?:log|debug|info)\s*\([^)]*\)\s*\}/g },
  { kind: "alert-only", re: /on(?:Click|Submit)\s*=\s*\{\s*\(\s*[^)]*\)\s*=>\s*(?:window\.)?alert\s*\([^)]*\)\s*\}/g },
];
const COMING_SOON = /(coming soon|not implemented|TODO:?\s*(wire|implement|hook)|placeholder onClick)/i;

const files = SCAN.flatMap((d) => walk(d));
const rows = [];
for (const f of files) {
  const text = readFileSync(f, "utf8");
  const lines = text.split(/\r?\n/);
  lines.forEach((line, i) => {
    for (const p of PATTERNS) {
      p.re.lastIndex = 0;
      if (p.re.test(line)) rows.push({ file: rel(f), line: i + 1, kind: p.kind, snippet: line.trim().slice(0, 120) });
    }
    if (COMING_SOON.test(line) && /<(button|a|StandardButton)\b/i.test(line)) {
      rows.push({ file: rel(f), line: i + 1, kind: "coming-soon stub", snippet: line.trim().slice(0, 120) });
    }
  });
}

const header = `# Dead-button inventory\n\nGenerated ${new Date().toISOString()} over ${files.length} component files.\n\n` +
  `Target: every interactive control does something real on the demo dataset (A3#1/#2/#10).\n` +
  `Fix each row in P4-T02 (wire to a real handler/endpoint, or remove from the demo build).\n\n` +
  (rows.length === 0
    ? "No dead/stub handlers found. \n"
    : `| File | Line | Classification | Snippet |\n|---|---|---|---|\n` +
      rows.map((r) => `| ${r.file} | ${r.line} | ${r.kind} | \`${r.snippet.replace(/\|/g, "\\|")}\` |`).join("\n") + "\n");

writeFileSync(OUT, header, "utf8");
console.log(`Dead-button scan: ${rows.length} flagged handler(s) across ${files.length} files -> ${OUT}`);
if (rows.length) { for (const r of rows) console.error(`  ${r.file}:${r.line}  ${r.kind}`); process.exit(1); }