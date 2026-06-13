#!/usr/bin/env node
/*
 * PlantProcess IQ - T-041  Golden-Rule guard: no hardcoded demo pages
 *
 * The Golden Rule: demo pages are authored through the HMI (page-builder ->
 * backend PageDefinition -> rendered by DynamicPage at /pages/:slug). A demo
 * page that is a hardcoded React component wired as its own route violates it.
 *
 * This scan walks the frontend source and FAILS (exit 1) if any hardcoded
 * demo page module is imported/routed. It PASSES (exit 0) when demo content is
 * served only via the dynamic slug route. DynamicPage itself is allowed.
 *
 * Usage:
 *   node scripts/verify-no-hardcoded-demo-pages.mjs [srcRoot]
 *   (default srcRoot: Frontend/PlantProcess.Web/src)
 */
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";

const srcRoot = process.argv[2] || "Frontend/PlantProcess.Web/src";

// Hardcoded demo-page module path fragments that must NOT be imported/routed.
// (Normalised to forward slashes before matching.)
const DENY_PATH_FRAGMENTS = [
  // Customer demo CONTENT must be HMI-authored (PageDefinition -> DynamicPage at /pages/:slug),
  // never a coded React route. Any module under pages/Demo* is a Golden-Rule violation.
  // (Platform/operator surfaces - reset console, evidence/compiler, suggestions - were reclassified
  //  to pages/PlatformOps; they are not customer demo content.)
  "pages/Demo",
];

// Modules that look demo-ish but are legitimate (the dynamic renderer, contexts).
const ALLOW_PATH_FRAGMENTS = [
  "pages/DynamicPage",
  "state/DemoModeContext",
];

function walk(dir) {
  const out = [];
  let entries;
  try {
    entries = readdirSync(dir);
  } catch {
    return out;
  }
  for (const name of entries) {
    if (name === "node_modules" || name.startsWith(".")) continue;
    const full = join(dir, name);
    const st = statSync(full);
    if (st.isDirectory()) out.push(...walk(full));
    else if (/\.(tsx?|jsx?)$/.test(name)) out.push(full);
  }
  return out;
}

function isAllowed(spec) {
  return ALLOW_PATH_FRAGMENTS.some((f) => spec.includes(f));
}

function offendingFragment(spec) {
  if (isAllowed(spec)) return null;
  return DENY_PATH_FRAGMENTS.find((f) => spec.includes(f)) || null;
}

// Match the module specifier in: import("..."), from "...", require("...")
const SPEC_RE = /(?:import\s*\(\s*|from\s+|require\s*\(\s*)["'`]([^"'`]+)["'`]/g;

const files = walk(srcRoot);
const violations = [];

for (const file of files) {
  const text = readFileSync(file, "utf8");
  const lines = text.split(/\r?\n/);
  lines.forEach((line, idx) => {
    let m;
    SPEC_RE.lastIndex = 0;
    while ((m = SPEC_RE.exec(line)) !== null) {
      const spec = m[1].replace(/\\/g, "/");
      const frag = offendingFragment(spec);
      if (frag) {
        violations.push({
          file: relative(process.cwd(), file).split(sep).join("/"),
          line: idx + 1,
          spec,
          fragment: frag,
        });
      }
    }
  });
}

if (files.length === 0) {
  console.error(`[no-hardcoded-demo-pages] No source files found under '${srcRoot}'. Check the path.`);
  process.exit(2);
}

if (violations.length > 0) {
  console.error("[no-hardcoded-demo-pages] FAIL - hardcoded demo pages are routed/imported.");
  console.error("  The Golden Rule requires demo pages to be HMI-authored (slug -> DynamicPage).");
  console.error("  Replace each with a backend PageDefinition served at /pages/:slug, then remove the import.\n");
  for (const v of violations) {
    console.error(`  ${v.file}:${v.line}  imports '${v.spec}'  (matches '${v.fragment}')`);
  }
  console.error(`\n  ${violations.length} violation(s) across ${new Set(violations.map((v) => v.file)).size} file(s). Scanned ${files.length} files.`);
  process.exit(1);
}

console.log(`[no-hardcoded-demo-pages] PASS - no hardcoded demo pages imported. Scanned ${files.length} files under '${srcRoot}'.`);
process.exit(0);
