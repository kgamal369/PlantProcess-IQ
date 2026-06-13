import { expect, test } from "@playwright/test";
import { prepareAuthenticatedPage } from "./helpers/hardening";

// PPIQ-T20: performance budgets + progress states at demo scale.
//   PPIQ_PERF_ROUTE      (default "/dashboard")  - heaviest dashboard to profile
//   PPIQ_PERF_BUDGET_MS  (default 8000)          - interactive-load ceiling at demo scale
// Requires the app running (frontend + backend). prepareAuthenticatedPage logs in via the browser
// context request client and seeds demo-mode, so the route resolves to the real dashboard.
const ROUTE = process.env.PPIQ_PERF_ROUTE ?? "/dashboard";
const BUDGET_MS = Number(process.env.PPIQ_PERF_BUDGET_MS ?? "8000");

const LOADING_SELECTOR = [
  '[aria-busy="true"]',
  '.ppiq-std-table-skeleton',
  '[class*="ppiq-skeleton"]',
  '[role="progressbar"]',
].join(", ");

test.describe("PPIQ-T20 performance budgets + progress states", () => {
  test(`heaviest dashboard (${ROUTE}) interactive-loads within ${BUDGET_MS}ms`, async ({ page, request }) => {
    await prepareAuthenticatedPage(page, request);

    const start = Date.now();
    await page.goto(ROUTE, { waitUntil: "domcontentloaded" });
    await page.waitForLoadState("networkidle", { timeout: BUDGET_MS * 2 }).catch(() => {});
    const elapsed = Date.now() - start;

    await expect(page.locator("body")).toBeVisible();
    expect(
      elapsed,
      `${ROUTE} took ${elapsed}ms (budget ${BUDGET_MS}ms). Profile the slow queries / add the ` +
      `indexes the analysis flags, or tune PPIQ_PERF_BUDGET_MS for this hardware.`
    ).toBeLessThanOrEqual(BUDGET_MS);
  });

  test(`a slow ${ROUTE} load shows a progress state (no blank hang)`, async ({ page, request }) => {
    await prepareAuthenticatedPage(page, request);

    await page.route("**/analytics/**", async (route) => {
      await new Promise((r) => setTimeout(r, 1200));
      await route.continue();
    });

    await page.goto(ROUTE, { waitUntil: "commit" });

    const loading = page.locator(LOADING_SELECTOR).first();
    await expect(
      loading,
      "PPIQ-T20: a load slower than ~400ms must show a progress/loading indicator " +
      "(aria-busy / ppiq-skeleton / progressbar), not a blank hang."
    ).toBeVisible({ timeout: BUDGET_MS });
  });
});
