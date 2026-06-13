#!/usr/bin/env node
/* PPIQ Phase-3 hygiene gate. Fails (exit 1) if precise debris returns:
 *   - committed *.bak_* files
 *   - _legacy_* folders
 *   - *.runtime.tsx / *.runtime.generated.tsx tombstone siblings
 * (Legitimate thin re-exports / .implementation public-surface splits are allowed
 *  on purpose - this codebase uses them; only the rename-to-close-gate tombstones
 *  are forbidden, and those are identified by the .runtime(.generated) filename.)
 * Usage: node scripts/hygiene-check.mjs [repoRoot=.]
 */
import { readdirSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";

const ROOT = process.argv[2] || ".";
const SKIP_DIRS = new Set(["node_modules", ".git", "dist", "build", ".ppiq-script-backups", ".phase7-backups"]);

function walk(dir, acc = []) {
  let names; try { names = readdirSync(dir); } catch { return acc; }
  for (const name of names) {
    if (SKIP_DIRS.has(name)) continue;
    const full = join(dir, name);
    let st; try { st = statSync(full); } catch { continue; }
    if (st.isDirectory()) walk(full, acc); else acc.push(full);
  }
  return acc;
}
const rel = (f) => relative(ROOT, f).split(sep).join("/");

const offenders = [];
for (const f of walk(ROOT)) {
  const r = rel(f);
  if (/\.bak_\d/.test(r)) offenders.push(`${r} :: committed backup (*.bak_*)`);
  if (/(^|\/)_legacy_/.test(r)) offenders.push(`${r} :: legacy folder`);
  if (/\.runtime\.tsx$|\.runtime\.generated\.tsx$/.test(r)) offenders.push(`${r} :: rename-tombstone sibling (.runtime/.runtime.generated)`);
}

if (offenders.length) {
  console.error("HYGIENE GATE FAILED - debris/tombstones must not be committed:");
  for (const o of offenders) console.error("  " + o);
  process.exit(1);
}
console.log("Hygiene gate passed: no backups, legacy folders, or .runtime tombstones.");