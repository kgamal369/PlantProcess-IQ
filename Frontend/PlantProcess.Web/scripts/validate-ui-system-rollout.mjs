import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const srcRoot = path.join(root, "src");

const requiredFiles = [
  "src/components/table/StandardTable.tsx",
  "src/components/table/standard-table.css",
  "src/components/layout/PageGrid.tsx",
  "src/components/layout/page-grid.css",
  "src/components/skeletons/LoadingSkeletonSet.tsx",
  "src/components/skeletons/Skeleton.css",
];

function fail(message) {
  console.error(`❌ ${message}`);
  process.exit(1);
}

function pass(message) {
  console.log(`✓ ${message}`);
}

for (const file of requiredFiles) {
  if (!fs.existsSync(path.join(root, file))) {
    fail(`Missing required UI system file: ${file}`);
  }

  pass(`Exists: ${file}`);
}

const indexCss = fs.readFileSync(path.join(root, "src", "index.css"), "utf8");

for (const requiredImport of [
  "standard-table.css",
  "page-grid.css",
]) {
  if (!indexCss.includes(requiredImport)) {
    fail(`src/index.css must import ${requiredImport}`);
  }

  pass(`CSS imported: ${requiredImport}`);
}

function walk(dir) {
  const files = [];

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "coverage"].includes(entry.name)) continue;
      files.push(...walk(full));
    } else if (/\.(tsx)$/.test(entry.name)) {
      files.push(full);
    }
  }

  return files;
}

const rawTableFiles = [];

for (const file of walk(srcRoot)) {
  const relative = path.relative(root, file).replaceAll("\\", "/");

  if (
    relative.includes("StandardTable.tsx") ||
    relative.includes("__tests__") ||
    relative.includes("Website")
  ) {
    continue;
  }

  const content = fs.readFileSync(file, "utf8");

  if (/<table[\s>]/i.test(content) && !/StandardTable/.test(content)) {
    rawTableFiles.push(relative);
  }
}

if (rawTableFiles.length > 0) {
  console.warn("⚠ Raw <table> usage still exists. Rollout is not fully complete:");
  for (const file of rawTableFiles) {
    console.warn(` - ${file}`);
  }
  console.warn("This is a warning for Phase 2 rollout visibility, not a hard failure yet.");
} else {
  pass("No raw table usage detected outside StandardTable.");
}

pass("UI system rollout foundation validation passed");