import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) {
    throw new Error(`Missing required file: ${relativePath}`);
  }

  return fs.readFileSync(full, "utf8");
}

function assertContains(label, text, needle) {
  if (!text.includes(needle)) {
    throw new Error(`${label} missing: ${needle}`);
  }

  console.log(`✓ ${label}: ${needle}`);
}

const p07Sql = read("Backend/database/scripts/560_v5_p07_plant_connector_breadth.sql");
const p08Sql = read("Backend/database/scripts/570_v5_p08_enterprise_identity_mfa_sessions.sql");
const connectors = read("Backend/PlantProcess.Api/PlantConnectors/V5PlantConnectorEndpoints.cs");
const identity = read("Backend/PlantProcess.Api/EnterpriseIdentity/V5EnterpriseIdentityEndpoints.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");
const page = read("Frontend/PlantProcess.Web/src/pages/V5ConnectorsIdentityPage.tsx");

assertContains("P07 connector registry", p07Sql, "ppiq_plant_connectors");
assertContains("P07 read-only constraint", p07Sql, "ck_ppiq_plant_connector_read_only");
assertContains("P07 tag catalog", p07Sql, "ppiq_connector_tag_catalog");
assertContains("P07 truth snapshots", p07Sql, "ppiq_connector_truth_snapshots");
assertContains("P07 backfill jobs", p07Sql, "ppiq_connector_backfill_jobs");
assertContains("P07 checkpoints", p07Sql, "ppiq_connector_sync_checkpoints");
assertContains("P07 samples", p07Sql, "ppiq_connector_telemetry_samples");
assertContains("P07 drift", p07Sql, "ppiq_connector_schema_drift_events");
assertContains("P07 acceptance", p07Sql, "ppiq_v5_p07_acceptance");

assertContains("P07 API group", connectors, "/api/v5/plant-connectors");
assertContains("P07 read-only mock register", connectors, "/mock-historian/register");
assertContains("P07 read-only write rejection", connectors, "P07 read-only enforcement");
assertContains("P07 backfill endpoint", connectors, "/backfill");
assertContains("P07 truth endpoint", connectors, "/truth");
assertContains("P07 idempotent samples", connectors, "ON CONFLICT (connector_id, tag_path, observed_at_utc)");

assertContains("P08 identity policy", p08Sql, "ppiq_identity_policy");
assertContains("P08 TOTP enrollment", p08Sql, "ppiq_mfa_totp_enrollments");
assertContains("P08 recovery code hashes", p08Sql, "ppiq_mfa_recovery_codes");
assertContains("P08 sessions", p08Sql, "ppiq_user_sessions");
assertContains("P08 reuse detection", p08Sql, "ppiq_refresh_token_reuse_events");
assertContains("P08 account protection", p08Sql, "ppiq_account_protection_state");
assertContains("P08 auth audit", p08Sql, "ppiq_auth_audit_events");
assertContains("P08 acceptance", p08Sql, "ppiq_v5_p08_acceptance");

assertContains("P08 API group", identity, "/api/v5/enterprise-identity");
assertContains("P08 TOTP HMAC", identity, "HMACSHA1");
assertContains("P08 MFA enroll", identity, "/mfa/enroll");
assertContains("P08 MFA verify", identity, "/mfa/verify");
assertContains("P08 sessions list", identity, "/sessions");
assertContains("P08 session revoke", identity, "/sessions/{sessionId:guid}/revoke");
assertContains("P08 account protection endpoint", identity, "/account-protection/login-attempt");
assertContains("P08 password policy endpoint", identity, "/password-policy/check");
assertContains("P08 recovery hash", identity, "HashRecoveryCode");

assertContains("Program maps P07", program, "MapV5PlantConnectorEndpoints");
assertContains("Program maps P08", program, "MapV5EnterpriseIdentityEndpoints");

assertContains("Frontend proof", page, "Plant Connector Breadth + Enterprise Identity");
assertContains("Frontend connector action", page, "Register mock historian + backfill");
assertContains("Frontend MFA action", page, "Enroll TOTP MFA");

if (identity.includes("public string Code") || identity.includes("plain_code")) {
  throw new Error("P08 identity code must not introduce plaintext recovery-code storage.");
}

console.log("");
console.log("P07/P08 Doctrine v5 static acceptance passed.");