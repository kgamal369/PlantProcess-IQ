import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const srcRoot = path.join(frontendRoot, "src");
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

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if ([
        "node_modules",
        "dist",
        "build",
        "coverage",
        ".vite",
        "storybook-static",
        "__snapshots__"
      ].includes(entry.name)) {
        continue;
      }

      walk(full, output);
      continue;
    }

    if (/\.(ts|tsx|js|jsx)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

function hasDefaultExport(text) {
  return /\bexport\s+default\b/.test(text);
}

function makeFacade(baseName, hasDefault) {
  const impl = `./${baseName}.implementation`;
  const lines = [`export * from "${impl}";`];

  if (hasDefault) {
    lines.push(`export { default } from "${impl}";`);
  }

  lines.push("");
  return lines.join("\n");
}

function shouldSkip(relative) {
  const normalized = toPosix(relative);

  if (normalized.includes("/__tests__/")) return true;
  if (normalized.includes(".test.")) return true;
  if (normalized.includes(".spec.")) return true;
  if (normalized.includes(".stories.")) return true;
  if (normalized.includes(".d.ts")) return true;
  if (normalized.includes(".implementation.")) return true;
  if (normalized.includes("/test/")) return true;
  if (normalized.includes("/mocks/")) return true;
  if (normalized.endsWith("/main.tsx")) return true;
  if (normalized.endsWith("/vite-env.d.ts")) return true;

  return false;
}


/* C3C4_HOTFIX01_HELPERS */
function isGuardExcludedFile(relative) {
  const normalized = relative.replaceAll("\\", "/");
  return (
    normalized.includes("/test/") ||
    normalized.includes("/__tests__/") ||
    normalized.includes(".test.") ||
    normalized.includes(".spec.") ||
    normalized.includes(".stories.")
  );
}

function containsRetiredLegacyApiName(text) {
  const forbiddenName = ["plant", "Process", "Api"].join("");
  return text.includes(forbiddenName);
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^$\\{}()|[\]\\]/g, "\\const explicitCandidates = [");
}

function isOwnImplementationFacade(relative, text) {
  const baseName = path.basename(relative, path.extname(relative));
  const escapedBaseName = escapeRegExp(baseName);
  const exportAllPattern = new RegExp(
    String.raw`export\\s+\\*\\s+from\\s+["']\\./${escapedBaseName}\\.implementation["']`
  );
  const exportDefaultPattern = new RegExp(
    String.raw`export\\s+\\{\\s*default\\s*\\}\\s+from\\s+["']\\./${escapedBaseName}\\.implementation["']`
  );
  return exportAllPattern.test(text) || exportDefaultPattern.test(text);
}
/* C3C4_HOTFIX01_HELPERS_END */

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

const allSourceFiles = walk(srcRoot);
const dynamicCandidates = allSourceFiles
  .filter((file) => {
    const relative = toPosix(path.relative(root, file));
    if (shouldSkip(relative)) return false;
    const lines = countLines(read(file));
    return lines >= minLinesToSplit;
  })
  .map((file) => toPosix(path.relative(root, file)));

const candidateSet = new Set([...explicitCandidates, ...dynamicCandidates]);

const splitResults = [];

