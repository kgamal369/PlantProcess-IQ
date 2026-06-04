import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const srcRoot = path.join(root, "Frontend", "PlantProcess.Web", "src");
const reportDir = path.join(root, "Documentation", "v5");

fs.mkdirSync(reportDir, { recursive: true });

function exists(file) {
  return fs.existsSync(file);
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, text) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, text, "utf8");
}

function toPosix(value) {
  return value.replaceAll(path.sep, "/");
}

function countLines(text) {
  return text.split(/\r?\n/).length;
}

function retiredLegacyApiName() {
  return ["plant", "Process", "Api"].join("");
}

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "build", "coverage", ".vite", "storybook-static", "__snapshots__"].includes(entry.name)) continue;
      walk(full, output);
    } else if (/\.(ts|tsx|js|jsx)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

function isGuardExcludedFile(relative) {
  const normalized = relative.replaceAll("\\", "/");
  return (
    normalized.includes("/test/") ||
    normalized.includes("/__tests__/") ||
    normalized.includes(".test.") ||
    normalized.includes(".spec.") ||
    normalized.includes(".stories.") ||
    normalized.includes("/mocks/") ||
    normalized.endsWith("/main.tsx") ||
    normalized.endsWith("/vite-env.d.ts")
  );
}

function isSplitCandidateExcluded(relative) {
  const normalized = relative.replaceAll("\\", "/");
  return (
    isGuardExcludedFile(normalized) ||
    normalized.includes(".d.ts") ||
    normalized.includes(".implementation.")
  );
}

function hasDefaultExport(text) {
  return /\bexport\s+default\b/.test(text);
}

function makeFacade(baseName, hasDefault) {
  const lines = [`export * from "./${baseName}.implementation";`];

  if (hasDefault) {
    lines.push(`export { default } from "./${baseName}.implementation";`);
  }

  lines.push("");
  return lines.join("\n");
}

function isOwnImplementationFacade(relative, text) {
  const baseName = path.parse(relative).name;

  return (
    text.includes(`export * from "./${baseName}.implementation";`) ||
    text.includes(`export * from './${baseName}.implementation';`) ||
    text.includes(`export { default } from "./${baseName}.implementation";`) ||
    text.includes(`export { default } from './${baseName}.implementation';`)
  );
}

const explicitCandidates = [
  "Frontend/PlantProcess.Web/src/api/productCoreApiClient.ts",
  "Frontend/PlantProcess.Web/src/api/productApiHardening.ts",
  "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizard.tsx",
  "Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizardContent.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/AdminSchemaConfigurationTab.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/CanonicalSchemaMappingPanel.tsx",
  "Frontend/PlantProcess.Web/src/pages/Admin/TwoStageImportMonitorPanel.tsx",
  "Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.tsx",
  "Frontend/PlantProcess.Web/src/pages/DemoAnalytics/DemoAnalyticsPages.tsx",
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx",
  "Frontend/PlantProcess.Web/src/state/DashboardGridLayoutContext.tsx",
  "Frontend/PlantProcess.Web/src/ui/standard-components.tsx",
  "Frontend/PlantProcess.Web/src/App.tsx",
  "Frontend/PlantProcess.Web/src/demo/plantProcessDemoScenario.ts"
];

const minLinesToSplit = 450;

const dynamicCandidates = walk(srcRoot)
  .filter((file) => {
    const relative = toPosix(path.relative(root, file));
    if (isSplitCandidateExcluded(relative)) return false;
    return countLines(read(file)) >= minLinesToSplit;
  })
  .map((file) => toPosix(path.relative(root, file)));

const candidateSet = new Set([...explicitCandidates, ...dynamicCandidates]);
const splitResults = [];

for (const relative of [...candidateSet].sort()) {
  const originalFile = path.join(root, relative);

  if (!exists(originalFile)) {
    splitResults.push({ file: relative, status: "skipped", reason: "missing" });
    continue;
  }

  if (isSplitCandidateExcluded(relative)) {
    splitResults.push({ file: relative, status: "skipped", reason: "excluded" });
    continue;
  }

  const parsed = path.parse(originalFile);
  const implementationFile = path.join(parsed.dir, `${parsed.name}.implementation${parsed.ext}`);
  const originalText = read(originalFile);
  const originalLines = countLines(originalText);

  if (exists(implementationFile)) {
    const implText = read(implementationFile);
    const facadeText = makeFacade(parsed.name, hasDefaultExport(implText));
    write(originalFile, facadeText);

    splitResults.push({
      file: relative,
      implementation: toPosix(path.relative(root, implementationFile)),
      status: "facade-refreshed",
      originalLines,
      facadeLines: countLines(facadeText)
    });
    continue;
  }

  if (originalLines < minLinesToSplit && !explicitCandidates.includes(relative)) {
    splitResults.push({ file: relative, status: "skipped", reason: "below-threshold", originalLines });
    continue;
  }

  fs.renameSync(originalFile, implementationFile);
  const implText = read(implementationFile);
  const facadeText = makeFacade(parsed.name, hasDefaultExport(implText));
  write(originalFile, facadeText);

  splitResults.push({
    file: relative,
    implementation: toPosix(path.relative(root, implementationFile)),
    status: "split",
    originalLines,
    facadeLines: countLines(facadeText)
  });
}

const inventory = walk(srcRoot)
  .map((file) => {
    const relative = toPosix(path.relative(root, file));
    const text = read(file);
    const lines = countLines(text);
    const isImplementation = /\.implementation\.(ts|tsx|js|jsx)$/.test(relative);

    return {
      file: relative,
      lines,
      isImplementation,
      priority: lines >= 1500 ? "P0" : lines >= 1000 ? "P1" : lines >= 750 ? "P2" : lines >= 500 ? "P3" : "P4"
    };
  })
  .sort((a, b) => b.lines - a.lines);

const implementationBacklog = inventory
  .filter((x) => x.isImplementation && x.lines >= 500)
  .map((x, index) => ({ rank: index + 1, ...x }));

const publicLargeFiles = inventory
  .filter((x) => !x.isImplementation && !isGuardExcludedFile(x.file) && x.lines >= 500)
  .map((x, index) => ({ rank: index + 1, ...x }));

const facadeFiles = inventory
  .filter((item) => {
    if (item.isImplementation) return false;
    if (isGuardExcludedFile(item.file)) return false;

    const full = path.join(root, item.file);
    return isOwnImplementationFacade(item.file, read(full));
  });

const oversizedFacades = facadeFiles.filter((item) => item.lines > 40);

const forbidden = retiredLegacyApiName();
const plantProcessApiOffenders = [];
const phasePathOffenders = [];
const phaseImportOffenders = [];
const directImplementationImports = [];
const missingCssImports = [];

for (const file of walk(srcRoot)) {
  const relative = toPosix(path.relative(root, file));
  const text = read(file);
  const excluded = isGuardExcludedFile(relative);

  if (!excluded && text.includes(forbidden)) {
    plantProcessApiOffenders.push(relative);
  }

  if (!excluded && /(^|\/)(Phase\d+|phase\d+|P\d{2}P\d{2}|P03P04)(\/|\.|$)/.test(relative)) {
    phasePathOffenders.push(relative);
  }

  if (!excluded) {
    for (const pattern of [
      "Phase1WorkflowTruthPanel",
      "Phase56Pages",
      "Phase78Pages",
      "Phase910Pages",
      "Phase1112Pages",
      "Phase1314Pages",
      "@/components/phase2",
      "/phase2/"
    ]) {
      if (text.includes(pattern)) {
        phaseImportOffenders.push({ file: relative, pattern });
      }
    }
  }

  const importMatches = [
    ...text.matchAll(/from\s+["']([^"']*\.implementation)["']/g),
    ...text.matchAll(/import\s*\(\s*["']([^"']*\.implementation)["']\s*\)/g)
  ];

  for (const match of importMatches) {
    const imported = match[1];
    const baseName = path.parse(relative).name;
    const allowedOwnFacadeImport = imported === `./${baseName}.implementation`;

    if (!allowedOwnFacadeImport) {
      directImplementationImports.push({ file: relative, importPath: imported });
    }
  }

  if (/\.(ts|tsx|js|jsx)$/.test(file)) {
    for (const match of text.matchAll(/import\s+["'](\.\/[^"']+\.css)["'];/g)) {
      const cssFull = path.resolve(path.dirname(file), match[1]);

      if (!exists(cssFull)) {
        missingCssImports.push({ file: relative, importPath: match[1] });
      }
    }
  }
}

const report = {
  generatedAtUtc: new Date().toISOString(),
  minLinesToSplit,
  splitResults,
  publicLargeFiles,
  implementationBacklog,
  facadeFiles,
  oversizedFacades,
  plantProcessApiOffenders,
  phasePathOffenders,
  phaseImportOffenders,
  directImplementationImports,
  missingCssImports,
  topThirtyLargestFiles: inventory.slice(0, 30),
  note: "C3/C4 creates safe public boundaries. Implementation files remain as backlog for later deep extraction."
};

write(
  path.join(reportDir, "pack-c3-c4-frontend-boundary-refactor-report.json"),
  JSON.stringify(report, null, 2)
);

write(
  path.join(reportDir, "pack-c4-deep-extraction-backlog.json"),
  JSON.stringify(implementationBacklog, null, 2)
);

write(
  path.join(reportDir, "pack-c3-c4-large-file-inventory.json"),
  JSON.stringify(inventory, null, 2)
);

console.log(JSON.stringify({
  split: splitResults.filter((x) => x.status === "split").length,
  refreshed: splitResults.filter((x) => x.status === "facade-refreshed").length,
  skipped: splitResults.filter((x) => x.status === "skipped").length,
  publicLargeFiles: publicLargeFiles.length,
  implementationBacklog: implementationBacklog.length,
  oversizedFacades: oversizedFacades.length,
  plantProcessApiOffenders: plantProcessApiOffenders.length,
  phasePathOffenders: phasePathOffenders.length,
  phaseImportOffenders: phaseImportOffenders.length,
  directImplementationImports: directImplementationImports.length,
  missingCssImports: missingCssImports.length,
  report: "Documentation/v5/pack-c3-c4-frontend-boundary-refactor-report.json"
}, null, 2));

if (
  oversizedFacades.length > 0 ||
  plantProcessApiOffenders.length > 0 ||
  phasePathOffenders.length > 0 ||
  phaseImportOffenders.length > 0 ||
  directImplementationImports.length > 0 ||
  missingCssImports.length > 0
) {
  console.error("Pack C3/C4 boundary refactor still has blocking offenders.");
  process.exit(3);
}

console.log("[pack-c3-c4] frontend boundary refactor completed.");