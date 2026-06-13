/* P7-T06 short soak: hammer the key routes for N seconds, fail on any 5xx / error. */
const BASE = process.env.PPIQ_LOCAL_URL || "http://localhost:4173";
const ROUTES = (process.env.PPIQ_SOAK_ROUTES || "/,/product,/pricing,/security,/contact").split(",").map((s) => s.trim());
const SECONDS = Number(process.env.PPIQ_SOAK_SECONDS || 45);
const end = Date.now() + SECONDS * 1000;
let n = 0, bad = 0;
while (Date.now() < end) {
  for (const r of ROUTES) {
    try { const res = await fetch(BASE + r); n++; if (res.status >= 500) { bad++; console.error(`BAD ${r} -> ${res.status}`); } }
    catch (e) { bad++; console.error(`ERR ${r} -> ${e.message}`); }
  }
  await new Promise((r) => setTimeout(r, 1000));
}
console.log(`soak: ${n} requests over ${SECONDS}s across ${ROUTES.length} routes, ${bad} failure(s)`);
process.exit(bad ? 1 : 0);