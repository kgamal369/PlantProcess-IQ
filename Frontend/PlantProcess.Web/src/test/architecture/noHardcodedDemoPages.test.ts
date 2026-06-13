// PPIQ-T14 Golden Rule: demo pages are authored through the HMI (page-builder -> PageDefinition ->
// DynamicPage at /pages/:slug), never as hardcoded React routes. The guard lives at the repo root
// (scripts/verify-no-hardcoded-demo-pages.mjs). This test locates it by walking up from cwd and runs
// it against this app's src, pinning it into `npm run test` (a CI stage) so a hardcoded demo page
// fails CI instead of slipping past an ad-hoc manual scan.
import { execFileSync } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { describe, expect, it } from "vitest";

function findGuard(): string | null {
  let dir = process.cwd();
  for (let i = 0; i < 6; i++) {
    const candidate = join(dir, "scripts", "verify-no-hardcoded-demo-pages.mjs");
    if (existsSync(candidate)) return candidate;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  return null;
}

describe("PPIQ-T14 Golden Rule: no hardcoded demo pages", () => {
  const guard = findGuard();

  it("the Golden-Rule guard script is present", () => {
    expect(
      guard,
      "scripts/verify-no-hardcoded-demo-pages.mjs not found at or above the frontend"
    ).not.toBeNull();
  });

  it("no demo page is hardcoded as a React route", () => {
    if (!guard) return; // presence asserted above; nothing to run

    let failed = "";
    try {
      // cwd is the frontend root, so srcRoot "src" scans this app's source.
      execFileSync("node", [guard, "src"], { cwd: process.cwd(), stdio: "pipe" });
    } catch (err: unknown) {
      const e = err as { stdout?: Buffer; stderr?: Buffer; message?: string };
      failed =
        (e.stdout?.toString() ?? "") +
        (e.stderr?.toString() ?? "") +
        (e.message ?? "");
    }

    expect(
      failed,
      "PPIQ-T14: a demo page is hardcoded as a React route. Author it through the HMI page-builder " +
      "(PageDefinition -> DynamicPage) instead:\n" + failed
    ).toBe("");
  });
});
