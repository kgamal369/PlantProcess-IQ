import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";

const root = process.cwd();

const outDir = path.join(root, "Documentation/v5/oidc-runtime-dev");
fs.mkdirSync(outDir, { recursive: true });

const tenantId = "00000000-0000-0000-0000-000000000001";
const providerCode = "runtime-keycloak";
const issuer = "http://localhost:18080/realms/plantprocessiq";
const audience = "plantprocessiq-web";
const kid = `ppiq-runtime-rs256-${new Date().toISOString().slice(0, 10).replaceAll("-", "")}`;

function b64url(input) {
  return Buffer.from(input)
    .toString("base64")
    .replace(/=/g, "")
    .replace(/\+/g, "-")
    .replace(/\//g, "_");
}

function signJwt(privateKey, payload) {
  const header = {
    alg: "RS256",
    typ: "JWT",
    kid
  };

  const signingInput = `${b64url(JSON.stringify(header))}.${b64url(JSON.stringify(payload))}`;
  const signature = crypto.sign("RSA-SHA256", Buffer.from(signingInput, "ascii"), privateKey);

  return `${signingInput}.${b64url(signature)}`;
}

const { publicKey, privateKey } = crypto.generateKeyPairSync("rsa", {
  modulusLength: 2048,
  publicExponent: 0x10001
});

const publicJwk = publicKey.export({ format: "jwk" });

const now = Math.floor(Date.now() / 1000);

const validPayload = {
  iss: issuer,
  aud: audience,
  sub: "keycloak-user-001",
  email: "quality.engineer@customer.example",
  name: "Quality Engineer",
  groups: ["QualityEngineers"],
  iat: now,
  nbf: now - 30,
  exp: now + 3600
};

const expiredPayload = {
  ...validPayload,
  sub: "keycloak-user-expired",
  iat: now - 7200,
  nbf: now - 7200,
  exp: now - 3600
};

const validToken = signJwt(privateKey, validPayload);
const expiredToken = signJwt(privateKey, expiredPayload);

// Tamper by changing tier/group payload without resigning.
const validParts = validToken.split(".");
const tamperedPayload = {
  ...validPayload,
  groups: ["PlantAdmins"],
  email: "tampered@customer.example"
};
const tamperedToken = `${validParts[0]}.${b64url(JSON.stringify(tamperedPayload))}.${validParts[2]}`;

const sql = `
\\encoding UTF8
SET client_encoding = 'UTF8';

SELECT set_config('app.current_tenant', '${tenantId}', false);

INSERT INTO public.ppiq_oidc_runtime_jwks_keys
(
    tenant_id,
    provider_code,
    key_id,
    issuer,
    audience,
    algorithm,
    jwk_n_b64url,
    jwk_e_b64url,
    status
)
VALUES
(
    '${tenantId}',
    '${providerCode}',
    '${kid}',
    '${issuer}',
    '${audience}',
    'RS256',
    '${publicJwk.n}',
    '${publicJwk.e}',
    'active'
)
ON CONFLICT (tenant_id, provider_code, key_id)
DO UPDATE SET
    issuer = EXCLUDED.issuer,
    audience = EXCLUDED.audience,
    jwk_n_b64url = EXCLUDED.jwk_n_b64url,
    jwk_e_b64url = EXCLUDED.jwk_e_b64url,
    algorithm = 'RS256',
    status = 'active',
    retired_at_utc = NULL;
`.trim();

fs.writeFileSync(path.join(outDir, "runtime-valid-id-token.jwt"), validToken, "utf8");
fs.writeFileSync(path.join(outDir, "runtime-expired-id-token.jwt"), expiredToken, "utf8");
fs.writeFileSync(path.join(outDir, "runtime-tampered-id-token.jwt"), tamperedToken, "utf8");
fs.writeFileSync(path.join(outDir, "runtime-public-jwk.json"), JSON.stringify(publicJwk, null, 2), "utf8");
fs.writeFileSync(path.join(outDir, "register-runtime-jwks.sql"), sql, "utf8");
fs.writeFileSync(path.join(outDir, "runtime-token-metadata.json"), JSON.stringify({
  tenantId,
  providerCode,
  issuer,
  audience,
  kid,
  subject: validPayload.sub,
  email: validPayload.email,
  groups: validPayload.groups
}, null, 2), "utf8");

console.log(JSON.stringify({
  outDir,
  tenantId,
  providerCode,
  issuer,
  audience,
  kid,
  validToken: path.join(outDir, "runtime-valid-id-token.jwt"),
  expiredToken: path.join(outDir, "runtime-expired-id-token.jwt"),
  tamperedToken: path.join(outDir, "runtime-tampered-id-token.jwt"),
  registerSql: path.join(outDir, "register-runtime-jwks.sql")
}, null, 2));