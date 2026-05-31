// P00A-TEST-REGISTER: RETIRE-PENDING-REPLACEMENT
// Date: 2026-05-31T11:07:14.744Z
// Replacement: Data lifecycle tests
// Reason: This file is tracked by the P00A Test Register and should not be treated as a final behavioural test.

const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const failures = [];

function read(rel) {
  const file = path.join(root, rel);
  if (!fs.existsSync(file)) {
    failures.push("Missing file: " + rel);
    return "";
  }
  return fs.readFileSync(file, "utf8");
}

function ok(condition, message) {
  if (!condition) failures.push(message);
}

const compose = read("docker-compose.demo-sources.yml");
for (const service of ["meltshop-postgres","caster-oracle","hsm-oracle","pkl-mssql","excel-yard","downtime-mysql","parsytec-mysql","excel-qa"]) {
  ok(compose.includes(service), "T245 missing demo source service: " + service);
}
ok(!compose.includes("0.0.0.0:"), "T245 demo sources must not publish to 0.0.0.0");
ok(!compose.includes("ports:"), "T245 demo sources should not define public host ports");

const phase2Sql = read("Backend/database/scripts/140_phase02_demo_sources_genealogy_spine.sql");
ok(phase2Sql.includes("ppiq_demo_source_presets"), "T290 source presets table missing");
ok(phase2Sql.includes("ppiq_demo_canonical_layout"), "T261 canonical layout table missing");
ok(phase2Sql.includes("ppiq_demo_genealogy_spine"), "T294 genealogy spine missing");
ok(phase2Sql.includes("ADV_COIL4002"), "T246/T294 known demo coil ADV_COIL4002 missing");
ok(phase2Sql.includes("DEMO_PARSYTEC_MYSQL") && phase2Sql.includes("DEMO_QA_EXCEL"), "T246 source coverage incomplete");

const layoutDoc = read("docs/demo/canonical-layout.md");
ok(layoutDoc.includes("ADV_HEAT4002") && layoutDoc.includes("ADV_COIL4002"), "T261 canonical layout doc missing known lineage");

const sourceDoc = read("docs/demo/demo-source-systems.md");
ok(sourceDoc.includes("eight") || sourceDoc.includes("8"), "T245 source-system doc missing eight-source statement");

const phase3Sql = read("Backend/database/scripts/141_phase03_page_builder_foundation.sql");
ok(phase3Sql.includes("page_definitions"), "T289 page_definitions table missing");
ok(phase3Sql.includes("page_definition_shares"), "T243 page sharing table missing");
ok(phase3Sql.includes("layout_json"), "T240 layout_json missing");
ok(phase3Sql.includes("widget_bindings_json"), "T240 widget bindings missing");
ok(phase3Sql.includes("demo-quality-investigation"), "T240 seeded demo page missing");

const pageApi = read("Frontend/PlantProcess.Web/src/api/pageBuilder/pageBuilder.api.ts");
ok(pageApi.includes("PageDefinitionDto"), "T240 frontend PageDefinition DTO missing");
ok(pageApi.includes("/pages"), "T240 frontend page API missing /pages contract");

const builder = read("Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.tsx");
ok(builder.includes("Widget library"), "T241 widget library missing");
ok(builder.includes("Canvas"), "T241 page canvas missing");
ok(builder.includes("widgetBindingsJson"), "T242 binding payload missing");

const grammar = read("docs/page-builder/widget-script-grammar.md");
ok(grammar.includes("SafeSqlValidator"), "T278 grammar doc missing SafeSqlValidator rule");

const e2e = read("Frontend/PlantProcess.Web/e2e/page-builder-v7.spec.ts");
ok(e2e.includes("page-builder"), "T244 page-builder e2e smoke missing");

if (failures.length) {
  console.error("V7 Phase 2/3 acceptance validation failed:");
  for (const failure of failures) console.error(" - " + failure);
  process.exit(1);
}

console.log("V7 Phase 2/3 static acceptance validation passed.");
console.log("Runtime proof still required: docker compose demo sources, SQL apply, backend build, frontend build, live connector smoke tests.");
