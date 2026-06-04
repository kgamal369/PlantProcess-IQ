import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const srcRoot = path.join(root, "Frontend", "PlantProcess.Web", "src");
const reportDir = path.join(root, "Documentation", "v5");

fs.mkdirSync(reportDir, { recursive: true });

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function readFile(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
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

function isGuardExcludedFile(relative) {
  const normalized = relative.replaceAll("\\", "/");
  return (
    normalized.includes("/test/") ||
    normalized.includes("/__tests__/") ||
    normalized.includes(".test.") ||
    normalized.includes(".spec.") ||
    normalized.includes(".stories.") ||
    normalized.includes("/mocks/")
  );
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

const requiredFiles = [
  "Documentation/v5/pack-c3-c4-frontend-boundary-refactor-report.json",
  "Documentation/v5/pack-c4-deep-extraction-backlog.json",
  "Documentation/v5/pack-c3-c4-large-file-inventory.json",
  "Frontend/PlantProcess.Web/src/api/productApiClient.ts",
  "Frontend/PlantProcess.Web/src/api/productCoreApiClient.ts",
  "Frontend/PlantProcess.Web/src/api/productApiHardening.ts"
];

for (const file of requiredFiles) {
  ok(`${file} exists`, exists(file));
}

const forbidden = retiredLegacyApiName();
const files = walk(srcRoot);

const plantProcessApiOffenders = [];
const phasePathOffenders = [];
const phaseImportOffenders = [];
const directImplementationImports = [];
const oversizedFacades = [];
const implementationFiles = [];
const publicLargeFiles = [];
const missingCssImports = [];

for (const file of files) {
  const relative = toPosix(path.relative(root, file));
  const text = fs.readFileSync(file, "utf8");
  const lines = countLines(text);
  const excluded = isGuardExcludedFile(relative);
  const isImplementation = /\.implementation\.(ts|tsx|js|jsx)$/.test(relative);

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

  if (isImplementation) {
    implementationFiles.push({ file: relative, lines });
  } else {
    if (!excluded && lines >= 500) {
      publicLargeFiles.push({ file: relative, lines });
    }

    if (!excluded && isOwnImplementationFacade(relative, text) && lines > 40) {
      oversizedFacades.push({ file: relative, lines });
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

      if (!fs.existsSync(cssFull)) {
        missingCssImports.push({ file: relative, importPath: match[1] });
      }
    }
  }
}

ok("zero plantProcessApi references", plantProcessApiOffenders.length === 0, `${plantProcessApiOffenders.length} offender(s)`);
ok("zero phase-named source paths", phasePathOffenders.length === 0, `${phasePathOffenders.length} offender(s)`);
ok("zero phase-named import identifiers", phaseImportOffenders.length === 0, `${phaseImportOffenders.length} offender(s)`);
ok("zero direct implementation imports except own facade", directImplementationImports.length === 0, `${directImplementationImports.length} offender(s)`);
ok("zero missing local CSS imports", missingCssImports.length === 0, `${missingCssImports.length} offender(s)`);
ok("all implementation facades stay small", oversizedFacades.length === 0, `${oversizedFacades.length} oversized facade(s)`);
ok("implementation boundary files exist", implementationFiles.length > 0, `${implementationFiles.length} implementation file(s)`);

const report = {
  generatedAtUtc: new Date().toISOString(),
  plantProcessApiOffenders,
  phasePathOffenders,
  phaseImportOffenders,
  directImplementationImports,
  missingCssImports,
  oversizedFacades,
  implementationFiles: implementationFiles.sort((a, b) => b.lines - a.lines),
  publicLargeFiles: publicLargeFiles.sort((a, b) => b.lines - a.lines),
  note: "Runtime source only. Tests are excluded from legacy-name checks to avoid self-matching guard literals."
};

fs.writeFileSync(
  path.join(reportDir, "pack-c3-c4-frontend-boundary-validation-report.json"),
  JSON.stringify(report, null, 2),
  "utf8"
);

console.log("");
console.log(`Pack C3/C4 validation passed. implementationFiles=${implementationFiles.length}, publicLargeFiles=${publicLargeFiles.length}`);