const fs = require("fs");
const path = require("path");
const cp = require("child_process");

const root = process.cwd();
const timestamp = new Date().toISOString().replace(/[-:T.Z]/g, "").slice(0, 14);
const backupRoot = path.join(root, ".pack_b_backup", "repair_pack_b2_import_syntax_" + timestamp);

const frontendRoot = path.join(root, "Frontend", "PlantProcess.Web");
const contentDir = path.join(
  frontendRoot,
  "src",
  "components",
  "dashboard",
  "widget-builder",
  "content"
);

const evidencePath = path.join(root, "docs", "pack-b", "PACK_B_IMPLEMENTATION_EVIDENCE.md");

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

function patch(file, mutator) {
  if (!isFile(file)) {
    console.warn("[WARN] Missing file: " + rel(file));
    return false;
  }

  backup(file);

  const before = read(file).replace(/\r\n/g, "\n");
  const after = mutator(before);

  if (after !== before) {
    write(file, after);
    return true;
  }

  return false;
}

function fixBrokenImportMemberSeparators(text) {
  let next = text;

  const replacements = [
    [/WidgetBuilderState\s+RelativeDateUnit,/g, "WidgetBuilderState,\n  RelativeDateUnit,"],
    [/WizardStep\s+RelativeDateUnit,/g, "WizardStep,\n  RelativeDateUnit,"],
    [/WidgetBuilderState\s+WizardStep,/g, "WidgetBuilderState,\n  WizardStep,"],
    [/WidgetBuilderState\s+ValidationIssue,/g, "WidgetBuilderState,\n  ValidationIssue,"],
    [/selectFieldForDimension\s+toInputDateTime,/g, "selectFieldForDimension,\n  toInputDateTime,"],
    [/toInputDateTime\s+fromInputDateTime,/g, "toInputDateTime,\n  fromInputDateTime,"],
    [/stepOrder\s+formatError,/g, "stepOrder,\n  formatError,"],
    [/formatError\s+fromInputDateTime,/g, "formatError,\n  fromInputDateTime,"],
    [/mapValidationIssues\s+selectFieldForDimension,/g, "mapValidationIssues,\n  selectFieldForDimension,"],
    [/parseJson\s+relativeFromUtc,/g, "parseJson,\n  relativeFromUtc,"],
    [/WidgetBuilderWizardProps,\s*WidgetBuilderState\s+WizardStep,/g, "WidgetBuilderWizardProps,\n  WidgetBuilderState,\n  WizardStep,"]
  ];

  for (const [regex, value] of replacements) {
    next = next.replace(regex, value);
  }

  // Generic cleanup inside import braces only: turn "A   B," into "A,\n  B,"
  next = next.replace(/import\s+(type\s+)?\{([\s\S]*?)\}\s+from\s+("[^"]+"|'[^']+');/gm, (all, typeKeyword, body, source) => {
    let cleaned = body;

    for (let i = 0; i < 10; i += 1) {
      const updated = cleaned.replace(/\b([A-Za-z_][A-Za-z0-9_]*)\s{2,}([A-Za-z_][A-Za-z0-9_]*,)/g, "$1,\n  $2");
      if (updated === cleaned) break;
      cleaned = updated;
    }

    // Normalize duplicate commas and trailing shape.
    cleaned = cleaned
      .replace(/,\s*,/g, ",")
      .replace(/\n\s*\n/g, "\n")
      .trim();

    return "import " + (typeKeyword || "") + "{\n  " + cleaned.replace(/\n/g, "\n  ") + "\n} from " + source + ";";
  });

  return next;
}

function writeEvidence() {
  let evidence = isFile(evidencePath)
    ? read(evidencePath).replace(/\r\n/g, "\n")
    : "# PlantProcess IQ Pack B Evidence\n";

  if (!evidence.includes("PPIQ_PACK_B2_IMPORT_SYNTAX_REPAIR")) {
    evidence += [
      "",
      "## Pack B-2 import syntax repair",
      "",
      "- Marker: `PPIQ_PACK_B2_IMPORT_SYNTAX_REPAIR`.",
      "- Fixed generated malformed import lists such as `WidgetBuilderState RelativeDateUnit` and `stepOrder formatError`.",
      "- No business logic changed.",
      ""
    ].join("\n");

    write(evidencePath, evidence);
  }
}

console.log("=================================================================================================");
console.log("PlantProcess IQ Pack B-2 import syntax repair");
console.log("=================================================================================================");
console.log("Backup folder: " + backupRoot);

ensureDir(backupRoot);

const files = [
  "WidgetBuilderWizardContent.data-steps.tsx",
  "WidgetBuilderWizardContent.filter-step.tsx",
  "WidgetBuilderWizardContent.helpers.ts",
  "WidgetBuilderWizardContent.model.ts",
  "WidgetBuilderWizardContent.preview-steps.tsx",
  "WidgetBuilderWizardContent.view.tsx"
].map((name) => path.join(contentDir, name));

for (const file of files) {
  patch(file, fixBrokenImportMemberSeparators);
}

writeEvidence();

console.log("");
console.log("Current WidgetBuilderWizardContent split line counts:");
for (const file of fs.readdirSync(contentDir).filter((name) => name.startsWith("WidgetBuilderWizardContent."))) {
  const absolute = path.join(contentDir, file);
  if (isFile(absolute)) {
    console.log(" - " + rel(absolute) + ": " + lineCount(absolute));
  }
}

run("Frontend npm build", npmArgs("run", "build"), frontendRoot);

if (isFile(path.join(root, "tools", "phase56", "validate-phase56.cjs"))) {
  run("Phase 5/6 validation", ["node", "tools/phase56/validate-phase56.cjs"]);
}

run("Pack B closure gate preview", ["node", "tools/pack-b/validate-pack-b-p05-closure.cjs"], root, true);

console.log("");
console.log("=================================================================================================");
console.log("Pack B-2 import syntax repair completed.");
console.log("Backup: " + backupRoot);
console.log("=================================================================================================");