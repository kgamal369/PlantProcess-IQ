const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const marker = "PPIQ_P2_T012_FIELDS_INPUTS_STANDARDIZATION";

function full(rel) {
  return path.join(root, rel.replaceAll("/", path.sep));
}

function exists(rel) {
  return fs.existsSync(full(rel));
}

function read(rel) {
  return fs.readFileSync(full(rel), "utf8");
}

function fail(message) {
  console.error("[RED] P2-T012 validation failed: " + message);
  process.exit(1);
}

function run(command, args, cwd = root) {
  const result = childProcess.spawnSync(command, args, {
    cwd,
    encoding: "utf8",
    shell: process.platform === "win32",
  });

  if (result.error) {
    fail(command + " could not start: " + result.error.message);
  }

  if (result.status !== 0) {
    fail(
      command +
        " " +
        args.join(" ") +
        " failed\nSTDOUT:\n" +
        (result.stdout || "") +
        "\nSTDERR:\n" +
        (result.stderr || "")
    );
  }

  return (result.stdout || "") + (result.stderr || "");
}

const required = [
  "Frontend/PlantProcess.Web/src/components/standard/StandardFields.tsx",
  "Frontend/PlantProcess.Web/src/components/standard/index.ts",
  "Frontend/PlantProcess.Web/docs/ui-standards/input-inventory.csv",
  "tools/ui/audit-fields.cjs",
  "Frontend/PlantProcess.Web/package.json",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const fields = read("Frontend/PlantProcess.Web/src/components/standard/StandardFields.tsx");
const index = read("Frontend/PlantProcess.Web/src/components/standard/index.ts");
const csv = read("Frontend/PlantProcess.Web/docs/ui-standards/input-inventory.csv");
const pkg = JSON.parse(read("Frontend/PlantProcess.Web/package.json"));

for (const token of [
  marker,
  "StandardInput",
  "StandardSelect",
  "StandardTextArea",
  "helperText",
  "error",
  "required",
  "aria-invalid",
  "aria-describedby",
  "isLoading",
  "searchable",
  "multiple",
]) {
  if (!fields.includes(token)) fail("StandardFields missing " + token);
}

if (!index.includes("StandardInput") && !index.includes("StandardFields")) {
  fail("standard index missing field exports");
}

const header = csv.split(/\r?\n/)[0] ?? "";

for (const column of [
  "task",
  "file",
  "line",
  "page",
  "currentImplementation",
  "fieldType",
  "labelText",
  "intent",
  "validationBehavior",
  "errorState",
  "helperTextSupport",
  "labelPosition",
  "requiredMarkerStyle",
  "focusRing",
  "standardStatus",
]) {
  if (!header.includes(column)) fail("input-inventory.csv missing column " + column);
}

const rows = csv.trim().split(/\r?\n/).slice(1);
if (rows.length < 3) fail("input-inventory.csv has too few rows: " + rows.length);
if (!csv.includes("StandardInput")) fail("input-inventory.csv must include StandardInput usage");
if (!csv.includes("StandardSelect")) fail("input-inventory.csv must include StandardSelect usage");
if (!csv.includes("StandardTextArea")) fail("input-inventory.csv must include StandardTextArea usage");

if (!pkg.scripts || !pkg.scripts["validate:fields"]) {
  fail("package.json missing validate:fields");
}

if (pkg.scripts["validate:fields"].includes("WARN") || pkg.scripts["validate:fields"].includes("||")) {
  fail("validate:fields is advisory/non-blocking");
}

run("node", ["--check", "tools/ui/audit-fields.cjs"], root);
run("node", ["tools/ui/audit-fields.cjs", "--fail"], root);

console.log("[GREEN] P2-T012 static validation passed.");