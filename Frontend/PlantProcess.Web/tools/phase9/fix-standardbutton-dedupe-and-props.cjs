/* ============================================================
 * PlantProcess IQ — Phase 9 StandardButton Dedupe + Prop Fix
 *
 * Fixes:
 * - Duplicate StandardButton imports from barrel/direct/relative imports
 * - Bad self-imports inside standard component internals
 * - ariaLabel inside src/ui/standard-components.tsx, which expects aria-label
 * ============================================================ */

const fs = require("fs");
const path = require("path");

const root = process.cwd();
const srcRoot = path.join(root, "src");
const backupRoot = process.env.PPIQ_REPAIR_BACKUP_ROOT || path.join(root, "_phase9_standardbutton_dedupe_backup");

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

function rel(file) {
  return path.relative(root, file);
}

function norm(file) {
  return file.replaceAll("\\", "/");
}

function backup(file) {
  const target = path.join(backupRoot, rel(file));
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(file, target);
}

function isStandardInternal(file) {
  const n = norm(file);
  return (
    n.endsWith("/src/ui/standard-components.tsx") ||
    n.includes("/src/components/standard/")
  );
}

function parseStandardButtonImportLine(line) {
  const namedImport = line.match(
    /^\s*import\s+\{([^}]*)\}\s+from\s+["']([^"']*components\/standard(?:\/StandardButton)?)["'];?\s*$/
  );

  if (!namedImport) return null;

  const names = namedImport[1]
    .split(",")
    .map((x) => x.trim())
    .filter(Boolean);

  if (!names.includes("StandardButton")) return null;

  return {
    names,
    source: namedImport[2],
    onlyStandardButton: names.length === 1 && names[0] === "StandardButton",
    directButtonFile: namedImport[2].endsWith("/StandardButton"),
  };
}

function cleanupDuplicateStandardButtonImports(content, file) {
  const lines = content.split(/\r?\n/);
  const entries = [];

  for (let i = 0; i < lines.length; i += 1) {
    const parsed = parseStandardButtonImportLine(lines[i]);
    if (parsed) {
      entries.push({ index: i, ...parsed });
    }
  }

  if (entries.length === 0) {
    return content;
  }

  if (isStandardInternal(file)) {
    for (const entry of entries) {
      lines[entry.index] = "";
    }

    return lines.join("\n").replace(/\n{3,}/g, "\n\n");
  }

  if (entries.length === 1) {
    return content;
  }

  // Prefer an existing grouped import that already imports other standard components.
  let keep =
    entries.find((entry) => !entry.onlyStandardButton) ||
    entries.find((entry) => entry.directButtonFile) ||
    entries[0];

  for (const entry of entries) {
    if (entry.index === keep.index) continue;

    if (entry.onlyStandardButton) {
      lines[entry.index] = "";
    }
  }

  return lines.join("\n").replace(/\n{3,}/g, "\n\n");
}

function hasAnyStandardButtonImport(content) {
  return /import\s+\{[^}]*\bStandardButton\b[^}]*\}\s+from\s+["'][^"']*components\/standard(?:\/StandardButton)?["'];?/m.test(content);
}

function insertStandardButtonImportSafely(content) {
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

function patchStandardButtonOpeningTags(content, file) {
  if (isStandardInternal(file)) {
    return content.replace(/<StandardButton\b([\s\S]*?)(\/?>)/g, (_full, attrs, end) => {
      const patched = attrs.replace(/\bariaLabel=/g, "aria-label=");
      return `<StandardButton${patched}${end}`;
    });
  }

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
  const original = fs.readFileSync(file, "utf8");
  let next = original;

  next = cleanupDuplicateStandardButtonImports(next, file);

  const usesStandardButton = /<StandardButton\b|<\/StandardButton>/.test(next);

  if (usesStandardButton && !isStandardInternal(file)) {
    next = insertStandardButtonImportSafely(next);
  }

  next = cleanupDuplicateStandardButtonImports(next, file);
  next = patchStandardButtonOpeningTags(next, file);
  next = next.replace(/\n{3,}/g, "\n\n");

  if (next !== original) {
    backup(file);
    fs.writeFileSync(file, next, "utf8");
    changed += 1;
    console.log(`[phase9-dedupe-fix] ${rel(file)}`);
  }
}

console.log(`[phase9-dedupe-fix] Changed files: ${changed}`);