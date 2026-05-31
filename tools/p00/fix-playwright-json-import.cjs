const fs = require("node:fs");
const path = require("node:path");

const file = path.join(
  process.cwd(),
  "Frontend",
  "PlantProcess.Web",
  "e2e",
  "visual",
  "phase56-analytics-system.visual.spec.ts"
);

if (!fs.existsSync(file)) {
  throw new Error("Missing visual spec: " + file);
}

let text = fs.readFileSync(file, "utf8");

const jsonImportRegex =
  /import\s+([A-Za-z_$][\w$]*)\s+from\s+["']\.\/phase56-baseline-manifest\.json["'];?/;

const match = text.match(jsonImportRegex);

if (!match) {
  console.log("No static phase56-baseline-manifest.json import found. Nothing to patch.");
  process.exit(0);
}

const variableName = match[1];

function ensureImport(source, importLine) {
  if (source.includes(importLine)) {
    return source;
  }

  return importLine + "\n" + source;
}

text = text.replace(jsonImportRegex, "").trimStart();

text = ensureImport(text, 'import fs from "node:fs";');
text = ensureImport(text, 'import path from "node:path";');
text = ensureImport(text, 'import { fileURLToPath } from "node:url";');

const loader = `
const phase56VisualSpecDir = path.dirname(fileURLToPath(import.meta.url));
const ${variableName} = JSON.parse(
  fs.readFileSync(
    path.join(phase56VisualSpecDir, "phase56-baseline-manifest.json"),
    "utf8",
  ),
);
`;

const firstNonImportIndex = (() => {
  const lines = text.split("\n");
  let index = 0;

  while (index < lines.length) {
    const line = lines[index].trim();

    if (line === "" || line.startsWith("import ")) {
      index += 1;
      continue;
    }

    break;
  }

  return lines.slice(0, index).join("\n").length;
})();

const lines = text.split("\n");
let insertLine = 0;
while (insertLine < lines.length) {
  const line = lines[insertLine].trim();
  if (line === "" || line.startsWith("import ")) {
    insertLine += 1;
    continue;
  }
  break;
}

lines.splice(insertLine, 0, loader.trim(), "");

text = lines.join("\n");

fs.writeFileSync(file, text.replace(/\r\n/g, "\n"), "utf8");

console.log("Patched Playwright visual JSON manifest import:");
console.log(file);