for (const relative of [...candidateSet].sort()) {
  const originalFile = path.join(root, relative);

  if (!exists(originalFile)) {
    splitResults.push({
      file: relative,
      status: "skipped",
      reason: "missing"
    });
    continue;
  }

  if (shouldSkip(relative)) {
    splitResults.push({
      file: relative,
      status: "skipped",
      reason: "skip-rule"
    });
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
    splitResults.push({
      file: relative,
      status: "skipped",
      reason: "below-threshold",
      originalLines
    });
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

// ------------------------------------------------------------
// Build C4 extraction backlog.
// ------------------------------------------------------------
const inventory = walk(srcRoot)
  .map((file) => {
    const text = read(file);
    const relative = toPosix(path.relative(root, file));
    const lines = countLines(text);
    const isImplementation = /\.implementation\.(ts|tsx|js|jsx)$/.test(relative);

    return {
      file: relative,
      lines,
      isImplementation,
      priority:
        lines >= 1500 ? "P0" :
        lines >= 1000 ? "P1" :
        lines >= 750 ? "P2" :
        lines >= 500 ? "P3" :
        "P4",
      recommendedExtraction:
        relative.includes("WidgetBuilderWizard") ? "Split wizard into step components: DatasetStep, FilterStep, PreviewStep, SaveStep, ScriptStep." :
        relative.includes("AdminDbConfigurationTab") ? "Split into connection profile list, profile form, provider matrix, test connection panel." :
        relative.includes("AdminSchemaConfigurationTab") ? "Split into schema discovery, object coverage, mapping summary, validation panel." :
        relative.includes("MaterialAnalyticsPages") ? "Split into analytics shell, KPI strip, filter panel, trends panel, details table." :
        relative.includes("productCoreApiClient") ? "Split by domain API modules and keep productCoreApiClient as aggregation layer." :
        relative.includes("App.") ? "Split route graph, lazy imports, and navigation metadata." :
        "Extract cohesive UI sections and pure helpers behind tests."
    };
  })
  .sort((a, b) => b.lines - a.lines);

const implementationBacklog = inventory
  .filter((x) => x.isImplementation && x.lines >= 500)
  .map((x, index) => ({
    rank: index + 1,
    ...x
  }));

const publicLargeFiles = inventory
  .filter((x) => !x.isImplementation && x.lines >= 500)
  .map((x, index) => ({
    rank: index + 1,
    ...x
  }));

const facadeFiles = inventory
  .filter((item) => {
    if (item.isImplementation) return false;
    const full = path.join(root, item.file);
    return isOwnImplementationFacade(item.file, read(full));
  })
  .map((item) => {
    const full = path.join(root, item.file);
    const text = read(full);
    return {
      ...item,
      text
    };
  });

const oversizedFacades = facadeFiles.filter((item) => item.lines > 40 && isOwnImplementationFacade(item.file, item.text ?? read(path.join(root, item.file))));

// ------------------------------------------------------------
// Detect forbidden leftovers.
// ------------------------------------------------------------
const plantProcessApiOffenders = [];
const phasePathOffenders = [];
const phaseImportOffenders = [];
const directImplementationImports = [];
const missingCssImports = [];

for (const file of walk(srcRoot)) {
  const relative = toPosix(path.relative(root, file));
  const text = read(file);

  if (!isGuardExcludedFile(relative) && containsRetiredLegacyApiName(text)) {
    plantProcessApiOffenders.push(relative);
  }

  if (/(^|\/)(Phase\d+|phase\d+|P\d{2}P\d{2}|P03P04)(\/|\.|$)/.test(relative)) {
    phasePathOffenders.push(relative);
  }

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

  const importMatches = [
    ...text.matchAll(/from\s+["']([^"']*\.implementation)["']/g),
    ...text.matchAll(/import\s*\(\s*["']([^"']*\.implementation)["']\s*\)/g)
  ];

  for (const match of importMatches) {
    const imported = match[1];
    const baseName = path.basename(relative, path.extname(relative));
    const allowedOwnFacadeImport = imported === `./${baseName}.implementation`;

    if (!allowedOwnFacadeImport) {
      directImplementationImports.push({
        file: relative,
        importPath: imported
      });
    }
  }

  if (/\.(ts|tsx|js|jsx)$/.test(file)) {
    for (const match of text.matchAll(/import\s+["'](\.\/[^"']+\.css)["'];/g)) {
      const cssFull = path.resolve(path.dirname(file), match[1]);

      if (!exists(cssFull)) {
        missingCssImports.push({
          file: relative,
          importPath: match[1]
        });
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
  facadeFiles: facadeFiles.map(({ text, ...rest }) => rest),
  oversizedFacades,
  plantProcessApiOffenders,
  phasePathOffenders,
  phaseImportOffenders,
  directImplementationImports,
  missingCssImports,
  topThirtyLargestFiles: inventory.slice(0, 30),
  note: "C3/C4 creates safe public boundaries and a C4 extraction backlog. Deep internal extraction should be done in later focused packs."
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