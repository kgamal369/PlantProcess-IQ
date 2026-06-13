// PPIQ-T09: no raw load-failure string may survive outside the DataFetchBoundary component.
// Every data-fetch failure must render the branded, retryable boundary. Pins the migrated state.
import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";

const ROOT = process.cwd();
const SCAN_ROOT = join(ROOT, "src");

const ALLOWLIST = [
  "src/components/standard/DataFetchBoundary.tsx",
  "src/components/standard/ErrorBoundary.tsx",
];

const FORBIDDEN = [
  /could ?n.?t load/i,
  /failed to load/i,
  /unable to load/i,
  /loading failed/i,
];

function walk(dir: string, acc: string[] = []): string[] {
  let names: string[];
  try {
    names = readdirSync(dir);
  } catch {
    return acc;
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
  if (!(r.endsWith(".ts") || r.endsWith(".tsx"))) return false;
  if (r.includes("/__tests__/") || r.endsWith(".test.ts") || r.endsWith(".test.tsx")) return false;
  if (r.endsWith(".stories.tsx")) return false;
  if (ALLOWLIST.includes(r)) return false;
  return true;
}

describe("PPIQ-T09 no silent failure", () => {
  const files = walk(SCAN_ROOT).filter(isScannable);

  it("renders the DataFetchBoundary instead of raw load-error strings", () => {
    const offenders: string[] = [];
    for (const file of files) {
      const text = readFileSync(file, "utf8");
      for (const pattern of FORBIDDEN) {
        const m = text.match(pattern);
        if (m) offenders.push(`${rel(file)} :: "${m[0]}" (use DataFetchBoundary)`);
      }
    }
    expect(
      offenders,
      `PPIQ-T09: raw load-error strings found - route through DataFetchBoundary:\n  ${offenders.join("\n  ")}`
    ).toHaveLength(0);
  });
});
