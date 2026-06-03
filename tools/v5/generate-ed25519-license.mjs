import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";

const root = process.cwd();

const args = new Map();
for (let i = 2; i < process.argv.length; i += 1) {
  const current = process.argv[i];
  if (!current.startsWith("--")) continue;

  const key = current.slice(2);
  const value = process.argv[i + 1] && !process.argv[i + 1].startsWith("--")
    ? process.argv[++i]
    : "true";

  args.set(key, value);
}

function arg(name, fallback) {
  return args.get(name) ?? fallback;
}

function b64url(input) {
  return Buffer.from(input)
    .toString("base64")
    .replace(/=/g, "")
    .replace(/\+/g, "-")
    .replace(/\//g, "_");
}

function b64urlToBuffer(value) {
  const padded = value.replace(/-/g, "+").replace(/_/g, "/");
  const pad = padded.length % 4 === 2 ? "==" : padded.length % 4 === 3 ? "=" : "";
  return Buffer.from(padded + pad, "base64");
}

const outDir = path.resolve(root, arg("out", "Documentation/v5/ed25519-license-dev"));
const tenantId = arg("tenant", "00000000-0000-0000-0000-000000000001");
const tier = arg("tier", "ProPlus");
const days = Number(arg("days", "365"));
const instanceId = arg("instance", "");
const keyId = arg("kid", `ppiq-dev-ed25519-${new Date().toISOString().slice(0, 10).replaceAll("-", "")}`);

fs.mkdirSync(outDir, { recursive: true });

const { publicKey, privateKey } = crypto.generateKeyPairSync("ed25519");

const publicJwk = publicKey.export({ format: "jwk" });
const privatePem = privateKey.export({ format: "pem", type: "pkcs8" });
const publicPem = publicKey.export({ format: "pem", type: "spki" });

const rawPublicKey = b64urlToBuffer(publicJwk.x);
const publicKeyB64 = rawPublicKey.toString("base64");

const now = new Date();
const expires = new Date(now.getTime() + days * 24 * 60 * 60 * 1000);

const payload = {
  tenantId,
  licenseKey: `PPIQ-${tenantId.replaceAll("-", "").slice(0, 8).toUpperCase()}-${Date.now()}`,
  tier,
  issuedAtUtc: now.toISOString(),
  expiresAtUtc: expires.toISOString(),
  instanceId: instanceId || undefined,
  features: [
    "ReadOnlySourceRegistry",
    "CsvImport",
    "ExcelImport",
    "PostgreSqlConnector",
    "SchemaSqlViewBuilder",
    "CrossSourceJoinExecution",
    "KpiViewBuilder",
    "WidgetScriptLayer",
    "DashboardPageBuilder",
    "DataQualityFullScan",
    "RiskDashboardView",
    "CorrelationManualRun",
    "InvestigationWorkflow",
    "MlLearningJobs"
  ],
  limits: {
    maxUsers: tier === "Enterprise" ? null : 25,
    maxDataSources: tier === "Enterprise" ? null : 8,
    maxScheduledJobs: tier === "Enterprise" ? null : 25,
    maxDashboards: tier === "Enterprise" ? null : 20,
    minRefreshIntervalMinutes: tier === "Enterprise" ? 1 : 2,
    maxPreviewRows: tier === "Enterprise" ? 100000 : 10000
  }
};

const header = {
  alg: "EdDSA",
  typ: "ppiq-license+jws",
  kid: keyId
};

const signingInput = `${b64url(JSON.stringify(header))}.${b64url(JSON.stringify(payload))}`;
const signature = crypto.sign(null, Buffer.from(signingInput, "ascii"), privateKey);
const compactJws = `${signingInput}.${b64url(signature)}`;

const files = {
  privatePem: path.join(outDir, "ed25519-private-key.pkcs8.pem"),
  publicPem: path.join(outDir, "ed25519-public-key.spki.pem"),
  publicSql: path.join(outDir, "register-public-key.sql"),
  license: path.join(outDir, "plantprocessiq-license-ed25519.jws"),
  payload: path.join(outDir, "plantprocessiq-license-payload.json")
};

fs.writeFileSync(files.privatePem, privatePem, "utf8");
fs.writeFileSync(files.publicPem, publicPem, "utf8");
fs.writeFileSync(files.license, compactJws, "utf8");
fs.writeFileSync(files.payload, JSON.stringify(payload, null, 2), "utf8");

const sql = `
\\encoding UTF8
SET client_encoding = 'UTF8';
SELECT set_config('app.current_tenant', '${tenantId}', false);

INSERT INTO public.ppiq_ed25519_license_public_keys
(
  tenant_id,
  key_id,
  public_key_b64,
  algorithm,
  status
)
VALUES
(
  '${tenantId}',
  '${keyId}',
  '${publicKeyB64}',
  'Ed25519',
  'active'
)
ON CONFLICT (tenant_id, key_id)
DO UPDATE SET
  public_key_b64 = EXCLUDED.public_key_b64,
  algorithm = 'Ed25519',
  status = 'active',
  retired_at_utc = NULL;
`.trim();

fs.writeFileSync(files.publicSql, sql, "utf8");

console.log(JSON.stringify({
  keyId,
  tenantId,
  tier,
  expiresAtUtc: payload.expiresAtUtc,
  publicKeyB64,
  files
}, null, 2));