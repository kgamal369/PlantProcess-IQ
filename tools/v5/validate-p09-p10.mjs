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

const p09Sql = read("Backend/database/scripts/580_v5_p09_enterprise_identity_sso_scim.sql");
const p10Sql = read("Backend/database/scripts/590_v5_p10_signed_license_anti_tamper.sql");
const sso = read("Backend/PlantProcess.Api/EnterpriseSsoScim/V5EnterpriseSsoScimEndpoints.cs");
const licensing = read("Backend/PlantProcess.Api/SignedLicensing/V5SignedLicensingEndpoints.cs");
const page = read("Frontend/PlantProcess.Web/src/pages/V5SsoLicensingPage.tsx");
const program = read("Backend/PlantProcess.Api/Program.cs");

assertContains("P09 SSO provider table", p09Sql, "ppiq_sso_provider_configs");
assertContains("P09 role mappings", p09Sql, "ppiq_sso_role_mappings");
assertContains("P09 principal links", p09Sql, "ppiq_sso_principals");
assertContains("P09 SCIM users", p09Sql, "ppiq_scim_users");
assertContains("P09 SCIM groups", p09Sql, "ppiq_scim_groups");
assertContains("P09 SCIM token hashes", p09Sql, "ppiq_scim_bearer_tokens");
assertContains("P09 provisioning audit", p09Sql, "ppiq_identity_provisioning_audit");
assertContains("P09 acceptance", p09Sql, "ppiq_v5_p09_acceptance");

assertContains("P09 API group", sso, "/api/v5/sso");
assertContains("P09 mock IdP token", sso, "/mock-idp/token");
assertContains("P09 SSO login", sso, "/login");
assertContains("P09 role mapping admin", sso, "/role-mappings");
assertContains("P09 SCIM endpoint", sso, "/api/v5/scim/v2");
assertContains("P09 SCIM Users", sso, "/Users");
assertContains("P09 SCIM Groups", sso, "/Groups");
assertContains("P09 no hard-delete", sso, "deactivated_at_utc");

assertContains("P10 signing key registry", p10Sql, "ppiq_license_signing_keys");
assertContains("P10 signed artifacts", p10Sql, "ppiq_signed_license_artifacts");
assertContains("P10 entitlement projection", p10Sql, "ppiq_license_entitlement_projection");
assertContains("P10 source of truth", p10Sql, "verified_signed_license");
assertContains("P10 license events", p10Sql, "ppiq_license_events");
assertContains("P10 acceptance", p10Sql, "ppiq_v5_p10_acceptance");

assertContains("P10 API group", licensing, "/api/v5/licensing");
assertContains("P10 dev license", licensing, "/dev/create-license");
assertContains("P10 activate", licensing, "/activate");
assertContains("P10 verify", licensing, "/verify");
assertContains("P10 tamper test", licensing, "/tamper-test");
assertContains("P10 ECDSA verifier", licensing, "VerifyPayload");
assertContains("P10 entitlement projection", licensing, "UpsertEntitlementsAsync");
assertContains("P10 canonical payload", licensing, "CanonicalJson");

assertContains("Frontend proof", page, "Enterprise SSO / SCIM + Signed Offline Licensing");
assertContains("Frontend mock SSO", page, "Run mock SSO login");
assertContains("Frontend signed license", page, "Create + activate signed license");

assertContains("Program maps P09", program, "MapV5EnterpriseSsoScimEndpoints");
assertContains("Program maps P10", program, "MapV5SignedLicensingEndpoints");

if (p10Sql.includes("source_of_truth text NOT NULL DEFAULT 'editable_db'")) {
  throw new Error("P10 must not use editable DB tier as entitlement source.");
}

console.log("");
console.log("P09/P10 Doctrine v5 static acceptance passed.");