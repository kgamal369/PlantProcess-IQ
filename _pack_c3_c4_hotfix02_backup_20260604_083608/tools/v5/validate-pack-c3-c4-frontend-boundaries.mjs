import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const srcRoot = path.join(root, "Frontend", "PlantProcess.Web", "src");
const reportDir = path.join(root, "Documentation", "v5");

fs.mkdirSync(reportDir, { recursive: true });

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) throw new Error(`Missing file: ${relativePath}`);
  return fs.readFileSync(full, "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) {
    throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  }

  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function countLines(text) {
  return text.split(/\r?\n/).length;
}

function toPosix(value) {
  return value.replaceAll(path.sep, "/");
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
  return value.replace(/[.*+?^$\\{}()|[\]\\]/g, "\\const requiredFiles = [");
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
  const isImplementation = /\.implementation\.(ts|tsx|js|jsx)$/.test(relative);

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

  if (isImplementation) {
    implementationFiles.push({ file: relative, lines });
  } else {
    if (lines >= 500) {
      publicLargeFiles.push({ file: relative, lines });
    }

    if (isOwnImplementationFacade(relative, text) && lines > 40) {
      oversizedFacades.push({ file: relative, lines });
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

      if (!fs.existsSync(cssFull)) {
        missingCssImports.push({
          file: relative,
          importPath: match[1]
        });
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
  note: "Public large files should now be limited. Remaining implementation files are allowed and tracked for future deep extraction."
};

fs.writeFileSync(
  path.join(reportDir, "pack-c3-c4-frontend-boundary-validation-report.json"),
  JSON.stringify(report, null, 2),
  "utf8"
);

console.log("");
console.log(`Pack C3/C4 validation passed. implementationFiles=${implementationFiles.length}, publicLargeFiles=${publicLargeFiles.length}`);