import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const scanRoots = ["src/pages", "src/features"];
const nativeTags = ["button", "input", "select", "textarea", "table"];
const failures = [];

function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  const entries = fs.readdirSync(dir, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) files.push(...walk(full));
    else if (/\.(tsx|ts)$/.test(entry.name)) files.push(full);
  }
  return files;
}

for (const scanRoot of scanRoots) {
  for (const file of walk(path.join(root, scanRoot))) {
    const rel = path.relative(root, file).replaceAll("\\", "/");
    const text = fs.readFileSync(file, "utf8");

    for (const tag of nativeTags) {
      const regex = new RegExp(`<${tag}(?=[\\s>/])`, "i");
      if (regex.test(text)) {
        failures.push(`${rel}: Native <${tag}> element found. Use StandardButton, StandardInput, StandardSelect, StandardTextArea or StandardTable.`);
      }
    }

    const hardeningImport = /from\s+["'][^"']*hardening[^"']*["']|import\s+["'][^"']*hardening[^"']*["']/i;
    if (hardeningImport.test(text)) {
      failures.push(`${rel}: Forbidden import from hardening module found. Move reusable UI/contracts into canonical standard/app modules.`);
    }
  }
}

if (failures.length > 0) {
  console.error("\n❌ PPIQ-T205 standard import/UI gate failed.\n");
  for (const failure of failures) console.error("- " + failure);
  process.exit(1);
}

console.log("✅ PPIQ-T205 standard import/UI gate passed.");
