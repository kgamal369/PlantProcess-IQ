import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const licensePath = path.join(root, "Documentation/v5/ed25519-license-dev/plantprocessiq-license-ed25519.jws");
const outPath = path.join(root, "Documentation/v5/ed25519-license-dev/activate-dev-license-direct.sql");

if (!fs.existsSync(licensePath)) {
  throw new Error(`Missing Pack B1 dev license: ${licensePath}`);
}

function b64urlDecode(value) {
  const normalized = value.replace(/-/g, "+").replace(/_/g, "/");
  const pad = normalized.length % 4 === 2 ? "==" : normalized.length % 4 === 3 ? "=" : "";
  return Buffer.from(normalized + pad, "base64").toString("utf8");
}

function sql(value) {
  if (value === undefined || value === null || value === "") return "NULL";
  return `'${String(value).replaceAll("'", "''")}'`;
}

const jws = fs.readFileSync(licensePath, "utf8").trim();
const parts = jws.split(".");
if (parts.length !== 3) {
  throw new Error("Dev license is not compact JWS.");
}

const header = JSON.parse(b64urlDecode(parts[0]));
const payload = JSON.parse(b64urlDecode(parts[1]));

const tenantId = payload.tenantId;
const licenseKey = payload.licenseKey;
const keyId = header.kid;
const tier = payload.tier;
const issuedAtUtc = payload.issuedAtUtc;
const expiresAtUtc = payload.expiresAtUtc;
const instanceId = payload.instanceId ?? null;
const features = JSON.stringify(payload.features ?? []);
const limits = JSON.stringify(payload.limits ?? {});
const payloadJson = JSON.stringify(payload);

const sqlText = `
\\encoding UTF8
SET client_encoding = 'UTF8';

SELECT set_config('app.current_tenant', '${tenantId}', false);

UPDATE public.ppiq_ed25519_activated_licenses
SET activation_status = 'superseded'
WHERE tenant_id = '${tenantId}'
  AND activation_status = 'active'
  AND license_key <> '${licenseKey}';

INSERT INTO public.ppiq_ed25519_activated_licenses
(
    tenant_id,
    license_key,
    key_id,
    compact_jws,
    tier,
    payload_json,
    features_json,
    limits_json,
    issued_at_utc,
    expires_at_utc,
    instance_id,
    verification_status,
    activation_status,
    verification_error,
    activated_at_utc,
    last_verified_at_utc
)
VALUES
(
    '${tenantId}',
    ${sql(licenseKey)},
    ${sql(keyId)},
    ${sql(jws)},
    ${sql(tier)},
    ${sql(payloadJson)}::jsonb,
    ${sql(features)}::jsonb,
    ${sql(limits)}::jsonb,
    ${sql(issuedAtUtc)}::timestamptz,
    ${sql(expiresAtUtc)}::timestamptz,
    ${sql(instanceId)},
    'valid',
    'active',
    NULL,
    now(),
    now()
)
ON CONFLICT (tenant_id, license_key)
DO UPDATE SET
    key_id = EXCLUDED.key_id,
    compact_jws = EXCLUDED.compact_jws,
    tier = EXCLUDED.tier,
    payload_json = EXCLUDED.payload_json,
    features_json = EXCLUDED.features_json,
    limits_json = EXCLUDED.limits_json,
    issued_at_utc = EXCLUDED.issued_at_utc,
    expires_at_utc = EXCLUDED.expires_at_utc,
    instance_id = EXCLUDED.instance_id,
    verification_status = 'valid',
    activation_status = 'active',
    verification_error = NULL,
    last_verified_at_utc = now();

SELECT tenant_id, license_key, tier, verification_status, activation_status
FROM public.ppiq_v_ed25519_current_entitlements
WHERE tenant_id = '${tenantId}';
`.trim();

fs.writeFileSync(outPath, sqlText, "utf8");

console.log(JSON.stringify({
  outPath,
  tenantId,
  licenseKey,
  keyId,
  tier,
  issuedAtUtc,
  expiresAtUtc
}, null, 2));