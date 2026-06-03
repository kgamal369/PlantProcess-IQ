/* ============================================================
 * PlantProcess IQ — Phase 9 StandardButton Import Placement Fix
 *
 * Problem:
 * Previous repair inserted:
 *   import { StandardButton } from "@/components/standard";
 * inside multi-line imports like:
 *   import {
 *      ...
 *      import { StandardButton } ...
 *   } from "@/api/plantProcessApi";
 *
 * This script:
 * 1) Removes all standalone StandardButton import lines.
 * 2) Removes accidental self-imports from standard component internals.
 * 3) Re-injects StandardButton after the COMPLETE top import section.
 * 4) Converts StandardButton-only props:
 *      aria-label -> ariaLabel
 *      disabled   -> isDisabled
 * ============================================================ */

const fs = require("fs");
const path = require("path");

const root = process.cwd();
const srcRoot = path.join(root, "src");
const backupRoot = process.env.PPIQ_REPAIR_BACKUP_ROOT || path.join(root, "_phase9_import_hotfix_backup");

const standaloneStandardButtonImport =
  /^\s*import\s+\{\s*StandardButton\s*\}\s+from\s+["']@\/components\/standard["'];?\s*$/gm;

const skipReinjectFragments = [
  `${path.sep}components${path.sep}standard${path.sep}`,
  `${path.sep}ui${path.sep}standard-components.tsx`,
  `${path.sep}__tests__${path.sep}`,
  ".test.tsx",
  ".spec.tsx",
  ".stories.tsx",
  ".snap",
];

function walk(dir) {
  if (!fs.existsSync(dir)) return [];

  const entries = fs.readdirSync(dir, { withFileTypes: true });
  const result = [];

  for (const entry of entries) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      result.push(...walk(full));
    } else if (entry.isFile() && full.endsWith(".tsx")) {
      result.push(full);
    }
  }

  return result;
}

function rel(file) {
  return path.relative(root, file);
}

function backup(file) {
  const target = path.join(backupRoot, rel(file));
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(file, target);
}

function shouldSkipReinject(file) {
  return skipReinjectFragments.some((fragment) => file.includes(fragment));
}

function hasBarrelStandardButtonImport(content) {
  return /import\s+\{[\s\S]*?\bStandardButton\b[\s\S]*?\}\s+from\s+["']@\/components\/standard["'];?/m.test(content);
}

function insertAfterCompleteImportSection(content) {
  if (hasBarrelStandardButtonImport(content)) {
    return content;
  }

  const lines = content.split(/\r?\n/);

  let inImport = false;
  let sawImport = false;
  let lastImportEndIndex = -1;

  for (let i = 0; i < lines.length; i += 1) {
    const trimmed = lines[i].trim();

    if (inImport) {
      if (trimmed.endsWith(";") || trimmed.includes(";")) {
        inImport = false;
        lastImportEndIndex = i;
      }
      continue;
    }

    if (
      trimmed === "" ||
      trimmed.startsWith("//") ||
      trimmed.startsWith("/*") ||
      trimmed.startsWith("*") ||
      trimmed.startsWith("*/")
    ) {
      continue;
    }

    if (trimmed.startsWith("import ")) {
      sawImport = true;

      if (trimmed.endsWith(";") || trimmed.includes(";")) {
        lastImportEndIndex = i;
      } else {
        inImport = true;
      }

      continue;
    }

    if (sawImport) {
      break;
    }
  }

  const importLine = 'import { StandardButton } from "@/components/standard";';

  if (lastImportEndIndex >= 0) {
    lines.splice(lastImportEndIndex + 1, 0, importLine);
    return lines.join("\n");
  }

  return `${importLine}\n${content}`;
}

function patchStandardButtonProps(content) {
  return content.replace(/<StandardButton\b([\s\S]*?)(\/?>)/g, (_full, attrs, end) => {
    let patched = attrs;

    patched = patched.replace(/\baria-label=/g, "ariaLabel=");
    patched = patched.replace(/\bdisabled=/g, "isDisabled=");

    // Handles boolean disabled without value:
    // <StandardButton disabled ...> -> <StandardButton isDisabled ...>
    patched = patched.replace(/(\s)disabled(\s|>|\/)/g, "$1isDisabled$2");

    return `<StandardButton${patched}${end}`;
  });
}

let changed = 0;

for (const file of walk(srcRoot)) {
  const original = fs.readFileSync(file, "utf8");

  let next = original;

  // Always remove bad standalone StandardButton imports first.
  next = next.replace(standaloneStandardButtonImport, "");

  // Normalize too many blank lines after removing imports.
  next = next.replace(/\n{3,}/g, "\n\n");

  const usesStandardButton = /<StandardButton\b|<\/StandardButton>/.test(next);

  if (usesStandardButton && !shouldSkipReinject(file)) {
    next = insertAfterCompleteImportSection(next);
    next = patchStandardButtonProps(next);
  }

  // For standard internals, never self-import StandardButton.
  if (shouldSkipReinject(file)) {
    next = next.replace(standaloneStandardButtonImport, "");
  }

  if (next !== original) {
    backup(file);
    fs.writeFileSync(file, next, "utf8");
    changed += 1;
    console.log(`[phase9-import-hotfix] ${rel(file)}`);
  }
}

console.log(`[phase9-import-hotfix] Changed files: ${changed}`);