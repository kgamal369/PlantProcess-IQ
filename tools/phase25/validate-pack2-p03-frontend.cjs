const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const failures = [];

function read(relativePath) {
  const file = path.join(root, relativePath);
  if (!fs.existsSync(file)) {
    failures.push("Missing file: " + relativePath);
    return "";
  }

  return fs.readFileSync(file, "utf8");
}

function mustContain(relativePath, regex, message) {
  const text = read(relativePath);
  if (text && !regex.test(text)) {
    failures.push(message + " in " + relativePath);
  }
}

const pageText = read("Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx");

if (/\\nimport\s/.test(pageText)) {
  failures.push("PageBuilderPage contains escaped newline import corruption");
}

mustContain(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  /pageBuilderApi\.create/,
  "PageBuilderPage must save through backend pageBuilderApi"
);

mustContain(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  /pageBuilderApi\.getBySlug/,
  "PageBuilderPage must load through backend pageBuilderApi"
);

mustContain(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  /pageBuilderApi\.delete/,
  "PageBuilderPage must delete through backend pageBuilderApi"
);

mustContain(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  /type:\s*"moveWidget"/,
  "PageBuilderPage must expose move widget action"
);

mustContain(
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  /type:\s*"resizeWidget"/,
  "PageBuilderPage must expose resize widget action"
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase23-pagebuilder-persistence.spec.ts",
  /creates, reloads, updates, lists, validates and deletes/,
  "P03 API persistence E2E must exist"
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/phase23-pagebuilder-persistence.spec.ts",
  /UI saves and reloads/,
  "P03 UI save/load E2E must exist"
);

mustContain(
  "Frontend/PlantProcess.Web/e2e/page-builder-v7.spec.ts",
  /persisted metadata actions/,
  "Page builder smoke must cover persistence actions"
);

if (failures.length > 0) {
  console.error("Pack 2 validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("Pack 2 P03 frontend structural validation passed.");
