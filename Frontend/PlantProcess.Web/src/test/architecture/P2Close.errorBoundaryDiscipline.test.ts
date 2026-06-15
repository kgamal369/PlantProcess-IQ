// PPIQ-202: architecture gate. Fails the build if any catch block under src/pages renders JSX
// inline (bespoke try/catch error UI) instead of delegating to the standard ErrorBoundary /
// DataFetchBoundary. Error UI must come from the standard primitives or from error STATE, never
// from JSX returned directly inside a catch.
import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync, statSync, existsSync } from "node:fs";
import { join, relative } from "node:path";

const ROOT = process.cwd();
const SCAN_ROOT = join(ROOT, "src", "pages");

function walk(dir: string, acc: string[] = []): string[] {
  let names: string[];
  try { names = readdirSync(dir); } catch { return acc; }
  for (const name of names) {
    const full = join(dir, name);
    let isDir = false;
    try { isDir = statSync(full).isDirectory(); } catch { isDir = false; }
    if (isDir) walk(full, acc);
    else acc.push(full);
  }
  return acc;
}

function isScannable(file: string): boolean {
  const r = relative(ROOT, file).replace(/\\/g, "/");
  if (!r.endsWith(".tsx")) return false;
  if (r.includes("/__tests__/") || r.endsWith(".test.tsx") || r.endsWith(".stories.tsx")) return false;
  return true;
}

function catchBodiesRenderingJsx(src: string): string[] {
  const out: string[] = [];
  const re = /catch\s*\([^)]*\)\s*\{/g;
  let m: RegExpExecArray | null;
  while ((m = re.exec(src)) !== null) {
    let i = m.index + m[0].length;
    let depth = 1;
    while (i < src.length && depth > 0) {
      const ch = src[i];
      if (ch === "{") depth++;
      else if (ch === "}") depth--;
      i++;
    }
    const body = src.slice(m.index + m[0].length, i - 1);
    if (/return\s*\(?\s*<[A-Za-z]/.test(body)) out.push(body.trim().slice(0, 80));
  }
  return out;
}

describe("PPIQ-202 error-boundary discipline", () => {
  it("the standard boundaries exist", () => {
    expect(existsSync(join(ROOT, "src/components/standard/ErrorBoundary.tsx"))).toBe(true);
    expect(existsSync(join(ROOT, "src/components/standard/DataFetchBoundary.tsx"))).toBe(true);
  });

  it("no catch block under src/pages renders JSX inline", () => {
    const offenders: string[] = [];
    for (const file of walk(SCAN_ROOT).filter(isScannable)) {
      const hits = catchBodiesRenderingJsx(readFileSync(file, "utf8"));
      if (hits.length) offenders.push(`${relative(ROOT, file).replace(/\\/g, "/")} -> ${hits[0]}`);
    }
    expect(offenders, `bespoke catch-render error UI must move to the standard boundary:\n${offenders.join("\n")}`).toHaveLength(0);
  });
});