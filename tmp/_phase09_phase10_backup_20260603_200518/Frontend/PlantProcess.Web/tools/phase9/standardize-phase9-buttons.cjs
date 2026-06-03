/* ============================================================
 * PlantProcess IQ — Phase 9 safe button standardization codemod
 *
 * Converts raw <button> to <StandardButton> without creating duplicate
 * imports and without touching standard component internals.
 * ============================================================ */

const fs = require("fs");
const path = require("path");

const root = process.cwd();
const srcRoot = path.join(root, "src");

const excludedFragments = [
  "/src/components/standard/",
  "/src/ui/standard-components.tsx",
  "/__tests__/",
  ".test.tsx",
  ".spec.tsx",
  ".stories.tsx",
  ".snap",
];

function norm(file) {
  return file.replaceAll("\\", "/");
}

function shouldSkip(file) {
  const n = norm(file);
  return excludedFragments.some((fragment) => n.includes(fragment));
}

function walk(dir) {
  if (!fs.existsSync(dir)) return [];

  const entries = fs.readdirSync(dir, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      files.push(...walk(full));
    } else if (entry.isFile() && full.endsWith(".tsx")) {
      files.push(full);
    }
  }

  return files;
}

function hasAnyStandardButtonImport(content) {
  return /import\s+\{[^}]*\bStandardButton\b[^}]*\}\s+from\s+["'][^"']*components\/standard(?:\/StandardButton)?["'];?/m.test(content);
}

function insertAfterCompleteImportSection(content) {
  if (hasAnyStandardButtonImport(content)) {
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
    patched = patched.replace(/(\s)disabled(\s|>|\/)/g, "$1isDisabled$2");

    return `<StandardButton${patched}${end}`;
  });
}

let changed = 0;

for (const file of walk(srcRoot)) {
  if (shouldSkip(file)) continue;

  const original = fs.readFileSync(file, "utf8");
  let next = original;

  if (/<button\b/.test(next) || /<\/button>/.test(next)) {
    next = next.replace(/<button\b/g, "<StandardButton");
    next = next.replace(/<\/button>/g, "</StandardButton>");
  }

  if (/<StandardButton\b|<\/StandardButton>/.test(next)) {
    next = insertAfterCompleteImportSection(next);
    next = patchStandardButtonProps(next);
  }

  if (next !== original) {
    fs.writeFileSync(file, next, "utf8");
    changed += 1;
    console.log(`[phase9-button-standardize] ${path.relative(root, file)}`);
  }
}

console.log(`[phase9-button-standardize] Changed files: ${changed}`);

// Always run final dedupe if available.
const dedupe = path.join(root, "tools", "phase9", "fix-standardbutton-dedupe-and-props.cjs");
if (fs.existsSync(dedupe)) {
  require(dedupe);
}