
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
  "src/components/standard/index.ts",
  "src/components/standard/__tests__/StandardButton.test.tsx",
  "src/components/standard/__tests__/StandardTabs.test.tsx",
  "src/components/standard/__tests__/StandardTable.test.tsx",
  "src/components/standard/StandardButton.stories.tsx",
  "src/components/standard/StandardTable.stories.tsx",
  "src/components/standard/StandardTabs.stories.tsx",
  "src/components/standard/StandardFields.stories.tsx",
  "src/components/standard/StandardSurface.stories.tsx",
  "src/components/standard/DesignTokens.stories.tsx",
  "src/components/standard/DoDont.stories.tsx",
  "src/components/standard/Onboarding.stories.tsx",
  "docs/ui-standards/button-inventory.csv",
  "docs/ui-standards/table-inventory.csv",
  "docs/ui-standards/tabs-inventory.csv",
  "docs/ui-standards/input-inventory.csv",
  "docs/ui-standards/inventory-summary.md",
];

const requiredScripts = [
  "ui:inventory",
  "ui:validate:phase2-full",
  "test:ui-standards",
  "validate:phase2:ui-standards-full",
];

const missingFiles = requiredFiles.filter((file) => !fs.existsSync(path.join(root, file)));
const pkg = JSON.parse(fs.readFileSync(path.join(root, "package.json"), "utf8"));
const missingScripts = requiredScripts.filter((script) => !pkg.scripts?.[script]);

function csvRows(file) {
  const full = path.join(root, file);
  if (!fs.existsSync(full)) return 0;
  return fs.readFileSync(full, "utf8").trim().split(/\r?\n/).length - 1;
}

const buttonRows = csvRows("docs/ui-standards/button-inventory.csv");
const tableRows = csvRows("docs/ui-standards/table-inventory.csv");
const tabsRows = csvRows("docs/ui-standards/tabs-inventory.csv");
const inputRows = csvRows("docs/ui-standards/input-inventory.csv");

const countFailures = [];
if (buttonRows < 1) countFailures.push("button-inventory.csv has no rows");
if (tableRows < 1) countFailures.push("table-inventory.csv has no rows");
if (tabsRows < 1) countFailures.push("tabs-inventory.csv has no rows");
if (inputRows < 1) countFailures.push("input-inventory.csv has no rows");

if (missingFiles.length || missingScripts.length || countFailures.length) {
  if (missingFiles.length) {
    console.error("Missing required files:");
    for (const file of missingFiles) console.error("- " + file);
  }

  if (missingScripts.length) {
    console.error("Missing package scripts:");
    for (const script of missingScripts) console.error("- " + script);
  }

  if (countFailures.length) {
    console.error("CSV count failures:");
    for (const failure of countFailures) console.error("- " + failure);
  }

  process.exit(1);
}

console.log("PPIQ Phase 2 full UI standards validation passed.");
console.log("Buttons: " + buttonRows);
console.log("Tables: " + tableRows);
console.log("Tabs/navigation: " + tabsRows);
console.log("Inputs/forms: " + inputRows);
