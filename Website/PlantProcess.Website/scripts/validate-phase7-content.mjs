#!/usr/bin/env node
/* PPIQ-PHASE7 site validator. Dependency-free. Run: node scripts/validate-phase7-content.mjs
 * Exits non-zero on any failure so CI can gate on it. Mirrors the existing
 * validate-website-content.mjs forbidden[] contract and extends it to the new pages. */
import fs from "node:fs";
import path from "node:path";

const ROOT = path.join(process.cwd(), "src");
const EXT = new Set([".ts", ".tsx", ".js", ".jsx", ".html"]);

// Forbidden CLAIMS (rendered copy). These mirror the site's own honesty-lint.
const FORBIDDEN = [
  /fully autonomous root cause/i,
  /automatic root cause proof/i,
  /replaces mes/i,
  /replaces scada/i,
  /replaces level 2/i,
  /controls plc/i,
  /writes back to plc/i,
  /guaranteed root cause/i,
];
// Approved vocabulary that must appear somewhere in the product copy.
const APPROVED = [/read-only/i, /suspected contributor/i, /rule-based risk/i, /evidence/i];
// Palette tokens that must be present (brand fidelity).
const PALETTE = ["#050B18", "#0B1730", "#00D4FF", "#0A84FF", "#2CE6A2", "#FFB020", "#FF4D6D"];
// Canonical tagline.
const TAGLINE = "Connect Your Plant Data. Understand Your Process.";
const TAGLINE_DRIFT = [
  "Connect plant data. Understand your process. Act with evidence.",
  "Connect plant data. Understand your process.",
];

function walk(dir) {
  let out = [];
  if (!fs.existsSync(dir)) return out;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    if (e.name === "node_modules" || e.name === "dist") continue;
    const full = path.join(dir, e.name);
    if (e.isDirectory()) out = out.concat(walk(full));
    else if (EXT.has(path.extname(e.name))) out.push(full);
  }
  return out;
}

// Strip line comments + block comments so a forbidden pattern that only exists
// as a lint definition or code comment is not counted as a rendered claim.
function stripComments(s) {
  return s.replace(/\/\*[\s\S]*?\*\//g, " ").replace(/(^|\s)\/\/.*$/gm, " ");
}

const files = walk(ROOT);
const allCopy = files.map((f) => stripComments(fs.readFileSync(f, "utf8"))).join("\n");
const allRaw = files.map((f) => fs.readFileSync(f, "utf8")).join("\n");

let failures = 0;
const fail = (m) => { console.error("  FAIL  " + m); failures++; };
const pass = (m) => console.log("  ok    " + m);

// 1) forbidden claims absent (in rendered copy, comments stripped)
for (const p of FORBIDDEN) {
  if (p.test(allCopy)) {
    // show offending files for fast triage
    const hits = files.filter((f) => p.test(stripComments(fs.readFileSync(f, "utf8"))))
                      .map((f) => path.relative(process.cwd(), f));
    fail(`forbidden claim present ${p} -> ${hits.join(", ")}`);
  } else pass(`forbidden claim absent ${p}`);
}

// 2) approved vocabulary present
for (const p of APPROVED) {
  if (p.test(allCopy)) pass(`approved vocab present ${p}`);
  else fail(`approved vocab missing ${p}`);
}

// 3) palette tokens present
for (const hex of PALETTE) {
  if (allRaw.includes(hex)) pass(`palette token present ${hex}`);
  else fail(`palette token missing ${hex}`);
}

// 4) tagline canonical, no drift
if (allRaw.includes(TAGLINE)) pass("canonical tagline present");
else fail("canonical tagline missing");
for (const d of TAGLINE_DRIFT) {
  if (allRaw.includes(d)) fail(`tagline drift present -> "${d}"`);
}

// 5) both new products carry all Golden-Rule sections
const PRODUCTS = ["yardWarehouse", "mes"];
for (const id of PRODUCTS) {
  const f = path.join(ROOT, "content", "products", id + ".ts");
  if (!fs.existsSync(f)) { fail(`product file missing ${id}.ts`); continue; }
  const t = fs.readFileSync(f, "utf8");
  for (const sec of ["headline", "subTagline", "problem", "capabilities", "benefits", "diagram", "licensing", "cta", "evidencePosture"]) {
    if (t.includes(sec + ":")) pass(`${id}: section ${sec}`);
    else fail(`${id}: section ${sec} missing`);
  }
}

console.log("");
if (failures > 0) { console.error(`Phase-7 content validation FAILED with ${failures} issue(s).`); process.exit(1); }
console.log("Phase-7 content validation passed.");