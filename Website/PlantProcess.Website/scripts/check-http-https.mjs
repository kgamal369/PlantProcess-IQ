/* P7-T04 transport check. Local preview must answer 200. If PPIQ_SITE_URL is set,
 * also assert the deployed site serves https 200 and redirects http -> https. */
const LOCAL = process.env.PPIQ_LOCAL_URL || "http://localhost:4173";
const SITE = (process.env.PPIQ_SITE_URL || "").replace(/\/+$/, "");
let fail = 0;
try {
  const r = await fetch(LOCAL, { redirect: "follow" });
  if (r.status === 200) console.log(`local preview 200 OK (${LOCAL})`);
  else { console.error(`local preview not 200: ${r.status}`); fail++; }
} catch (e) { console.error(`local preview unreachable: ${e.message}`); fail++; }

if (SITE) {
  const httpUrl = SITE.replace(/^https:/, "http:");
  try {
    const h = await fetch(httpUrl, { redirect: "manual" });
    if ([301, 302, 307, 308].includes(h.status)) console.log(`http -> https redirect OK (${h.status})`);
    else { console.error(`no http->https redirect: ${h.status}`); fail++; }
  } catch (e) { console.error(`http probe failed: ${e.message}`); fail++; }
  try {
    const s = await fetch(SITE);
    if (s.status === 200) console.log("deployed https 200 OK");
    else { console.error(`deployed https not 200: ${s.status}`); fail++; }
  } catch (e) { console.error(`https probe failed: ${e.message}`); fail++; }
} else {
  console.log("PPIQ_SITE_URL unset -> deployed http/https redirect check skipped (local-only run).");
}
process.exit(fail ? 1 : 0);