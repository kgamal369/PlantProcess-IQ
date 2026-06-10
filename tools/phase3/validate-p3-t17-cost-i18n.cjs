
const fs = require("fs");
const path = require("path");

const root = process.cwd();
const marker = "PPIQ_P3_T17_COST_I18N_CONTRACT";

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
  console.error("[RED] P3-T17 validation failed: " + message);
  process.exit(1);
}

function walk(dir, predicate) {
  const out = [];

  if (!fs.existsSync(dir)) return out;

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const item = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (entry.name === "node_modules" || entry.name === "dist" || entry.name === "bin" || entry.name === "obj") continue;
      out.push(...walk(item, predicate));
    } else if (!predicate || predicate(item)) {
      out.push(item);
    }
  }

  return out;
}

const required = [
  "Frontend/PlantProcess.Web/src/i18n/v5I18n.tsx",
  "Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.ts",
  "Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.test.ts",
  "Backend/database/scripts/425_p3_t17_cost_assumption_i18n_contract.sql",
  "docs/phase3/P3_T17_COST_I18N.md",
];

for (const rel of required) {
  if (!exists(rel)) fail("missing " + rel);
}

const helper = read("Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.ts");
const v5 = read("Frontend/PlantProcess.Web/src/i18n/v5I18n.tsx");
const test = read("Frontend/PlantProcess.Web/src/i18n/p3T17CostAssumptionI18n.test.ts");
const sql = read("Backend/database/scripts/425_p3_t17_cost_assumption_i18n_contract.sql");

if (!helper.includes("P3_T17_COST_I18N_CONTRACT")) fail("helper missing P3-T17 marker");
if (!helper.includes(marker)) fail("helper missing exact marker");
if (!helper.includes('"en"') || !helper.includes('"de"') || !helper.includes('"ar"')) fail("helper missing required locales");
if (!helper.includes("rtl")) fail("helper missing RTL direction");
if (!helper.includes("إدارة افتراضات التكلفة")) fail("helper missing Arabic title");
if (!helper.includes("Kostenannahmen verwalten")) fail("helper missing German title");
if (!helper.includes("p3T17ValidateCostI18nCatalog")) fail("helper missing validation function");

for (const key of [
  "v5.p3.cost.title",
  "v5.p3.cost.description",
  "v5.p3.cost.save",
]) {
  if (!v5.includes(key)) fail("v5I18n.tsx missing " + key);
}

if (!v5.includes('code: "ar"') || !v5.includes('direction: "rtl"')) {
  fail("v5I18n.tsx missing Arabic RTL locale contract");
}

if (!sql.includes(marker)) fail("SQL missing marker");
if (!sql.includes("CREATE TABLE IF NOT EXISTS public.ppiq_i18n_string_keys")) fail("SQL missing i18n string key table");
if (!sql.includes("CREATE TABLE IF NOT EXISTS public.ppiq_i18n_translations")) fail("SQL missing i18n translations table");
if (!sql.includes("ppiq_p3_t17_cost_i18n_status")) fail("SQL missing status function");
if (!sql.includes("v5-p3-cost-assumptions")) fail("SQL missing screen code");
if (!sql.includes("is_high_traffic")) fail("SQL missing high-traffic flag");
if (!sql.includes("إدارة افتراضات التكلفة")) fail("SQL missing Arabic translation");
if (!sql.includes("Kostenbänder speichern")) fail("SQL missing German save translation");

if (!test.includes("p3T17ValidateCostI18nCatalog")) fail("unit test missing catalog validation");
if (!test.includes("toHaveLength")) fail("unit test missing row-count proof");
if (!test.includes("rtl")) fail("unit test missing RTL proof");
if (!test.includes("v5.p3.cost.title")) fail("unit test missing qualified-key proof");

const frontendFiles = walk(full("Frontend/PlantProcess.Web/src"), (file) => file.endsWith(".tsx") || file.endsWith(".ts"));
const frontendJoined = frontendFiles.map((file) => fs.readFileSync(file, "utf8")).join("\n");

if (!frontendJoined.includes('t("v5.p3.cost.title")')) {
  fail("no frontend screen uses t(\"v5.p3.cost.title\")");
}

if (!frontendJoined.includes('t("v5.p3.cost.description")')) {
  fail("no frontend screen uses t(\"v5.p3.cost.description\")");
}

if (!frontendJoined.includes("dir={locale.direction}")) {
  fail("frontend cost/i18n screen does not bind dir to locale.direction");
}

if (!frontendJoined.includes("/api/value/cost-assumptions")) {
  fail("cost-assumption UI/API wiring not found");
}

console.log("[GREEN] P3-T17 static validation passed.");
