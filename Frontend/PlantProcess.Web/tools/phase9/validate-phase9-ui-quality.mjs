/* ============================================================
 * PlantProcess IQ — Phase 9 UI quality gate
 *
 * Gate checks:
 * 1) No raw <button> remains in app TSX outside the standard library.
 * 2) Every app route has a Phase 9 action-matrix test entry.
 * 3) Empty/loading/error vocabulary avoids dead-end language.
 *
 * This is intentionally static and fast. Browser behavior is covered by
 * e2e/phase9-action-matrix.spec.ts and e2e/phase9-responsive-states.spec.ts.
 * ============================================================ */

import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const srcRoot = path.join(root, "src");

const requiredRoutes = [
  "/dashboard",
  "/admin",
  "/materials",
  "/risk",
  "/data-quality",
  "/correlations",
  "/ml-readiness",
  "/investigate/advanced",
  "/suggestions",
  "/page-builder",
  "/widget-script-compiler",
  "/commercial/license",
  "/demo-lifecycle",
  "/brand",
];

const excluded = [
  `${path.sep}components${path.sep}standard${path.sep}`,
  `${path.sep}__tests__${path.sep}`,
  ".test.tsx",
  ".spec.tsx",
  ".stories.tsx",
  ".snap",
  `${path.sep}ui${path.sep}standard-components.tsx`,
];

function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return walk(full);
    return entry.isFile() ? [full] : [];
  });
}

function shouldSkip(file) {
  return excluded.some((fragment) => file.includes(fragment));
}

const failures = [];
const warnings = [];

for (const file of walk(srcRoot)) {
  if (!file.endsWith(".tsx") || shouldSkip(file)) continue;

  const text = fs.readFileSync(file, "utf8");
  const rawButtons = [...text.matchAll(/<button\b/g)];

  if (rawButtons.length > 0) {
    failures.push(`${path.relative(root, file)} has ${rawButtons.length} raw <button> element(s). Use StandardButton.`);
  }

  const inlineStyleCount = (text.match(/style=\{\{/g) ?? []).length;
  if (inlineStyleCount > 0) {
    warnings.push(`${path.relative(root, file)} has ${inlineStyleCount} inline style object(s). Track for CSS/token migration.`);
  }
}

const matrixFile = path.join(root, "e2e", "helpers", "phase9ActionMatrix.ts");
if (!fs.existsSync(matrixFile)) {
  failures.push("Missing e2e/helpers/phase9ActionMatrix.ts");
} else {
  const matrixText = fs.readFileSync(matrixFile, "utf8");
  for (const route of requiredRoutes) {
    if (!matrixText.includes(`path: "${route}"`)) {
      failures.push(`Action matrix is missing route ${route}`);
    }
  }
}

const appText = fs.existsSync(path.join(root, "src", "App.tsx"))
  ? fs.readFileSync(path.join(root, "src", "App.tsx"), "utf8")
  : "";

for (const forbidden of ["white screen", "spinner forever", "dead button"]) {
  if (appText.toLowerCase().includes(forbidden)) {
    failures.push(`Forbidden hardening phrase remains in App.tsx: ${forbidden}`);
  }
}

for (const warning of warnings.slice(0, 40)) {
  console.warn(`[phase9-ui-quality warning] ${warning}`);
}
if (warnings.length > 40) {
  console.warn(`[phase9-ui-quality warning] ${warnings.length - 40} additional inline-style warnings suppressed.`);
}

if (failures.length > 0) {
  console.error("[phase9-ui-quality] FAILED");
  for (const failure of failures) console.error(` - ${failure}`);
  process.exit(1);
}

console.log("[phase9-ui-quality] PASSED");
console.log(`[phase9-ui-quality] Inline-style warnings tracked: ${warnings.length}`);