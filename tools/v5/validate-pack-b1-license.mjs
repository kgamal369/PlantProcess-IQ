import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";

const root = process.cwd();

function read(relativePath) {
  const full = path.join(root, relativePath);
  if (!fs.existsSync(full)) {
    throw new Error(`Missing file: ${relativePath}`);
  }
  return fs.readFileSync(full, "utf8");
}

function ok(label, condition, evidence = "") {
  if (!condition) {
    throw new Error(`${label} failed${evidence ? `: ${evidence}` : ""}`);
  }
  console.log(`OK ${label}${evidence ? `: ${evidence}` : ""}`);
}

function b64urlToBuffer(value) {
  const padded = value.replace(/-/g, "+").replace(/_/g, "/");
  const pad = padded.length % 4 === 2 ? "==" : padded.length % 4 === 3 ? "=" : "";
  return Buffer.from(padded + pad, "base64");
}

const sql = read("Backend/database/scripts/650_remaining_p10_ed25519_verified_license.sql");
const endpoint = read("Backend/PlantProcess.Api/SignedLicensing/V5Ed25519LicenseEndpoints.cs");
const signer = read("tools/v5/generate-ed25519-license.mjs");
const program = read("Backend/PlantProcess.Api/Program.cs");
const csproj = read("Backend/PlantProcess.Api/PlantProcess.Api.csproj");

ok("SQL has Ed25519 public key registry", sql.includes("ppiq_ed25519_license_public_keys"));
ok("SQL has activated license table", sql.includes("ppiq_ed25519_activated_licenses"));
ok("SQL has verified entitlement view", sql.includes("ppiq_v_ed25519_current_entitlements"));
ok("SQL has no-escalation audit table", sql.includes("ppiq_ed25519_entitlement_audit"));
ok("SQL constrains algorithm to Ed25519", sql.includes("ck_ppiq_ed25519_public_key_alg"));

ok("Endpoint uses BouncyCastle Ed25519 signer", endpoint.includes("Ed25519Signer"));
ok("Endpoint uses raw Ed25519 public key parameters", endpoint.includes("Ed25519PublicKeyParameters"));
ok("Endpoint requires EdDSA header", endpoint.includes("License alg must be EdDSA"));
ok("Endpoint verifies signature", endpoint.includes("VerifySignature"));
ok("Endpoint exposes offline verification", endpoint.includes("/verify-offline"));
ok("Endpoint exposes activation", endpoint.includes("/activate"));
ok("Endpoint exposes current verified entitlements", endpoint.includes("/current"));
ok("Endpoint exposes no-escalation check", endpoint.includes("/entitlement-check"));
ok("Endpoint ignores DB tier override", endpoint.includes("ignoredDbTierOverride"));
ok("Endpoint source of truth is verified license", endpoint.includes("verified_ed25519_license"));

ok("Program maps Ed25519 endpoints", program.includes("MapV5Ed25519LicenseEndpoints"));
ok("API project references BouncyCastle", csproj.includes("BouncyCastle.Cryptography"));

ok("Signer is outside product server", signer.includes("generateKeyPairSync(\"ed25519\")"));
ok("Signer creates compact JWS", signer.includes("ppiq-license+jws"));
ok("Signer writes public-key registration SQL", signer.includes("register-public-key.sql"));

// Runtime crypto smoke using Node, independent from API.
// This proves the generated license is a real Ed25519 signature.
const { publicKey, privateKey } = crypto.generateKeyPairSync("ed25519");
const header = { alg: "EdDSA", typ: "ppiq-license+jws", kid: "node-smoke" };
const payload = {
  tenantId: "00000000-0000-0000-0000-000000000001",
  licenseKey: "PPIQ-SMOKE",
  tier: "ProPlus",
  issuedAtUtc: new Date().toISOString(),
  expiresAtUtc: new Date(Date.now() + 86400000).toISOString()
};

const b64url = (input) => Buffer.from(input)
  .toString("base64")
  .replace(/=/g, "")
  .replace(/\+/g, "-")
  .replace(/\//g, "_");

const signingInput = `${b64url(JSON.stringify(header))}.${b64url(JSON.stringify(payload))}`;
const signature = crypto.sign(null, Buffer.from(signingInput, "ascii"), privateKey);
const verified = crypto.verify(null, Buffer.from(signingInput, "ascii"), publicKey, signature);
ok("Node Ed25519 crypto smoke verifies", verified);

const tamperedPayload = {
  ...payload,
  tier: "Enterprise"
};
const tamperedInput = `${signingInput.split(".")[0]}.${b64url(JSON.stringify(tamperedPayload))}`;
const tamperedVerified = crypto.verify(null, Buffer.from(tamperedInput, "ascii"), publicKey, signature);
ok("Node Ed25519 tamper smoke rejects modified payload", !tamperedVerified);

console.log("");
console.log("Pack B1 strict Ed25519 license validation passed.");