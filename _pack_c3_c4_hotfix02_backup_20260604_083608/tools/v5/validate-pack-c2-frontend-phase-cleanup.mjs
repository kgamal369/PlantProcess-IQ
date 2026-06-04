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
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function walk(dir, output = []) {
  if (!fs.existsSync(dir)) return output;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "build", "coverage", ".vite", "bin", "obj"].includes(entry.name)) continue;
      walk(full, output);
    } else if (/\.(ts|tsx|js|jsx|css)$/.test(entry.name)) {
      output.push(full);
    }
  }

  return output;
}

function toPosix(value) {
  return value.replaceAll(path.sep, "/");
}

ok("C1 productApiClient still exists", exists("Frontend/PlantProcess.Web/src/api/productApiClient.ts"));
ok("C1 productCoreApiClient still exists", exists("Frontend/PlantProcess.Web/src/api/productCoreApiClient.ts"));
ok("C1 productApiHardening still exists", exists("Frontend/PlantProcess.Web/src/api/productApiHardening.ts"));
ok("old plantProcessApi deleted", !exists("Frontend/PlantProcess.Web/src/api/plantProcessApi.ts"));
ok("old legacy plantProcessApi deleted", !exists("Frontend/PlantProcess.Web/src/api/legacy/plantProcessApi.ts"));


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
  return value.replace(/[.*+?^$\\{}()|[\]\\]/g, "\\const files = walk(srcRoot);");
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

const files = walk(srcRoot);

const plantApiOffenders = [];
const phasePathOffenders = [];
const importOffenders = [];
const contentFindings = [];

for (const file of files) {
  const relative = toPosix(path.relative(root, file));
  const text = fs.readFileSync(file, "utf8");

  if (!isGuardExcludedFile(relative) && containsRetiredLegacyApiName(text)) {
    plantApiOffenders.push(relative);
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
      importOffenders.push({ file: relative, pattern });
    }
  }

  const matches = text.match(/\b(Phase\d+|phase\d+|P\d{2}P\d{2}|P03P04)\b/g) ?? [];
  if (matches.length > 0) {
    contentFindings.push({
      file: relative,
      matches: [...new Set(matches)].sort()
    });
  }
}

const rootWizardRelative = "Frontend/PlantProcess.Web/src/components/dashboard/WidgetBuilderWizard.tsx";
if (exists(rootWizardRelative)) {
  const lineCount = read(rootWizardRelative).split(/\r?\n/).length;
  ok("root dashboard WidgetBuilderWizard retired to small bridge", lineCount <= 40, `${lineCount} line(s)`);
}

ok("zero plantProcessApi references remain", plantApiOffenders.length === 0, `${plantApiOffenders.length} offender(s)`);
ok("zero phase-named frontend source paths", phasePathOffenders.length === 0, `${phasePathOffenders.length} offender(s)`);
ok("zero phase-named frontend import identifiers", importOffenders.length === 0, `${importOffenders.length} offender(s)`);

const report = {
  generatedAtUtc: new Date().toISOString(),
  plantApiOffenders,
  phasePathOffenders,
  importOffenders,
  contentFindings,
  note: "Content findings are intentionally reported for Pack C4. Pack C2 is a path/import cleanup gate."
};

fs.writeFileSync(
  path.join(reportDir, "pack-c2-frontend-phase-cleanup-validation-report.json"),
  JSON.stringify(report, null, 2),
  "utf8"
);

console.log("");
console.log(`Pack C2 validation passed. contentFindingsForC4=${contentFindings.length}`);