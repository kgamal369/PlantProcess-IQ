import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

const packageJsonPath = path.join(root, "package.json");
if (!fs.existsSync(packageJsonPath)) {
  console.error("Missing frontend package.json");
  process.exit(1);
}

const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, "utf8"));
const requiredScripts = ["build", "test", "test:run", "e2e", "ui:validate", "ui:validate:phase2-full"];
const missingScripts = requiredScripts.filter((script) => !packageJson.scripts || !packageJson.scripts[script]);

if (missingScripts.length > 0) {
  console.error("Missing frontend validation scripts:");
  for (const script of missingScripts) console.error(`- ${script}`);
  process.exit(1);
}

const requiredFiles = [
  "src/components/standard/StandardButton.tsx",
  "src/components/standard/StandardFields.tsx",
  "src/components/standard/StandardTabs.tsx",
  "src/components/standard/StandardTable.tsx",
  "src/components/standard/StandardSurface.tsx",
  "src/components/standard/DataFetchBoundary.tsx",
  "src/components/standard/__tests__/StandardButton.test.tsx",
  "src/components/standard/__tests__/StandardTable.test.tsx",
  "src/components/standard/__tests__/StandardTabs.test.tsx",
  "docs/ui-standards/inventory-summary.md"
];

const missingFiles = requiredFiles.filter((file) => !fs.existsSync(path.join(root, file)));

if (missingFiles.length > 0) {
  console.error("Missing Phase 2 full UI standards files:");
  for (const file of missingFiles) console.error(`- ${file}`);
  process.exit(1);
}

console.log("✅ Phase 2 full UI standards structural validation passed.");
