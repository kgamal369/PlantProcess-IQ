// PPIQ-T12: the navigation contract.
//
// Two rules, both about what a customer can see and click.
//   1. No navigation entry may target a phase-token route. Phase, version and
//      task tokens are our sprint vocabulary, not the product's, and a route
//      carrying one is visible in the browser address bar.
//   2. No customer-visible navigation label or description may carry an
//      internal engineering token. A customer reading "Phase 15" or "step 14"
//      on screen learns that the product is organised around our plan.
//
// This scans AppLayout.tsx, which is where every navigation group is declared.
import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";

const ROOT = process.cwd();
const APP_LAYOUT = join(ROOT, "src", "components", "AppLayout.tsx");

const PHASE_ROUTE = /phase\d+/i;
const INTERNAL_TOKEN = /\bphase\s*\d+\b|\bstep\s*\d+\b|\bp\d{2}\b/i;

/** Strip comments so the guard reads code, never prose. A comment explaining
 *  the rule must never be able to fail the rule. */
function codeOnly(text: string): string {
  return text
    .split("\n")
    .map((line) => line.replace(/\/\/.*$/, ""))
    .join("\n")
    .replace(/\/\*[\s\S]*?\*\//g, "");
}

function source(): string {
  return codeOnly(readFileSync(APP_LAYOUT, "utf8"));
}

describe("PPIQ-T12 navigation contract", () => {
  it("declares navigation entries at all", () => {
    const targets = Array.from(source().matchAll(/\bto:\s*"([^"]+)"/g)).map((m) => m[1]);
    // A positive check beside every forbidding check: if the parse breaks, the
    // suite must fail loudly rather than pass an empty list.
    expect(targets.length).toBeGreaterThan(10);
  });

  it("routes no navigation entry to a phase-token path", () => {
    const targets = Array.from(source().matchAll(/\bto:\s*"([^"]+)"/g)).map((m) => m[1]);
    const offenders = targets.filter((t) => PHASE_ROUTE.test(t));
    expect(offenders).toEqual([]);
  });

  it("keeps internal engineering tokens out of customer-visible strings", () => {
    const strings = Array.from(source().matchAll(/\b(?:label|desc):\s*"([^"]+)"/g)).map((m) => m[1]);
    expect(strings.length).toBeGreaterThan(10);
    const offenders = strings.filter((s) => INTERNAL_TOKEN.test(s));
    expect(offenders).toEqual([]);
  });
});