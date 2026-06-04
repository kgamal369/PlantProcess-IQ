const fs = require("fs");
const path = require("path");

const packagePath = path.join(process.cwd(), "package.json");
const pkg = JSON.parse(fs.readFileSync(packagePath, "utf8"));

pkg.scripts ??= {};
pkg.scripts["ui:audit"] = "node tools/ui/audit-ui-instances.mjs";
pkg.scripts["ui:validate"] = "node tools/ui/validate-ui-standards.mjs";
pkg.scripts["storybook"] = "storybook dev -p 6006";
pkg.scripts["build:storybook"] = "storybook build";
pkg.scripts["validate:phase2:ui-standards"] = "npm run ui:audit && npm run ui:validate && npm run build";

fs.writeFileSync(packagePath, JSON.stringify(pkg, null, 2) + "\n");

const tsconfigPath = path.join(process.cwd(), "tsconfig.app.json");

if (fs.existsSync(tsconfigPath)) {
  const tsconfig = JSON.parse(fs.readFileSync(tsconfigPath, "utf8"));

  tsconfig.exclude ??= [];

  const excludes = [
    "src/**/*.stories.ts",
    "src/**/*.stories.tsx",
    ".storybook",
    "storybook-static"
  ];

  for (const item of excludes) {
    if (!tsconfig.exclude.includes(item)) {
      tsconfig.exclude.push(item);
    }
  }

  fs.writeFileSync(tsconfigPath, JSON.stringify(tsconfig, null, 2) + "\n");
}

console.log("PPIQ Phase 2 package.json and tsconfig.app.json patched successfully.");