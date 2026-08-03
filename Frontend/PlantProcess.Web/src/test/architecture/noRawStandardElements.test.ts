// @vitest-environment node
// PPIQ-T11: enforce Standard* primitives. Fails if a raw <button> or <table> appears in a page or
// component, where StandardButton / StandardTable exist. The standard/ primitives are excluded -
// they are the canonical place raw elements are allowed. Pins the current clean tree (npm run test).
import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const ROOT = process.cwd();
const SCAN_ROOTS = ["src/pages", "src/components", "src/features"];
const ALLOWLIST_PREFIXES = ["src/components/standard/"];

function walk(dir: string, acc: string[] = []): string[] {
  let names: string[];
  try {
    names = readdirSync(dir);
  } catch {
    return acc; // scan root absent in this checkout -> skip
  }
  for (const name of names) {
    const full = join(dir, name);
    let isDir = false;
    try {
      isDir = statSync(full).isDirectory();
    } catch {
      isDir = false;
    }
    if (isDir) walk(full, acc);
    else acc.push(full);
  }
  return acc;
}

function rel(file: string): string {
  return relative(ROOT, file).replace(/\\/g, "/");
}

function isScannable(file: string): boolean {
  const r = rel(file);
  if (!r.endsWith(".tsx")) return false;
  if (r.includes("/__tests__/") || r.endsWith(".test.tsx") || r.endsWith(".stories.tsx")) return false;
  if (ALLOWLIST_PREFIXES.some((p) => r.startsWith(p))) return false;
  return true;
}

describe("PPIQ-T11 design-system enforcement", () => {
  const files = SCAN_ROOTS.flatMap((r) => walk(join(ROOT, r))).filter(isScannable);

  it("scans at least one page/component (guard is actually wired)", () => {
    expect(files.length).toBeGreaterThan(0);
  });

  it("uses StandardButton / StandardTable instead of raw <button>/<table>", () => {
    const rawButton = /<button[\s/>]/;
    const rawTable = /<table[\s/>]/;
    const offenders: string[] = [];

    for (const file of files) {
      const text = readFileSync(file, "utf8");
      if (rawButton.test(text)) offenders.push(`${rel(file)} :: raw <button> (use StandardButton)`);
      if (rawTable.test(text)) offenders.push(`${rel(file)} :: raw <table> (use StandardTable)`);
    }

    expect(
      offenders,
      `PPIQ-T11: replace raw elements with Standard* primitives:\n  ${offenders.join("\n  ")}`
    ).toHaveLength(0);
  });
});
