const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const marker = "PPIQ_P2_T011_TABS_NAVIGATION_STANDARDIZATION";

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
  console.error("[RED] P2-T011 validation failed: " + message);
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
  "Frontend/PlantProcess.Web/src/components/standard/StandardTabs.tsx",
  "Frontend/PlantProcess.Web/src/components/standard/index.ts",
  "Frontend/PlantProcess.Web/src/components/standard/standard-components.css",
  "Frontend/PlantProcess.Web/docs/ui-standards/tabs-inventory.csv",
  "tools/ui/audit-tabs-navigation.cjs",
  "Frontend/PlantProcess.Web/package.json",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const tabs = read("Frontend/PlantProcess.Web/src/components/standard/StandardTabs.tsx");
const css = read("Frontend/PlantProcess.Web/src/components/standard/standard-components.css");
const index = read("Frontend/PlantProcess.Web/src/components/standard/index.ts");
const csv = read("Frontend/PlantProcess.Web/docs/ui-standards/tabs-inventory.csv");
const pkg = JSON.parse(read("Frontend/PlantProcess.Web/package.json"));

for (const token of [
  marker,
  "StandardTabItem",
  "orientation",
  "variant",
  "badge",
  "disabled",
  "syncSearchParam",
  "mountMode",
  "ArrowRight",
  "ArrowLeft",
  "Home",
  "End",
  "aria-selected",
  "aria-orientation",
  "content",
  "value",
  "searchParam",
  "lazy",
]) {
  if (!tabs.includes(token)) fail("StandardTabs missing " + token);
}

for (const token of [
  marker,
  "ppiq-std-tabs__list",
  "ppiq-std-tabs__item--active",
  "ppiq-std-tabs__badge",
  "focus-visible",
  "@media",
]) {
  if (!css.includes(token)) fail("standard CSS missing " + token);
}

if (!index.includes("StandardTabs")) fail("standard index missing StandardTabs export");

for (const column of [
  "task",
  "file",
  "line",
  "page",
  "currentImplementation",
  "itemCount",
  "navigationType",
  "activeIndicatorStyle",
  "badgeSupport",
  "keyboardNavigation",
  "lazyLoading",
  "responsiveBehavior",
  "standardStatus",
]) {
  if (!csv.split(/\r?\n/)[0].includes(column)) fail("tabs-inventory.csv missing column " + column);
}

const rows = csv.trim().split(/\r?\n/).slice(1);
if (rows.length < 4) fail("tabs-inventory.csv has too few rows: " + rows.length);
if (!csv.includes("AppLayout")) fail("tabs-inventory.csv must include AppLayout primary navigation");
if (!csv.includes("Admin")) fail("tabs-inventory.csv must include Admin in-page navigation");

if (!pkg.scripts || !pkg.scripts["validate:tabs"]) {
  fail("package.json missing validate:tabs");
}

if (pkg.scripts["validate:tabs"].includes("WARN") || pkg.scripts["validate:tabs"].includes("||")) {
  fail("validate:tabs is advisory/non-blocking");
}

run("node", ["--check", "tools/ui/audit-tabs-navigation.cjs"], root);
run("node", ["tools/ui/audit-tabs-navigation.cjs", "--fail"], root);

console.log("[GREEN] P2-T011 static validation passed.");