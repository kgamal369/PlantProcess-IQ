import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function fail(message) {
  console.error(`❌ ${message}`);
  process.exit(1);
}

function pass(message) {
  console.log(`✓ ${message}`);
}

function requireFile(relativePath) {
  if (!fs.existsSync(path.join(root, relativePath))) {
    fail(`Missing required UI system file: ${relativePath}`);
  }
  pass(`Exists: ${relativePath}`);
}

const requiredFiles = [
  "src/components/standard/tokens.ts",
  "src/components/standard/standard-components.css",
  "src/components/standard/StandardButton.tsx",
  "src/components/standard/StandardFields.tsx",
  "src/components/standard/StandardTabs.tsx",
  "src/components/standard/StandardTable.tsx",
  "src/components/standard/StandardSurface.tsx",
  "src/components/standard/DataFetchBoundary.tsx",
  "src/components/standard/index.ts",
  "src/components/layout/PageGrid.tsx",
  "src/components/layout/page-grid.css",
  "src/components/skeletons/LoadingSkeletonSet.tsx",
  "src/components/skeletons/Skeleton.css"
];

for (const file of requiredFiles) {
  requireFile(file);
}

console.log("✅ Current UI system rollout validation passed.");
