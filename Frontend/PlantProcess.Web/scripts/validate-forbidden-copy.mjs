import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const root = process.cwd();
const srcRoot = join(root, "src");

const forbidden = [
  /could\s+not\s+be\s+loaded/i,
  /could\s+not\s+load/i,
];

const allowedExtensions = new Set([".ts", ".tsx", ".js", ".jsx"]);
const ignoredDirectories = new Set([
  "node_modules",
  "dist",
  "build",
  "coverage",
  "playwright-report",
  "test-results",
]);

function extensionOf(filePath) {
  const idx = filePath.lastIndexOf(".");
  return idx >= 0 ? filePath.slice(idx) : "";
}

function walk(dir, results = []) {
  for (const entry of readdirSync(dir)) {
    if (ignoredDirectories.has(entry)) continue;

    const full = join(dir, entry);
    const stat = statSync(full);

    if (stat.isDirectory()) {
      walk(full, results);
      continue;
    }

    if (allowedExtensions.has(extensionOf(full))) {
      results.push(full);
    }
  }

  return results;
}

const failures = [];

for (const file of walk(srcRoot)) {
  const text = readFileSync(file, "utf8");

  for (const pattern of forbidden) {
    if (pattern.test(text)) {
      failures.push(relative(root, file));
      break;
    }
  }
}

if (failures.length > 0) {
  console.error("");
  console.error("PPIQ-T001 failed: forbidden customer-visible failure copy is still present.");
  console.error("");

  for (const file of failures) {
    console.error(` - ${file}`);
  }

  console.error("");
  console.error("Use the Refreshing pattern instead.");
  process.exit(1);
}

console.log("PPIQ-T001 passed: forbidden frontend copy is absent.");
