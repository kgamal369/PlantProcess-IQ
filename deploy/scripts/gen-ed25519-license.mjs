// PlantProcess IQ - DEV-ONLY Ed25519 license generator (matches V5Ed25519LicenseEndpoints verifier).
// Usage: node gen-ed25519-license.mjs <outDir> <kid> <days> <tenantId> <privateKeyPathOrEMPTY>
// If privateKeyPath exists, the keypair is REUSED (committed dev_public.pem stays valid); else generated.
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

const [outDir, kid, daysArg, tenantId, privPathArg] = process.argv.slice(2);
if (!outDir || !kid || !daysArg || !tenantId) {
  console.error('ARGS: <outDir> <kid> <days> <tenantId> [privateKeyPath]');
  process.exit(2);
}
const days = parseInt(daysArg, 10);
const privPath = privPathArg && privPathArg !== 'EMPTY' ? privPathArg : path.join(outDir, 'dev_private.pem');

const b64url = (buf) => Buffer.from(buf).toString('base64').replace(/\+/g,'-').replace(/\//g,'_').replace(/=+$/,'');
const b64std = (buf) => Buffer.from(buf).toString('base64');

fs.mkdirSync(outDir, { recursive: true });

// 1. reuse existing private key if present, else generate a fresh keypair
let publicKey, privateKey;
let reused = false;
if (fs.existsSync(privPath)) {
  privateKey = crypto.createPrivateKey(fs.readFileSync(privPath, 'utf8'));
  publicKey  = crypto.createPublicKey(privateKey);
  reused = true;
} else {
  ({ publicKey, privateKey } = crypto.generateKeyPairSync('ed25519'));
  fs.writeFileSync(privPath, privateKey.export({ type:'pkcs8', format:'pem' }), { mode: 0o600 });
}

// 2. derive raw 32-byte public key -> standard base64 (what verify-offline / DB expect)
const jwk = publicKey.export({ format:'jwk' });
const rawPub = Buffer.from(jwk.x, 'base64url');
if (rawPub.length !== 32) { console.error('FATAL: raw public key is not 32 bytes'); process.exit(3); }
const publicKeyB64 = b64std(rawPub);
fs.writeFileSync(path.join(outDir, 'dev_public.pem'), publicKey.export({ type:'spki', format:'pem' }));
fs.writeFileSync(path.join(outDir, 'dev_public.b64'), publicKeyB64 + '\n');

// 3. mint one signed compact-JWS per tier
const now = new Date();
const exp = new Date(now.getTime() + days*24*3600*1000);
const tiers = [
  { tier:'Light',      file:'light.token',      licenseKey:'PPIQ-DEV-LIGHT' },
  { tier:'Pro',        file:'pro.token',        licenseKey:'PPIQ-DEV-PRO' },
  { tier:'ProPlus',    file:'proplus.token',    licenseKey:'PPIQ-DEV-PROPLUS' },
  { tier:'Enterprise', file:'enterprise.token', licenseKey:'PPIQ-DEV-ENTERPRISE' }
];
const header = { alg:'EdDSA', typ:'license+jws', kid };
const hB64 = b64url(Buffer.from(JSON.stringify(header), 'utf8'));

const manifest = { kid, tenantId, algorithm:'Ed25519', issuedAtUtc:now.toISOString(),
                   expiresAtUtc:exp.toISOString(), reusedExistingKey:reused, publicKeyB64, tokens:[] };

for (const t of tiers) {
  const payload = {
    tenantId, licenseKey:t.licenseKey, tier:t.tier,
    issuedAtUtc:now.toISOString(), expiresAtUtc:exp.toISOString(),
    features:[], limits:{}
  };
  const pB64 = b64url(Buffer.from(JSON.stringify(payload), 'utf8'));
  const signingInput = Buffer.from(hB64 + '.' + pB64, 'ascii');
  const sig = crypto.sign(null, signingInput, privateKey);
  const jws = hB64 + '.' + pB64 + '.' + b64url(sig);

  // self-verify exactly like the C# verifier (raw-32 pubkey, ASCII signing input)
  const recon = crypto.createPublicKey({ key:{ kty:'OKP', crv:'Ed25519', x:rawPub.toString('base64url') }, format:'jwk' });
  const ok = crypto.verify(null, Buffer.from(hB64 + '.' + pB64, 'ascii'),
                           recon, Buffer.from(b64url(sig).replace(/-/g,'+').replace(/_/g,'/'), 'base64'));
  if (!ok) { console.error('FATAL: self-verify failed for ' + t.tier); process.exit(4); }

  fs.writeFileSync(path.join(outDir, t.file), jws + '\n');
  manifest.tokens.push({ tier:t.tier, licenseKey:t.licenseKey, file:t.file, selfVerified:true });
}

fs.writeFileSync(path.join(outDir, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
console.log(JSON.stringify({ ok:true, reusedExistingKey:reused, publicKeyB64, kid,
  tokens:tiers.map(t => t.file) }));