/* ============================================================
 * PlantProcess IQ — Phase 9 button standardization codemod
 *
 * Converts remaining raw JSX <button> usage in application .tsx files
 * to <StandardButton>. This keeps visual styling and old className values
 * while moving controls to the standard component foundation.
 *
 * Exclusions:
 * - standard component library itself
 * - tests/stories
 * - generated snapshots
 * ============================================================ */

const fs = require("fs");
const path = require("path");

const root = process.cwd();
const srcRoot = path.join(root, "src");

const excludedFragments = [
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

  const entries = fs.readdirSync(dir, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      files.push(...walk(full));
      continue;
    }

    if (entry.isFile() && full.endsWith(".tsx")) {
      files.push(full);
    }
  }

  return files;
}

function shouldSkip(file) {
  return excludedFragments.some((fragment) => file.includes(fragment));
}

function addStandardButtonImport(content) {
  if (content.includes("StandardButton")) return content;

  const importLine = 'import { StandardButton } from "@/components/standard";';
  const lines = content.split(/\r?\n/);
  let insertIndex = -1;

  for (let i = 0; i < lines.length; i += 1) {
    if (/^import\s/.test(lines[i])) insertIndex = i;
  }

  if (insertIndex >= 0) {
    lines.splice(insertIndex + 1, 0, importLine);
    return lines.join("\n");
  }

  return `${importLine}\n${content}`;
}

let changed = 0;

for (const file of walk(srcRoot)) {
  if (shouldSkip(file)) continue;

  const original = fs.readFileSync(file, "utf8");
  if (!/<button\b/.test(original) && !/<\/button>/.test(original)) continue;

  let next = original
    .replace(/<button\b/g, "<StandardButton")
    .replace(/<\/button>/g, "</StandardButton>")
    .replace(/(\s)disabled(?=\s|=|>)/g, "$1isDisabled");

  next = addStandardButtonImport(next);

  if (next !== original) {
    fs.writeFileSync(file, next, "utf8");
    changed += 1;
    console.log(`[phase9-button-standardize] ${path.relative(root, file)}`);
  }
}

console.log(`[phase9-button-standardize] Changed files: ${changed}`);