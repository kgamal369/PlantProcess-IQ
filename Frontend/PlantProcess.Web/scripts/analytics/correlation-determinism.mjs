#!/usr/bin/env node
/* PPIQ-PHASE5 correlation determinism harness.
 * Calls the correlation endpoint TWICE with identical inputs and asserts the
 * q-values + method selection are identical. Exits non-zero on drift so CI gates.
 *
 * CONFIG via env (point at your real endpoint + request body):
 *   PPIQ_API_BASE   e.g. http://localhost:5063  (default)
 *   PPIQ_CORR_PATH  e.g. /api/analytics/correlation/compute
 *   PPIQ_BEARER     optional bearer token
 *   PPIQ_CORR_BODY  optional JSON request body (else the sample below is used)
 *   PPIQ_CORR_QFIELD  dotted path to the q array/field in the response (default autodetect)
 */
const BASE = process.env.PPIQ_API_BASE || "http://localhost:5063";
const PATH = process.env.PPIQ_CORR_PATH || "/api/analytics/correlation/compute";
const BEARER = process.env.PPIQ_BEARER || "";
const BODY = process.env.PPIQ_CORR_BODY
  ? JSON.parse(process.env.PPIQ_CORR_BODY)
  : { datasetId: "demo", outcome: "defect_rate", drivers: ["coiling_temp", "finishing_speed", "dwell_minutes"] };

async function call() {
  const res = await fetch(BASE + PATH, {
    method: "POST",
    headers: { "Content-Type": "application/json", ...(BEARER ? { Authorization: `Bearer ${BEARER}` } : {}) },
    body: JSON.stringify(BODY),
  });
  if (!res.ok) throw new Error(`${PATH} -> HTTP ${res.status}`);
  return res.json();
}

// Pull every (driver|id -> method,q) pair we can find, order-independently.
function fingerprint(obj) {
  const out = {};
  const visit = (node) => {
    if (!node || typeof node !== "object") return;
    if (Array.isArray(node)) return node.forEach(visit);
    const key = node.id ?? node.driver ?? node.parameter;
    const q = node.qValue ?? node.q ?? node.fdrQ ?? node.bhQ;
    const m = node.method ?? node.methodUsed;
    if (key != null && (q != null || m != null)) out[String(key)] = { q: q ?? null, m: m ?? null };
    for (const v of Object.values(node)) visit(v);
  };
  visit(obj);
  return out;
}

(async () => {
  const a = fingerprint(await call());
  const b = fingerprint(await call());
  const keys = new Set([...Object.keys(a), ...Object.keys(b)]);
  if (keys.size === 0) { console.error("FAIL: could not find any method/q pairs in the response - set PPIQ_CORR_* CONFIG."); process.exit(1); }
  let diffs = 0;
  for (const k of keys) {
    const x = a[k], y = b[k];
    if (!x || !y || x.q !== y.q || x.m !== y.m) {
      diffs++;
      console.error(`  DRIFT ${k}: run1=${JSON.stringify(x)} run2=${JSON.stringify(y)}`);
    }
  }
  if (diffs > 0) { console.error(`FAIL: ${diffs} non-deterministic result(s).`); process.exit(1); }
  console.log(`OK: correlation deterministic across 2 runs (${keys.size} result(s)).`);
})().catch((e) => { console.error("ERROR:", e.message); process.exit(1); });