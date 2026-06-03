import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) throw new Error(`Missing required file: ${relativePath}`);
  return fs.readFileSync(full, "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function contains(label, text, needle) {
  ok(label, text.includes(needle), `missing ${needle}`);
}

const p13Sql = read("Backend/database/scripts/620_v5_p13_deployment_dr_portability.sql");
const p14Sql = read("Backend/database/scripts/630_v5_p14_compliance_refactor_controls.sql");
const p13Cs = read("Backend/PlantProcess.Api/DeploymentPortability/V5DeploymentPortabilityEndpoints.cs");
const p14Cs = read("Backend/PlantProcess.Api/ComplianceControls/V5ComplianceControlsEndpoints.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");

for (const needle of [
  "ppiq_deployment_airgap_bundles",
  "ppiq_deployment_dr_drills",
  "ppiq_open_format_export_bundles",
  "ppiq_source_code_escrow_records",
  "ppiq_v5_p13_acceptance"
]) {
  contains("P13 SQL", p13Sql, needle);
}

for (const file of [
  "deploy/airgap/docker-compose.airgap.yml",
  "deploy/airgap/IMAGE_MANIFEST.lock",
  "deploy/airgap/install-airgap.ps1",
  "deploy/airgap/preflight-airgap.ps1",
  "deploy/airgap/egress-deny-test.ps1",
  "deploy/airgap/SIZING_TIERS.md",
  "deploy/dr/backup.ps1",
  "deploy/dr/restore.ps1",
  "deploy/dr/ha-reference-compose.yml",
  "deploy/dr/RPO_RTO_RUNBOOK.md",
  "deploy/export/open-format-manifest.schema.json",
  "deploy/export/export-open-format.ps1",
  "docs/escrow/SOURCE_CODE_ESCROW_PROCESS.md"
]) {
  ok(`P13 file exists ${file}`, fs.existsSync(path.join(root, file)));
}

contains("P13 API group", p13Cs, "/api/v5/deployment");
contains("P13 airgap endpoint", p13Cs, "/airgap/bundles");
contains("P13 DR endpoint", p13Cs, "/dr/drill-record");
contains("P13 export endpoint", p13Cs, "/export/open-format");
contains("Program maps P13", program, "MapV5DeploymentPortabilityEndpoints");

for (const needle of [
  "ppiq_audit_events",
  "previous_hash",
  "event_hash",
  "ppiq_data_subject_requests",
  "ppiq_retention_policies",
  "ppiq_control_evidence_matrix",
  "ppiq_product_module_refactor_inventory",
  "ppiq_v5_p14_acceptance"
]) {
  contains("P14 SQL", p14Sql, needle);
}

contains("P14 API group", p14Cs, "/api/v5/compliance");
contains("P14 audit endpoint", p14Cs, "/audit/events");
contains("P14 DSAR endpoint", p14Cs, "/dsar");
contains("P14 retention endpoint", p14Cs, "/retention/run");
contains("P14 evidence endpoint", p14Cs, "/controls/evidence");
contains("P14 tamper hash", p14Cs, "previousHash");
contains("Program maps P14", program, "MapV5ComplianceControlsEndpoints");

for (const file of [
  "docs/compliance/CONTROL_EVIDENCE_MATRIX.md",
  "docs/modules/PRODUCT_MODULE_BOUNDARIES.md",
  "tools/v5/generate-p14-refactor-inventory.mjs"
]) {
  ok(`P14 file exists ${file}`, fs.existsSync(path.join(root, file)));
}

console.log("");
console.log("P13/P14 static acceptance passed.");