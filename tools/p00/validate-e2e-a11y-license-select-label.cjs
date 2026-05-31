const fs = require("node:fs");
const path = require("node:path");

const srcRoot = path.join(process.cwd(), "Frontend", "PlantProcess.Web", "src");

function walk(dir) {
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) return walk(full);
    if (entry.isFile() && /\.(tsx|jsx)$/.test(entry.name)) return [full];

    return [];
  });
}

const failures = [];

for (const file of walk(srcRoot)) {
  const text = fs.readFileSync(file, "utf8");

  for (const match of text.matchAll(/<select\b([^>]*)>([\s\S]*?)<\/select>/g)) {
    const attrs = match[1];
    const body = match[2];

    const isLicenseSelect =
      body.includes('value="Light"') &&
      body.includes('value="Pro"') &&
      body.includes('value="ProPlus"') &&
      body.includes('value="Enterprise"');

    const hasName =
      /\saria-label\s*=/.test(attrs) ||
      /\saria-labelledby\s*=/.test(attrs) ||
      /\sid\s*=/.test(attrs);

    if (isLicenseSelect && !hasName) {
      failures.push(path.relative(process.cwd(), file));
    }
  }
}

if (failures.length > 0) {
  console.error("Unlabeled license select remains:");
  for (const failure of failures) console.error(" - " + failure);
  process.exit(1);
}

console.log("A11y validation passed: license select has an accessible name.");
