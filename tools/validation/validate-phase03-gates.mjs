import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

let failed = 0;

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function read(relativePath) {
  return fs.readFileSync(path.join(root, relativePath), "utf8");
}

function check(name, condition, detail = "") {
  if (condition) {
    console.log(`✓ ${name}`);
    return;
  }

  failed += 1;
  console.error(`❌ ${name}${detail ? ` (${detail})` : ""}`);
}

console.log("");
console.log("============================================================");
console.log("PlantProcess IQ — v4 Phase 03 Gate");
console.log("Tasks: PPIQ-T113 .. PPIQ-T118");
console.log("============================================================");
console.log("");

const sqlPath = "Backend/database/scripts/130_phase03_two_stage_delta_import_architecture.sql";
const endpointPath = "Backend/PlantProcess.Api/Endpoints/Admin/TwoStageImportEndpoints.cs";
const programPath = "Backend/PlantProcess.Api/Program.cs";
const apiPath = "Frontend/PlantProcess.Web/src/api/twoStageImport/twoStageImport.api.ts";
const panelPath = "Frontend/PlantProcess.Web/src/pages/Admin/TwoStageImportMonitorPanel.tsx";
const jobsTabPath = "Frontend/PlantProcess.Web/src/pages/Admin/AdminJobsMonitorTab.tsx";
const e2ePath = "Frontend/PlantProcess.Web/e2e/api/phase03-two-stage-import.spec.ts";

check("PPIQ-T113 SQL script exists", exists(sqlPath));
check("PPIQ-T114/T115 backend endpoint exists", exists(endpointPath));
check("PPIQ-T117 frontend API exists", exists(apiPath));
check("PPIQ-T117 frontend monitor panel exists", exists(panelPath));
check("PPIQ-T118 E2E spec exists", exists(e2ePath));

if (exists(sqlPath)) {
  const sql = read(sqlPath);

  check("PPIQ-T113 dump_store schema is created", sql.includes("CREATE SCHEMA IF NOT EXISTS dump_store"));
  check("PPIQ-T113 source_table_dump_registry exists", sql.includes("source_table_dump_registry"));
  check("PPIQ-T113 dump source registration function exists", sql.includes("ppiq_register_dump_source"));
  check("PPIQ-T114 stage1 delta import function exists", sql.includes("ppiq_run_stage1_delta_import"));
  check("PPIQ-T114 last-index comparison exists", sql.includes("last_index_value_text") && sql.includes("last_index_column"));
  check("PPIQ-T115 stage2 canonical refresh function exists", sql.includes("ppiq_run_stage2_canonical_refresh"));
  check("PPIQ-T115 processed watermark exists", sql.includes("two_stage_processed_watermarks"));
  check("PPIQ-T116 job telemetry columns exist", sql.includes("last_started_heartbeat_utc") && sql.includes("consecutive_failure_count"));
  check("PPIQ-T118 full cycle function exists", sql.includes("ppiq_run_two_stage_full_cycle"));
}

if (exists(endpointPath)) {
  const endpoint = read(endpointPath);

  check("PPIQ-T114 stage1 endpoint exists", endpoint.includes('/stage1/run'));
  check("PPIQ-T115 stage2 endpoint exists", endpoint.includes('/stage2/run'));
  check("PPIQ-T118 full cycle endpoint exists", endpoint.includes('/run-full-cycle'));
  check("PPIQ-T113 source-tables endpoint exists", endpoint.includes('/source-tables'));
}

if (exists(programPath)) {
  const program = read(programPath);
  check("PPIQ-T114/T115 endpoints are mapped in Program.cs", program.includes("MapTwoStageImportEndpoints"));
}

if (exists(panelPath)) {
  const panel = read(panelPath);
  check("PPIQ-T117 panel has Stage 1 action", panel.includes('Run Stage 1'));
  check("PPIQ-T117 panel has Stage 2 action", panel.includes('Run Stage 2'));
  check("PPIQ-T117 panel has full cycle action", panel.includes('Run Full Cycle'));
  check("PPIQ-T117 panel shows unified jobs", panel.includes("overview.jobs.map"));
}

if (exists(jobsTabPath)) {
  const jobsTab = read(jobsTabPath);
  check("PPIQ-T117 panel is mounted in Jobs Monitor", jobsTab.includes("<TwoStageImportMonitorPanel />"));
}

if (failed > 0) {
  console.error("");
  console.error(`Phase 03 gate failed with ${failed} issue(s).`);
  process.exit(1);
}

console.log("");
console.log("✅ PPIQ v4 Phase 03 gate passed.");