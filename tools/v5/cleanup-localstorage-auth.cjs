const fs = require("fs");
const path = require("path");

const root = process.cwd();
const targets = [
  path.join(root, "Frontend", "PlantProcess.Web", "e2e"),
  path.join(root, "Frontend", "PlantProcess.Web", "src"),
];

const exts = new Set([".ts", ".tsx", ".js", ".jsx"]);
const forbiddenSetItem = /window\.localStorage\.setItem\(\s*["']plantprocess\.auth\.accessToken["'][\s\S]*?\);?/g;
const forbiddenSessionSetItem = /window\.sessionStorage\.setItem\(\s*["']plantprocess\.auth\.accessToken["'][\s\S]*?\);?/g;

function walk(dir) {
  if (!fs.existsSync(dir)) return [];
  const out = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) out.push(...walk(full));
    else if (entry.isFile() && exts.has(path.extname(full))) out.push(full);
  }
  return out;
}

let changed = 0;

for (const dir of targets) {
  for (const file of walk(dir)) {
    const original = fs.readFileSync(file, "utf8");
    let next = original;

    next = next.replace(forbiddenSetItem, "/* P01: access token is memory-only; UI bootstraps via HttpOnly refresh cookie. */");
    next = next.replace(forbiddenSessionSetItem, "/* P01: access token is memory-only; UI bootstraps via HttpOnly refresh cookie. */");

    // Keep test helper login API tokens for request-level tests, but do not seed browser storage.
    next = next.replace(
      /await page\.addInitScript\(\(accessToken\) => \{\s*\/\* P01: access token is memory-only; UI bootstraps via HttpOnly refresh cookie\. \*\/\s*\}, token\);/g,
      "/* P01: no browser token seeding; AuthProvider performs cookie refresh/login bootstrap. */"
    );

    if (next !== original) {
      fs.writeFileSync(file, next, "utf8");
      changed += 1;
      console.log(`[p01-cleanup] ${path.relative(root, file)}`);
    }
  }
}

console.log(`[p01-cleanup] Changed files: ${changed}`);