// uiConformanceRatchet.test.ts - Sweep C2-2
// RATCHET: raw form controls (D1) and inline style objects (D2) per file must
// never EXCEED the committed baseline, and no NEW file may introduce them.
// Decreases pass; regenerate the baseline (Add-UiRatchetGate.ps1
// -RegenerateBaseline) after each conformance pack to lock in progress.
import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";
import baseline from "./uiConformance.baseline.json";

const SRC = resolve(__dirname, "../..");
const EXCLUDE = /node_modules|dist|_phase9_standardbutton_dedupe_backup|__tests__|\.test\.|[\\/]test[\\/]|\.stories\./;
const STD = /[\\/]components[\\/]standard[\\/]|[\\/]ui[\\/]standard-components|[\\/]components[\\/]brand[\\/]/;
const RAW = /<(input|select|textarea|label)\b/g;
const STYLE = /style=\{\{/g;

function walk(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (EXCLUDE.test(p)) continue;
    if (statSync(p).isDirectory()) walk(p, out);
    else if (p.endsWith(".tsx")) out.push(p);
  }
  return out;
}

describe("UI conformance ratchet (D1 raw controls / D2 inline styles)", () => {
  it("no file exceeds its baseline; no new file introduces violations", () => {
    const base = baseline as Record<string, { d1: number; d2: number }>;
    const offenders: string[] = [];
    for (const file of walk(SRC)) {
      const rel = relative(SRC, file).replace(/\\/g, "/");
      const text = readFileSync(file, "utf8");
      const d1 = STD.test(file) ? 0 : (text.match(RAW) ?? []).length;
      const d2 = (text.match(STYLE) ?? []).length;
      const b = base[rel] ?? { d1: 0, d2: 0 };
      if (d1 > b.d1) offenders.push(`${rel} :: D1 raw controls ${d1} > baseline ${b.d1}`);
      if (d2 > b.d2) offenders.push(`${rel} :: D2 inline styles ${d2} > baseline ${b.d2}`);
    }
    expect(
      offenders,
      `UI ratchet violated - use Standard* primitives, never raw controls/inline styles:\n  ${offenders.join("\n  ")}`
    ).toHaveLength(0);
  });
});