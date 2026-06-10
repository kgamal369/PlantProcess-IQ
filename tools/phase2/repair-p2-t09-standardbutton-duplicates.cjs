const fs = require("fs");
const path = require("path");

const root = process.cwd();

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function backup(file) {
  if (!fs.existsSync(file)) return;

  const stamp = new Date().toISOString().replace(/[-:.TZ]/g, "").slice(0, 14);
  const rel = path.relative(root, file);
  const target = path.join(root, ".phase2_backups", "P2-T09_StandardButtonDuplicateRepair_" + stamp, rel);

  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.copyFileSync(file, target);
}

function parseNames(namesText) {
  return namesText
    .split(",")
    .map((x) => x.trim())
    .filter(Boolean);
}

function hasStandardButton(names) {
  return names.some((name) => {
    const clean = name.replace(/^type\s+/, "").trim();
    return clean === "StandardButton" || clean.startsWith("StandardButton as ");
  });
}

function removeStandardButtonFromImport(match) {
  const names = parseNames(match.names).filter((name) => {
    const clean = name.replace(/^type\s+/, "").trim();
    return clean !== "StandardButton" && !clean.startsWith("StandardButton as ");
  });

  if (names.length === 0) {
    return "";
  }

  return "import { " + names.join(", ") + " } from \"" + match.source + "\";\n";
}

function normalizeStandardButtonImports(text, rel) {
  const importRegex = /import\s+\{([\s\S]*?)\}\s+from\s+["']([^"']+)["'];?\s*/gm;
  const matches = [];

  let match;
  while ((match = importRegex.exec(text)) !== null) {
    const names = parseNames(match[1]);
    const source = match[2];

    if (hasStandardButton(names)) {
      matches.push({
        start: match.index,
        end: match.index + match[0].length,
        full: match[0],
        namesText: match[1],
        names,
        source,
      });
    }
  }

  const hasLocalStandardButtonDeclaration =
    /\b(export\s+)?(const|function)\s+StandardButton\b/.test(text) ||
    /\bclass\s+StandardButton\b/.test(text);

  if (matches.length === 0) {
    return text;
  }

  let removeSet = new Set();

  if (hasLocalStandardButtonDeclaration) {
    for (const item of matches) {
      removeSet.add(item);
    }
  } else if (matches.length > 1) {
    let keep =
      matches.find((x) => x.source === "@/components/standard") ||
      matches.find((x) => /components\/standard$/.test(x.source)) ||
      matches.find((x) => !/StandardButton$/.test(x.source)) ||
      matches[0];

    for (const item of matches) {
      if (item !== keep) {
        removeSet.add(item);
      }
    }
  }

  if (removeSet.size === 0) {
    return text;
  }

  let output = text;

  for (const item of Array.from(removeSet).sort((a, b) => b.start - a.start)) {
    const replacement = removeStandardButtonFromImport(item);
    output = output.slice(0, item.start) + replacement + output.slice(item.end);
  }

  return output;
}

const targetFiles = [
  "Frontend/PlantProcess.Web/src/pages/Analytics/InspectionJobsPage.tsx",
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.implementation.tsx",
  "Frontend/PlantProcess.Web/src/ui/standard-components.implementation.tsx",
];

let changed = 0;

for (const rel of targetFiles) {
  const file = full(rel);

  if (!fs.existsSync(file)) {
    console.log("[P2-T09] skipped missing " + rel);
    continue;
  }

  const before = fs.readFileSync(file, "utf8");
  const after = normalizeStandardButtonImports(before, rel);

  if (after !== before) {
    backup(file);
    fs.writeFileSync(file, after.replace(/\r?\n/g, "\r\n"), "utf8");
    console.log("[P2-T09] repaired duplicate StandardButton import in " + rel);
    changed += 1;
  }
}

const applyRel = "tools/phase2/apply-p2-t09-action-buttons.cjs";
const applyPath = full(applyRel);

if (fs.existsSync(applyPath)) {
  let applyText = fs.readFileSync(applyPath, "utf8");
  const before = applyText;

  applyText = applyText.replace(
    'function ensureNamedImport(text, source, name) {\n',
    'function ensureNamedImport(text, source, name) {\n' +
      '  if (name === "StandardButton") {\n' +
      '    const anyStandardButtonImport = /import\\s+\\{[\\s\\S]*?\\bStandardButton\\b[\\s\\S]*?\\}\\s+from\\s+["\\\'][^"\\\']*components\\/standard(?:\\/StandardButton|\\/index)?["\\\'];?/m;\n' +
      '    if (anyStandardButtonImport.test(text)) return text;\n' +
      '  }\n'
  );

  applyText = applyText.replace(
    '  if (r.startsWith("Frontend/PlantProcess.Web/src/components/standard/")) return false;\n',
    '  if (r.startsWith("Frontend/PlantProcess.Web/src/components/standard/")) return false;\n' +
      '  if (r.startsWith("Frontend/PlantProcess.Web/src/ui/")) return false;\n'
  );

  if (applyText !== before) {
    backup(applyPath);
    fs.writeFileSync(applyPath, applyText.replace(/\r?\n/g, "\r\n"), "utf8");
    console.log("[P2-T09] hardened apply script against duplicate StandardButton imports.");
    changed += 1;
  }
}

console.log("[GREEN] P2-T09 StandardButton duplicate import repair completed. Changed files: " + changed);