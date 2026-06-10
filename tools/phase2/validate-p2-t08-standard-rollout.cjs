
const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const marker = "PPIQ_P2_T08_STANDARD_COMPONENT_ROLLOUT_BLOCKING";

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
  console.error("[RED] P2-T08 validation failed: " + message);
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
  "Frontend/PlantProcess.Web/src/components/standard/StandardP2Controls.tsx",
  "Frontend/PlantProcess.Web/src/components/standard/standard-p2-controls.css",
  "tools/ui/audit-ui-instances.cjs",
  "Frontend/PlantProcess.Web/package.json",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const controls = read("Frontend/PlantProcess.Web/src/components/standard/StandardP2Controls.tsx");
const css = read("Frontend/PlantProcess.Web/src/components/standard/standard-p2-controls.css");
const audit = read("tools/ui/audit-ui-instances.cjs");
const pkg = JSON.parse(read("Frontend/PlantProcess.Web/package.json"));

if (!controls.includes(marker)) fail("StandardP2Controls missing marker");
for (const name of ["StandardP2Button", "StandardP2Input", "StandardP2Select", "StandardP2TextArea", "StandardP2Table"]) {
  if (!controls.includes("function " + name)) fail("missing " + name);
}

if (!css.includes("opacity: 0.35")) fail("disabled opacity brand rule missing");
if (!css.includes("border-radius: 8px")) fail("8px radius brand rule missing");
if (!css.includes("font-size: 14px")) fail("Inter 14px rule missing");
if (!css.includes(":focus-visible")) fail("visible focus ring missing");
if (!css.includes("font-weight: 600")) fail("semibold rule missing");

if (!audit.includes("native-control") || !audit.includes("inline-style")) fail("audit script missing native-control/inline-style checks");

if (!pkg.scripts || !pkg.scripts["validate:standard-imports"]) {
  fail("package.json missing validate:standard-imports");
}

if (pkg.scripts["validate:standard-imports"].includes("WARN") || pkg.scripts["validate:standard-imports"].includes("||")) {
  fail("validate:standard-imports is still advisory/non-blocking");
}

run("node", ["tools/ui/audit-ui-instances.cjs", "--fail"], root);

console.log("[GREEN] P2-T08 static validation passed.");
