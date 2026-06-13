import { test, expect, Page } from "@playwright/test";

// Pages to exercise. Extend if your routes differ.
const ROUTES = [
  "/",
  "/products/yard-warehouse-management",
  "/products/manufacturing-execution-integration",
];

async function noHorizontalOverflow(page: Page) {
  const overflow = await page.evaluate(() => {
    const d = document.documentElement;
    // 2px tolerance for sub-pixel rounding / scrollbars
    return d.scrollWidth - d.clientWidth;
  });
  expect(overflow, "horizontal overflow (scrollWidth - clientWidth)").toBeLessThanOrEqual(2);
}

test.describe("Phase 7 responsive + http/https", () => {
  for (const route of ROUTES) {
    test(`no overflow + reachable CTA @ ${route}`, async ({ page }) => {
      const mixed: string[] = [];
      page.on("console", (m) => {
        const t = m.text();
        if (/Mixed Content|mixed-content/i.test(t)) mixed.push(t);
      });
      const resp = await page.goto(route, { waitUntil: "networkidle" });
      expect(resp?.ok(), `navigation to ${route}`).toBeTruthy();

      await noHorizontalOverflow(page);

      // primary CTA must be present and clickable at this viewport
      const cta = page
        .getByRole("button", { name: /request a demo|book a demo|get a demo|contact|demo/i })
        .or(page.getByTestId("lead-submit"))
        .first();
      await expect(cta, "a primary CTA is visible").toBeVisible();
      await expect(cta, "CTA is enabled / clickable").toBeEnabled();

      expect(mixed, `mixed-content errors on ${route}`).toEqual([]);
    });
  }

  test("lead-capture submits at mobile and desktop widths", async ({ page }) => {
    // captured so the test passes even before a live backend exists
    await page.route("**/api/website/leads", (r) =>
      r.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify({ ok: true }) })
    );
    for (const width of [375, 1440]) {
      await page.setViewportSize({ width, height: 900 });
      await page.goto("/products/yard-warehouse-management", { waitUntil: "networkidle" });
      await page.getByTestId("lead-email").fill("buyer@example.com");
      await page.getByTestId("lead-company").fill("Example Steel");
      await page.getByTestId("lead-submit").click();
      await expect(page.getByTestId("lead-status"), `confirmation @ ${width}px`).toContainText(/touch|thank/i);
    }
  });
});