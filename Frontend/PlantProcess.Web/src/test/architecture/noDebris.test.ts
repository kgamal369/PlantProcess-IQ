// @vitest-environment node
// PPIQ Phase-3: fail if hygiene debris returns to the repo (mirrors scripts/hygiene-check.mjs).
import { describe, expect, it } from "vitest";
import { readdirSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";

const REPO = join(process.cwd(), "..", "..");
const SKIP = new Set(["node_modules", ".git", "dist", "build", ".ppiq-script-backups", ".phase7-backups"]);

function walk(dir: string, acc: string[] = []): string[] {
  let names: string[]; try { names = readdirSync(dir); } catch { return acc; }
  for (const name of names) {
    if (SKIP.has(name)) continue;
    const full = join(dir, name);
    let isDir = false; try { isDir = statSync(full).isDirectory(); } catch { /* ignore */ }
    if (isDir) walk(full, acc); else acc.push(full);
  }
  return acc;
}
const rel = (f: string) => relative(REPO, f).split(sep).join("/");

describe("PPIQ Phase-3 no-debris", () => {
  const files = walk(REPO).map(rel);
  it("no committed *.bak_* files", () => {
    expect(files.filter((f) => /\.bak_\d/.test(f))).toEqual([]);
  });
  it("no _legacy_ folders", () => {
    expect(files.filter((f) => /(^|\/)_legacy_/.test(f))).toEqual([]);
  });
  it("no .runtime/.runtime.generated shim siblings", () => {
    expect(files.filter((f) => /\.runtime\.tsx$|\.runtime\.generated\.tsx$/.test(f))).toEqual([]);
  });
});