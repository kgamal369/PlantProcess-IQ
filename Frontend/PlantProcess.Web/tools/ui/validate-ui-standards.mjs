import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

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
  "docs/ui-standards/component-specification.md",
  "docs/ui-standards/button-inventory.csv",
  "docs/ui-standards/input-inventory.csv",
  "docs/ui-standards/table-inventory.csv",
  "docs/ui-standards/tabs-inventory.csv",
  "docs/ui-standards/inventory-summary.md"
];

const missing = requiredFiles.filter((file) => !fs.existsSync(path.join(root, file)));

if (missing.length > 0) {
  console.error("Missing current UI standards files:");
  for (const file of missing) console.error(`- ${file}`);
  process.exit(1);
}

console.log("✅ Current UI standards structural validation passed.");
