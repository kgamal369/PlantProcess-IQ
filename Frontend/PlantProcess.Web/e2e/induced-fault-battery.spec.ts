import { test, expect, Page } from "@playwright/test";

// PPIQ-204: induced-fault battery - 500 -> contained error, slow endpoint -> progress state,
// empty dataset -> empty-insight state - plus a reusable console-cleanliness assertion.
function watchConsole(page: Page) {
  const errors: string[] = [];
  page.on("console", (m) => { if (m.type() === "error") errors.push(m.text()); });
  page.on("pageerror", (e) => errors.push(String(e)));
  return errors;
}

const WIDGET_API = "**/api/**/widgets/**";

test.describe("PPIQ-204 induced-fault battery", () => {
  test("a 500 yields a contained, retryable widget error; siblings survive", async ({ page }) => {
    const errors = watchConsole(page);
    let fail = true;
    await page.route(WIDGET_API, (route) => (fail ? route.fulfill({ status: 500, body: "{}" }) : route.continue()));
    await page.goto("/dashboards");
    await expect(page.getByTestId("widget-error-retry").first()).toBeVisible({ timeout: 15000 });
    await expect(page.getByTestId("widget-ok").first()).toBeVisible();
    fail = false;
    await page.getByTestId("widget-error-retry").first().click();
    await expect(page.getByTestId("widget-error-retry")).toHaveCount(0, { timeout: 15000 });
    expect(errors, "0 console errors/rejections").toHaveLength(0);
  });

  test("a slow endpoint shows a progress state, not a hang", async ({ page }) => {
    await page.route(WIDGET_API, async (route) => { await new Promise((r) => setTimeout(r, 3000)); route.continue(); });
    await page.goto("/dashboards");
    await expect(page.getByTestId("widget-loading").first()).toBeVisible({ timeout: 1500 });
    await expect(page.getByTestId("widget-ok").first()).toBeVisible({ timeout: 15000 });
  });

  test("an empty dataset shows the empty-insight state", async ({ page }) => {
    await page.route(WIDGET_API, (route) => route.fulfill({ status: 200, body: JSON.stringify({ rows: [] }) }));
    await page.goto("/dashboards");
    await expect(page.getByTestId("widget-empty-insight").first()).toBeVisible({ timeout: 15000 });
  });
});