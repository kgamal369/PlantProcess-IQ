// P00A-TEST-REGISTER: RETIRE-PENDING-REPLACEMENT
// Date: 2026-05-31T11:07:14.744Z
// Replacement: Auth/data lifecycle tests
// Reason: This file is tracked by the P00A Test Register and should not be treated as a final behavioural test.

const fs = require("node:fs");
const path = require("node:path");

const repo = process.cwd();
const checks = [];

function check(id, description, predicate) {
  let ok = false;
  try { ok = Boolean(predicate()); } catch { ok = false; }
  checks.push({ id, description, ok });
}

function read(rel) { return fs.readFileSync(path.join(repo, rel), "utf8"); }
function exists(rel) { return fs.existsSync(path.join(repo, rel)); }
function contains(rel, token) { return exists(rel) && read(rel).includes(token); }

check("PPIQ-T204", "full auth matrix contains admin/users/widgets/ml routes", () => {
  const t = read("Frontend/PlantProcess.Web/e2e/security/auth-matrix-admin.spec.ts");
  return t.includes("/admin/users/configured-summary") && t.includes("/admin/widgets/proof") && t.includes("/api/ml/foundation/readiness") && t.includes("auth-matrix.json");
});
check("PPIQ-T204", "backend proof endpoints mapped", () => contains("Backend/PlantProcess.Api/Program.cs", "app.MapAdminProofEndpoints();") && exists("Backend/PlantProcess.Api/Endpoints/Admin/AdminProofEndpoints.cs"));
check("PPIQ-T207", "delta integration tests expanded beyond shallow scaffold", () => {
  const t = read("Backend/tests/PlantProcess.Api.IntegrationTests/Import/DeltaImportResumabilityTests.cs");
  return t.includes("MaxRowsSmallBatch") && t.includes("Stage1Stage2AndFullCycle") && t.includes("RejectsAnonymousAccess");
});
check("PPIQ-T208", "static loopback exposure validator exists and README documents nmap proof", () => exists("tools/validation/validate-t208-exposure.cjs") && contains("Infrastructure/deploy/README.md", "nmap -Pn 178.105.152.180"));
check("PPIQ-T209", "multi-grain feature store completion SQL exists", () => contains("Backend/database/scripts/201_phase02_ml_feature_store_v6_completion.sql", "ppiq_ml_refresh_feature_store_v6") && contains("Backend/database/scripts/201_phase02_ml_feature_store_v6_completion.sql", "genealogy_json"));
check("PPIQ-T210", "derived feature definitions exist", () => {
  const t = read("Backend/database/scripts/201_phase02_ml_feature_store_v6_completion.sql");
  return ["chemistry.cev","thermal.true_superheat","casting.speed_mean","casting.speed_delta","operations.residency_minutes","operations.shift"].every(x => t.includes(x));
});
check("PPIQ-T211", "outcome definitions exist", () => {
  const t = read("Backend/database/scripts/201_phase02_ml_feature_store_v6_completion.sql");
  return ["defect.rate_per_m2","defect.class","defect.severity","defect.position","downtime.cascade_minutes","kpi.prime_yield","kpi.energy_per_ton","kpi.throughput"].every(x => t.includes(x));
});
check("PPIQ-T212", "type-aware compute entrypoint and engine fallback exist", () => contains("Backend/database/scripts/201_phase02_ml_feature_store_v6_completion.sql", "ppiq_ml_compute_correlations_v6") && contains("Backend/PlantProcess.Infrastructure/Analytics/PostgresCorrelationComputeEngine.cs", "postgres-v6-type-aware"));

console.log("PlantProcess IQ v6 Phase 01/02 completion validation");
let failed = 0;
for (const c of checks) {
  console.log((c.ok ? "OK   " : "FAIL ") + c.id + " - " + c.description);
  if (!c.ok) failed++;
}
if (failed > 0) process.exit(1);
