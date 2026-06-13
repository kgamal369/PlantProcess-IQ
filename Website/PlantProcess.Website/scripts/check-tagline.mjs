/* PPIQ-PHASE7 tagline drift guard. Run: node scripts/check-tagline.mjs */
import fs from "node:fs";
import path from "node:path";

const CANONICAL = "Connect Your Plant Data. Understand Your Process.";
const DRIFT = [
  "Connect plant data. Understand your process. Act with evidence.",
  "Connect plant data. Understand your process.",
];
const ROOT = path.join(process.cwd(), "src");
const EXT = new Set([".ts", ".tsx", ".js", ".jsx", ".mjs", ".html"]);

function walk(dir) {
  let out = [];
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (e.name === "node_modules" || e.name === "dist") continue;
    const full = path.join(dir, e.name);
    if (e.isDirectory()) out = out.concat(walk(full));
    else if (EXT.has(path.extname(e.name))) out.push(full);
  }
  return out;
}

const files = fs.existsSync(ROOT) ? walk(ROOT) : [];
let drift = 0;
let canonicalSeen = 0;
for (const f of files) {
  const t = fs.readFileSync(f, "utf8");
  if (t.includes(CANONICAL)) canonicalSeen++;
  for (const d of DRIFT) {
    if (t.includes(d)) { drift++; console.error(`  DRIFT  ${path.relative(process.cwd(), f)} -> "${d}"`); }
  }
}
if (drift > 0) { console.error(`FAIL: ${drift} drifted tagline(s) found.`); process.exit(1); }
if (canonicalSeen === 0) { console.error("FAIL: canonical tagline not found anywhere in src."); process.exit(1); }
console.log(`OK: tagline canonical (${canonicalSeen} surface(s)), zero drift.`);