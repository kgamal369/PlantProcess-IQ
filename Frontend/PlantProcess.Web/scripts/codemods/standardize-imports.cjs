const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

const rewrites = new Map([
  ["@/components/hardening/StandardButton", "@/components/standard/StandardButton"],
  ["@/hardening/StandardButton", "@/components/standard/StandardButton"],
  ["@/components/hardening/DataFetchBoundary", "@/components/standard/DataFetchBoundary"],
  ["@/hardening/DataFetchBoundary", "@/components/standard/DataFetchBoundary"],
  ["@/components/hardening/ErrorBoundary", "@/components/standard/ErrorBoundary"],
  ["@/components/hardening/AppErrorBoundary", "@/components/standard/ErrorBoundary"],
  ["@/hardening/RouteErrorBoundary", "@/components/standard/ErrorBoundary"],
  ["@/components/ErrorBoundary", "@/components/standard/ErrorBoundary"],
  ["@/components/table/StandardTable", "@/components/standard/StandardTable"],
]);

function walk(dir, files = []) {
  if (!fs.existsSync(dir)) return files;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (["node_modules", "dist", "build", "coverage"].includes(entry.name)) continue;

    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, files);
    else if (/\.(ts|tsx|js|jsx)$/.test(entry.name)) files.push(full);
  }

  return files;
}

let changed = 0;

for (const file of walk(path.join(root, "src"))) {
  const before = fs.readFileSync(file, "utf8");
  let after = before;

  for (const [from, to] of rewrites) {
    after = after.split(from).join(to);
  }

  if (after !== before) {
    fs.writeFileSync(file, after, "utf8");
    changed++;
  }
}

console.log("PPIQ standardize-imports codemod completed. Files changed: " + changed);
