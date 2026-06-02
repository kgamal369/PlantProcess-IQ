// P1-05: fails (exit 1) if two page components share a basename across paths.
// Usage: node tools/check-duplicate-pages.mjs
import { readdirSync, statSync } from "node:fs";
import { join } from "node:path";

const ROOT = "src/pages";
const map = new Map();

function walk(dir) {
  for (const entry of readdirSync(dir)) {
    const p = join(dir, entry);
    if (statSync(p).isDirectory()) walk(p);
    else if (/Page\.tsx$/.test(entry)) {
      const arr = map.get(entry) ?? [];
      arr.push(p.replace(/\\/g, "/"));
      map.set(entry, arr);
    }
  }
}

walk(ROOT);
const dups = [...map.entries()].filter(([, paths]) => paths.length > 1);
if (dups.length) {
  console.error("Duplicate page components found (one canonical path required):");
  for (const [name, paths] of dups) console.error(`  ${name}\n    - ${paths.join("\n    - ")}`);
  process.exit(1);
}
console.log("OK: every page component resolves to exactly one file.");
