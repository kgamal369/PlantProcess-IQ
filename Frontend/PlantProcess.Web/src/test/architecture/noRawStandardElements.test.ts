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

/**
 * T-250/F5 - one exact file, with its reason, and nothing wider.
 *
 * HeatmapChart is a two-axis intensity matrix. It uses table markup because that
 * is how a matrix stays accessible: scope="col" across the top, scope="row" down
 * the side, and a deliberately empty cell where nothing was observed rather than
 * a fabricated zero. It is not a business data grid and StandardTable would
 * damage its semantics.
 *
 * A directory-wide exemption for src/components/charts/ was rejected: it would
 * quietly license an ordinary raw table anywhere in that folder. This is the
 * exact path only, and the structural test below revokes the exception the
 * moment the component stops being a matrix.
 */
const RAW_TABLE_EXCEPTIONS: ReadonlyArray<{ file: string; reason: string }> = [
  {
    file: "src/components/charts/HeatmapChart.tsx",
    reason:
      "visual heatmap matrix using semantic table markup for two-axis accessibility; not a StandardTable data-grid surface",
  },
];

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
      const excepted = RAW_TABLE_EXCEPTIONS.some((e) => e.file === rel(file));
      if (rawTable.test(text) && !excepted) offenders.push(`${rel(file)} :: raw <table> (use StandardTable)`);
    }

    expect(
      offenders,
      `PPIQ-T11: replace raw elements with Standard* primitives:\n  ${offenders.join("\n  ")}`
    ).toHaveLength(0);
  });

  it("keeps the raw-table exception to exactly one file, still a matrix, with a written reason", () => {
    expect(RAW_TABLE_EXCEPTIONS).toHaveLength(1);

    for (const exception of RAW_TABLE_EXCEPTIONS) {
      expect(exception.file, "a wildcard is not an exact-path exception").not.toContain("*");
      expect(exception.reason.length, "an exception without a reason is an allowlist").toBeGreaterThan(40);

      const text = readFileSync(join(ROOT, exception.file), "utf8");
      // The exception survives only while the component is still a two-axis
      // matrix. If it turns into an ordinary table, this revokes it.
      expect(text, "excepted file no longer uses column headers").toContain(`scope="col"`);
      expect(text, "excepted file no longer uses row headers").toContain(`scope="row"`);
      expect(text, "excepted file no longer renders intensity cells").toContain("heatmap-cell");
    }
  });
});
