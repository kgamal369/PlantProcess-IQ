import fs from "node:fs";
import path from "node:path";
import process from "node:process";

const root = process.cwd();
const assetsDir = path.join(root, "dist", "assets");

const KB = 1024;

const budgets = [
  { name: "entry", pattern: /^index-.*\.js$/, maxKb: 250 },
  { name: "dashboard", pattern: /Dashboard/i, maxKb: 180 },
  { name: "admin", pattern: /Admin/i, maxKb: 250 },
  { name: "correlation", pattern: /Correlation/i, maxKb: 150 },
  { name: "vendor/react/recharts", pattern: /(vendor|react|recharts)/i, maxKb: 800 },
];

const absoluteMaxKb = 900;

function fail(message) {
  console.error(`\n❌ Bundle size check failed:\n${message}\n`);
  process.exit(1);
}

function walk(dir) {
  if (!fs.existsSync(dir)) return [];

  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const fullPath = path.join(dir, entry.name);
    return entry.isDirectory() ? walk(fullPath) : [fullPath];
  });
}

if (!fs.existsSync(assetsDir)) {
  fail(`dist/assets was not found. Run "npm run build" before "npm run size-check".`);
}

const jsFiles = walk(assetsDir).filter((file) => file.endsWith(".js"));

if (jsFiles.length === 0) {
  fail("No JavaScript chunks were found in dist/assets.");
}

const violations = [];

for (const file of jsFiles) {
  const fileName = path.basename(file);
  const sizeKb = fs.statSync(file).size / KB;

  if (sizeKb > absoluteMaxKb) {
    violations.push(`${fileName}: ${sizeKb.toFixed(1)}KB > absolute max ${absoluteMaxKb}KB`);
  }

  for (const budget of budgets) {
    if (budget.pattern.test(fileName) && sizeKb > budget.maxKb) {
      violations.push(`${fileName}: ${sizeKb.toFixed(1)}KB > ${budget.name} budget ${budget.maxKb}KB`);
    }
  }
}

console.log("\nPlantProcess IQ bundle size report");
console.log("──────────────────────────────────");

for (const file of jsFiles.sort()) {
  const fileName = path.basename(file);
  const sizeKb = fs.statSync(file).size / KB;
  console.log(`${fileName.padEnd(55)} ${sizeKb.toFixed(1).padStart(8)} KB`);
}

if (violations.length > 0) {
  fail(violations.map((item) => `- ${item}`).join("\n"));
}

console.log("\n✅ Bundle size check passed.\n");