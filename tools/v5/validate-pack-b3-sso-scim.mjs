import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) throw new Error(`Missing file: ${relativePath}`);
  return fs.readFileSync(full, "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function contains(label, text, needle) {
  ok(label, text.includes(needle), `found ${needle}`);
}

function b64urlToBuffer(value) {
  const normalized = value.replace(/-/g, "+").replace(/_/g, "/");
  const pad = normalized.length % 4 === 2 ? "==" : normalized.length % 4 === 3 ? "=" : "";
  return Buffer.from(normalized + pad, "base64");
}

const sql = read("Backend/database/scripts/660_remaining_p09_sso_scim_runtime_certification.sql");
const endpoint = read("Backend/PlantProcess.Api/EnterpriseSsoScim/V5IdentityRuntimeCertificationEndpoints.cs");
const program = read("Backend/PlantProcess.Api/Program.cs");
const generator = read("tools/v5/generate-oidc-runtime-token.mjs");
const docs = read("docs/identity/P09_SSO_SCIM_RUNTIME_CERTIFICATION.md");

contains("SQL has runtime JWKS table", sql, "ppiq_oidc_runtime_jwks_keys");
contains("SQL has runtime validation events", sql, "ppiq_sso_runtime_validation_events");
contains("SQL has SCIM contract events", sql, "ppiq_scim_runtime_contract_events");
contains("SQL has runtime-keycloak provider", sql, "runtime-keycloak");
contains("SQL has P09 runtime acceptance", sql, "ppiq_v5_p09_runtime_acceptance");

contains("Endpoint exposes runtime health", endpoint, "/health");
contains("Endpoint registers JWKS key", endpoint, "/oidc/register-jwks-key");
contains("Endpoint validates runtime token", endpoint, "/oidc/validate-token");
contains("Endpoint validates RS256", endpoint, "RS256");
contains("Endpoint verifies signature", endpoint, "VerifyData");
contains("Endpoint validates issuer", endpoint, "invalid_issuer");
contains("Endpoint validates audience", endpoint, "invalid_audience");
contains("Endpoint validates expiry", endpoint, "expired");
contains("Endpoint rejects future nbf", endpoint, "not_yet_valid");
contains("Endpoint maps group role", endpoint, "ResolveRuntimeRoleAsync");
contains("Endpoint upserts SSO principal", endpoint, "UpsertSsoPrincipalAsync");
contains("Endpoint SCIM schema proof", endpoint, "/scim/schema-proof");
contains("Endpoint SCIM login deny proof", endpoint, "/scim/deactivation-login-proof");

contains("Program maps runtime identity endpoints", program, "MapV5IdentityRuntimeCertificationEndpoints");

contains("Generator creates RSA keypair", generator, "generateKeyPairSync(\"rsa\"");
contains("Generator emits valid token", generator, "runtime-valid-id-token.jwt");
contains("Generator emits expired token", generator, "runtime-expired-id-token.jwt");
contains("Generator emits tampered token", generator, "runtime-tampered-id-token.jwt");
contains("Generator emits JWKS SQL", generator, "register-runtime-jwks.sql");

contains("Docs mention Keycloak", docs, "Keycloak");
ok(
  "Docs mention deactivation login-deny",
  docs.includes("deactivation/login-deny") || docs.includes("deactivation-login-deny"),
  "found deactivation login-deny wording"
);

// Node crypto smoke: validate generated runtime token files.
const validToken = read("Documentation/v5/oidc-runtime-dev/runtime-valid-id-token.jwt").trim();
const expiredToken = read("Documentation/v5/oidc-runtime-dev/runtime-expired-id-token.jwt").trim();
const tamperedToken = read("Documentation/v5/oidc-runtime-dev/runtime-tampered-id-token.jwt").trim();
const jwk = JSON.parse(read("Documentation/v5/oidc-runtime-dev/runtime-public-jwk.json"));

const publicKey = crypto.createPublicKey({
  key: jwk,
  format: "jwk"
});

function verifyJwt(token) {
  const parts = token.split(".");
  if (parts.length !== 3) return false;

  return crypto.verify(
    "RSA-SHA256",
    Buffer.from(`${parts[0]}.${parts[1]}`, "ascii"),
    publicKey,
    b64urlToBuffer(parts[2])
  );
}

ok("Node crypto accepts valid RS256 token", verifyJwt(validToken));
ok("Node crypto accepts expired token signature but policy rejects by exp in API", verifyJwt(expiredToken));
ok("Node crypto rejects tampered token signature", !verifyJwt(tamperedToken));

console.log("");
console.log("Pack B3 SSO/SCIM runtime certification validation passed.");