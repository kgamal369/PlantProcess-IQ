const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const marker = "PPIQ_P2_T09_ACTION_BUTTON_STANDARDIZATION";

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
  console.error("[RED] P2-T09 validation failed: " + message);
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
  "Frontend/PlantProcess.Web/src/components/standard/StandardButton.tsx",
  "Frontend/PlantProcess.Web/src/components/standard/standard-components.css",
  "tools/phase2/apply-p2-t09-action-buttons.cjs",
  "tools/ui/audit-action-buttons.cjs",
  "Frontend/PlantProcess.Web/e2e/p2-t09-action-button-visual.spec.ts",
  "Frontend/PlantProcess.Web/package.json",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const button = read("Frontend/PlantProcess.Web/src/components/standard/StandardButton.tsx");
const css = read("Frontend/PlantProcess.Web/src/components/standard/standard-components.css");
const visual = read("Frontend/PlantProcess.Web/e2e/p2-t09-action-button-visual.spec.ts");
const pkg = JSON.parse(read("Frontend/PlantProcess.Web/package.json"));

for (const token of [
  marker,
  "isLoading",
  "isDisabled",
  "loadingLabel",
  "aria-busy",
  "data-loading",
  "ppiq-std-button__spinner",
]) {
  if (!button.includes(token)) fail("StandardButton missing " + token);
}

for (const variant of ["primary", "secondary", "ghost", "action", "danger", "success"]) {
  if (!button.includes("\"" + variant + "\"")) fail("StandardButton missing variant " + variant);
}

for (const token of [
  marker,
  "ppiq-std-button--action",
  "ppiq-std-button--is-loading",
  "opacity: 0.35",
  "ppiq-p2t09-material-button",
]) {
  if (!css.includes(token)) fail("standard-components.css missing " + token);
}

if (!pkg.scripts || !pkg.scripts["validate:action-buttons"]) {
  fail("package.json missing validate:action-buttons");
}

if (pkg.scripts["validate:action-buttons"].includes("WARN") || pkg.scripts["validate:action-buttons"].includes("||")) {
  fail("validate:action-buttons is advisory/non-blocking");
}

if (!visual.includes("representativeRoutes") || !visual.includes("Material Investigation")) {
  fail("P2-T09 visual regression spec missing representative route coverage");
}

run("node", ["--check", "tools/phase2/apply-p2-t09-action-buttons.cjs"], root);
run("node", ["--check", "tools/ui/audit-action-buttons.cjs"], root);
run("node", ["tools/ui/audit-action-buttons.cjs", "--fail"], root);

console.log("[GREEN] P2-T09 static validation passed.");