// PPIQ Phase-3: forbid the rename-to-close-gate TOMBSTONE - a thin re-export whose
// only job is to front a .runtime / .runtime.generated sibling. Legitimate thin
// re-exports (barrels, public-surface X.tsx -> X.implementation) are allowed: this
// codebase uses them deliberately. Fails when a .runtime(.generated) shim is re-added.
import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";

const SRC = join(process.cwd(), "src");

function walk(dir: string, acc: string[] = []): string[] {
  let names: string[]; try { names = readdirSync(dir); } catch { return acc; }
  for (const name of names) {
    if (name === "node_modules") continue;
    const full = join(dir, name);
    let isDir = false; try { isDir = statSync(full).isDirectory(); } catch { /* ignore */ }
    if (isDir) walk(full, acc); else acc.push(full);
  }
  return acc;
}
const rel = (f: string) => relative(process.cwd(), f).split(sep).join("/");

const isReExport = (l: string) =>
  /^export\s+\*\s+from\s+["'`]\.\/[^"'`]+["'`];?$/.test(l) ||
  /^export\s+\{\s*default\s*\}\s+from\s+["'`]\.\/[^"'`]+["'`];?$/.test(l);
const frontsTombstone = (l: string) =>
  /from\s+["'`]\.\/[^"'`]*\.runtime(?:\.generated)?["'`]/.test(l);

describe("PPIQ Phase-3 no rename-tombstone shims", () => {
  const files = walk(SRC).filter(
    (f) => f.endsWith(".tsx") && !f.includes("__tests__") && !f.endsWith(".test.tsx") && !f.endsWith(".stories.tsx"),
  );
  it("no .tsx is a thin re-export fronting a .runtime/.runtime.generated tombstone", () => {
    const offenders: string[] = [];
    for (const f of files) {
      const code = readFileSync(f, "utf8").split(/\r?\n/).map((l) => l.trim())
        .filter((l) => l && !l.startsWith("//") && !l.startsWith("/*") && !l.startsWith("*"));
      const thin = code.length > 0 && code.length < 10 && code.every(isReExport);
      if (thin && code.some(frontsTombstone)) offenders.push(rel(f));
    }
    expect(offenders, `rename-tombstone shims:\n  ${offenders.join("\n  ")}`).toEqual([]);
  });
});