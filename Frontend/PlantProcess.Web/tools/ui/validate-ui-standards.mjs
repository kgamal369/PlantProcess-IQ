import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

const required = [
  "tools/ui/audit-ui-instances.mjs",
  "src/ui/design-tokens.css",
  "src/ui/standard-components.css",
  "src/ui/standard-components.tsx",
  "src/ui/index.ts",
  "src/ui/standards/ui-component-specs.md",
  "src/ui/standard-components.stories.tsx",
  ".storybook/main.ts",
  ".storybook/preview.ts",
  ".storybook/preview.css",
];

const missing = required.filter((file) => !fs.existsSync(path.join(root, file)));

if (missing.length) {
  console.error("Missing Phase 2 UI standards files:");
  for (const item of missing) console.error(`- ${item}`);
  process.exit(1);
}

const pkg = JSON.parse(fs.readFileSync(path.join(root, "package.json"), "utf8"));
const scripts = ["ui:audit", "ui:validate", "storybook", "build:storybook", "validate:phase2:ui-standards"];
const missingScripts = scripts.filter((name) => !pkg.scripts?.[name]);

if (missingScripts.length) {
  console.error("Missing package.json scripts:");
  for (const item of missingScripts) console.error(`- ${item}`);
  process.exit(1);
}

console.log("PPIQ Phase 2 UI standards validation passed.");