const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".pack_b_backup", "pack_b3_widget_shell_" + timestamp);

const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const widgetShellPath = path.join(
  frontendRoot,
  "src",
  "components",
  "dashboard",
  "widget-builder",
  "WidgetBuilderWizard.implementation.tsx"
);

const contentImplementationPath = path.join(
  frontendRoot,
  "src",
  "components",
  "dashboard",
  "widget-builder",
  "WidgetBuilderWizardContent.implementation.tsx"
);

const evidencePath = path.join(root, "docs", "pack-b", "PACK_B_IMPLEMENTATION_EVIDENCE.md");
const reportPath = path.join(root, "docs", "pack-b", "PACK_B3_WIDGET_SHELL_RETIREMENT_REPORT.json");

function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

function exists(file) {
  return fs.existsSync(file);
}

function isFile(file) {
  return exists(file) && fs.statSync(file).isFile();
}

function read(file) {
  return fs.readFileSync(file, "utf8");
}

function write(file, content) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, content.replace(/\n/g, "\r\n"), "utf8");
  console.log("Wrote: " + rel(file));
}

function rel(file) {
  return path.relative(root, file).split(path.sep).join("/");
}

function backup(file) {
  if (!isFile(file)) return;
  const target = path.join(backupRoot, path.relative(root, file));
  ensureDir(path.dirname(target));
  fs.copyFileSync(file, target);
}

function lineCount(file) {
  return isFile(file) ? read(file).replace(/\r\n/g, "\n").split("\n").length : 0;
}

function run(name, args, cwd = root, optional = false) {
  console.log("");
  console.log("---- " + name);

  try {
    cp.execFileSync(args[0], args.slice(1), {
      cwd,
      stdio: "inherit",
      shell: false
    });
    return { ok: true };
  } catch (error) {
    if (optional) {
      console.warn("[WARN] Optional step failed: " + name);
      console.warn(error.message || String(error));
      return { ok: false, error: error.message || String(error) };
    }

    throw error;
  }
}

function npmArgs(...args) {
  return process.platform === "win32" ? ["cmd", "/c", "npm", ...args] : ["npm", ...args];
}

function writeEvidence(report) {
  write(reportPath, JSON.stringify(report, null, 2) + "\n");

  let evidence = isFile(evidencePath)
    ? read(evidencePath).replace(/\r\n/g, "\n")
    : "# PlantProcess IQ Pack B Evidence\n";

  if (!evidence.includes("PPIQ_PACK_B3_WIDGET_BUILDER_SHELL_RETIREMENT")) {
    evidence += [
      "",
      "## Pack B-3 WidgetBuilderWizard shell retirement",
      "",
      "- Marker: `PPIQ_PACK_B3_WIDGET_BUILDER_SHELL_RETIREMENT`.",
      "- Replaced the duplicate `WidgetBuilderWizard.implementation.tsx` god-file with a thin compatibility wrapper.",
      "- The wrapper delegates to the already-split `WidgetBuilderWizardContent` implementation.",
      "- Reason: the original content implementation already exported `WidgetBuilderWizardContent as WidgetBuilderWizard`, so this is canonicalization, not feature removal.",
      "- Generated report: `docs/pack-b/PACK_B3_WIDGET_SHELL_RETIREMENT_REPORT.json`.",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack B-3 — WidgetBuilderWizard shell retirement");
console.log("=================================================================================================");
console.log("Backup folder: " + backupRoot);

ensureDir(backupRoot);

if (!isFile(widgetShellPath)) {
  throw new Error("Missing widget shell file: " + rel(widgetShellPath));
}

if (!isFile(contentImplementationPath)) {
  throw new Error("Missing split content implementation: " + rel(contentImplementationPath));
}

const beforeLines = lineCount(widgetShellPath);
backup(widgetShellPath);

const wrapper = [
  "/*",
  "  PlantProcess IQ Pack B-3 compatibility wrapper.",
  "  The old WidgetBuilderWizard shell duplicated WidgetBuilderWizardContent.",
  "  WidgetBuilderWizardContent is now the canonical split implementation.",
  "*/",
  "",
  "import WidgetBuilderWizardContent, {",
  "  WidgetBuilderWizardContent as CanonicalWidgetBuilderWizardContent,",
  "  WidgetBuilderWizard as CanonicalWidgetBuilderWizard,",
  "} from \"./WidgetBuilderWizardContent\";",
  "",
  "export const WidgetBuilderWizard = CanonicalWidgetBuilderWizard;",
  "export const WidgetBuilderWizardContent = CanonicalWidgetBuilderWizardContent;",
  "",
  "export default WidgetBuilderWizardContent;",
  ""
].join("\n");

write(widgetShellPath, wrapper);

const report = {
  generatedAtUtc: new Date().toISOString(),
  marker: "PPIQ_PACK_B3_WIDGET_BUILDER_SHELL_RETIREMENT",
  target: rel(widgetShellPath),
  beforeLines,
  afterLines: lineCount(widgetShellPath),
  canonicalImplementation: rel(contentImplementationPath),
  note:
    "This retires the duplicate WidgetBuilderWizard implementation by delegating to the split WidgetBuilderWizardContent implementation."
};

writeEvidence(report);

run("Frontend npm build", npmArgs("run", "build"), frontendRoot);

if (isFile(path.join(root, "tools", "phase56", "validate-phase56.cjs"))) {
  run("Phase 5/6 validation", ["node", "tools/phase56/validate-phase56.cjs"]);
}

run("Pack B closure gate preview", ["node", "tools/pack-b/validate-pack-b-p05-closure.cjs"], root, true);

console.log("");
console.log("=================================================================================================");
console.log("Pack B-3 WidgetBuilderWizard shell retirement completed.");
console.log("Before lines: " + beforeLines);
console.log("After lines : " + lineCount(widgetShellPath));
console.log("Backup      : " + backupRoot);
console.log("=================================================================================================");