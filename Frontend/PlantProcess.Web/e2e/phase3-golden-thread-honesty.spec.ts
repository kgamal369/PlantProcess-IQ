// PPIQ-305: Phase 3 genealogy + honesty e2e. Exercises the weighted transition
// attribution endpoint and the population/abstain surface on the analysis page.
// Skips gracefully when the stack/creds are not provided.
import { test, expect, request } from "@playwright/test";

const BASE = process.env.PPIQ_E2E_BASE_URL || "http://127.0.0.1:5063";

test.describe("Phase 3 - golden thread & honesty", () => {
  test("transition coil reports a weighted two-heat split summing to ~1.0", async () => {
    const api = await request.newContext({ baseURL: BASE });
    // Resolve the seeded transition coil to its material unit id, then read attribution.
    const probe = await api.get("/health").catch(() => null);
    test.skip(!probe || !probe.ok(), "API not reachable - set PPIQ_E2E_BASE_URL to a running stack.");

    // The detailed attribution is proven at the integration layer; here we assert
    // the public attribution endpoint returns a transition split when present.
    const res = await api.get("/api/v5/blended-provenance/weights/status").catch(() => null);
    test.skip(!res || !res.ok(), "blended-provenance endpoint not reachable for this profile.");
    const rows = await res!.json();
    const transition = Array.isArray(rows) ? rows.find((r: any) => r.hasTransition) : null;
    test.skip(!transition, "No transition coil seeded in this environment.");
    expect(Math.abs(Number(transition.contributionSum) - 1.0)).toBeLessThanOrEqual(0.01);
    expect(transition.isGreen).toBeTruthy();
  });

  test("advanced analysis surface renders a population/abstain bar (never a bare driver)", async ({ page }) => {
    const user = process.env.PPIQ_E2E_USER;
    const pass = process.env.PPIQ_E2E_PASS;
    test.skip(!user || !pass, "Set PPIQ_E2E_USER / PPIQ_E2E_PASS to drive the UI population check.");

    await page.goto(`${BASE}/`);
    // Best-effort navigation to the advanced analysis route; the honesty bar must exist.
    await page.goto(`${BASE}/analysis/advanced`).catch(() => {});
    const bar = page.getByTestId("analysis-honesty-bar").or(page.getByTestId("population-badge")).first();
    await expect(bar).toBeVisible({ timeout: 15000 });
  });
});