const fs = require("fs");
const path = require("path");
const childProcess = require("child_process");

const root = process.cwd();
const marker = "PPIQ_P2_T010_TABLE_STANDARDIZATION";

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
  console.error("[RED] P2-T010 validation failed: " + message);
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
  "Frontend/PlantProcess.Web/src/components/standard/StandardDataTable.tsx",
  "Frontend/PlantProcess.Web/src/components/SortableDataTable.tsx",
  "Frontend/PlantProcess.Web/src/components/standard/index.ts",
  "Frontend/PlantProcess.Web/src/components/standard/standard-components.css",
  "tools/ui/audit-tables.cjs",
  "Frontend/PlantProcess.Web/package.json",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const standard = read("Frontend/PlantProcess.Web/src/components/standard/StandardDataTable.tsx");
const sortable = read("Frontend/PlantProcess.Web/src/components/SortableDataTable.tsx");
const index = read("Frontend/PlantProcess.Web/src/components/standard/index.ts");
const css = read("Frontend/PlantProcess.Web/src/components/standard/standard-components.css");
const pkg = JSON.parse(read("Frontend/PlantProcess.Web/package.json"));

for (const token of [
  marker,
  "StandardDataTableColumn",
  "isLoading",
  "error",
  "emptyText",
  "loadingText",
  "caption",
  "ariaLabel",
  "aria-sort",
  "aria-busy",
  "rowKey",
]) {
  if (!standard.includes(token)) fail("StandardDataTable missing " + token);
}

if (!sortable.includes("StandardDataTable")) fail("SortableDataTable does not delegate to StandardDataTable");
if (sortable.includes("<table")) fail("SortableDataTable still renders raw table markup");
if (sortable.includes("style={")) fail("SortableDataTable still contains inline styles");
if (!sortable.includes("getRowKey")) fail("SortableDataTable missing backward-compatible getRowKey alias");
if (!index.includes("StandardDataTable")) fail("standard index missing StandardDataTable export");

for (const token of [
  marker,
  "ppiq-std-data-table-shell",
  "ppiq-std-data-table__state--loading",
  "ppiq-std-data-table__state--empty",
  "ppiq-std-data-table__state--error",
  "focus-visible",
]) {
  if (!css.includes(token)) fail("standard CSS missing " + token);
}

if (!pkg.scripts || !pkg.scripts["validate:tables"]) {
  fail("package.json missing validate:tables");
}

if (pkg.scripts["validate:tables"].includes("WARN") || pkg.scripts["validate:tables"].includes("||")) {
  fail("validate:tables is advisory/non-blocking");
}

run("node", ["--check", "tools/ui/audit-tables.cjs"], root);
run("node", ["tools/ui/audit-tables.cjs", "--fail"], root);

console.log("[GREEN] P2-T010 static validation passed.");