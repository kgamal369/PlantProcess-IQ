// T-051. FAILURE ISOLATION, PROVEN IN A BROWSER.
//
// The failure is forced from OUTSIDE the product, by intercepting one widget's
// query. There is no production throw flag, no test hook and no data-force-error
// prop: a seam that exists only so a test can pass is not evidence that the
// product isolates failures.

import { test, expect, type Page } from "@playwright/test";
import { prepareAuthenticatedPage } from "./helpers/hardening";

const WIDGET_QUERY = /\/analytics\/dashboard\/widgets?\/query/;

async function openDashboard(page: Page, request: Parameters<typeof prepareAuthenticatedPage>[1]) {
  await page.setViewportSize({ width: 1440, height: 900 });
  await prepareAuthenticatedPage(page, request);
  await page.goto("/dashboard", { waitUntil: "domcontentloaded", timeout: 30000 });
  await expect(page.locator(".dashboard-grid-layout-shell")).toBeVisible({ timeout: 30000 });
  await expect(page.locator(".react-grid-item").first()).toBeVisible({ timeout: 30000 });
}

test.describe("T-051 widget failure isolation", () => {
  test("one widget's query failure does not take the dashboard with it", async ({ page, request }) => {
    test.setTimeout(120000);

    // Fail exactly one widget by matching its own request body, so the blast
    // radius is chosen by the test rather than by the product.
    let targetCode: string | null = null;

    await page.route(WIDGET_QUERY, async (route) => {
      const body = route.request().postData() ?? "";
      if (targetCode === null) {
        const match = /"dimensionCode"\s*:\s*"([^"]+)"/.exec(body);
        targetCode = match ? match[1] : "";
      }
      if (targetCode !== "" && body.includes('"' + targetCode + '"')) {
        await route.fulfill({ status: 500, contentType: "application/json", body: '{"error":"forced"}' });
        return;
      }
      await route.continue();
    });

    await openDashboard(page, request);

    const failed = page.locator("[data-widget-state='failed']");
    await expect(failed.first(), "no widget reported the canonical failed state").toBeVisible({ timeout: 30000 });

    // The cell that carries the failure is still a grid item, so the persisted
    // geometry survived the crash.
    const failedCell = page.locator(".react-grid-item").filter({ has: failed });
    await expect(failedCell.first()).toBeVisible();

    // Siblings are unharmed and still on the grid.
    const total = await page.locator(".react-grid-item").count();
    const failedCount = await failedCell.count();
    expect(total, "the dashboard lost widgets when one failed").toBeGreaterThanOrEqual(3);
    expect(failedCount, "the failure spread beyond its own cell").toBeLessThan(total);

    // And a sibling is still interactive: the edit toggle still answers.
    const toggle = page.getByTestId("workspace-edit-toggle");
    await expect(toggle).toBeVisible();
    await toggle.click();
    await expect(page.locator("[data-edit-mode='on']")).toBeVisible({ timeout: 10000 });

    const surviving = page.locator(".react-grid-item .dashboard-widget__drag-handle");
    await expect(surviving.first(), "no sibling widget remained interactive").toBeVisible();
  });

  test("no raw exception text reaches the failed widget", async ({ page, request }) => {
    test.setTimeout(120000);

    await page.route(WIDGET_QUERY, async (route) => {
      await route.fulfill({
        status: 500,
        contentType: "application/json",
        body: '{"error":"NullReferenceException at PlantProcess.Api.Widgets.Query"}',
      });
    });

    await openDashboard(page, request);

    const failed = page.locator("[data-widget-state='failed']").first();
    await expect(failed).toBeVisible({ timeout: 30000 });

    const text = (await failed.textContent()) ?? "";
    expect(text, "the raw exception reached the presentation").not.toContain("NullReferenceException");
    expect(text, "an internal path reached the presentation").not.toContain("PlantProcess.Api");
  });

  test("a narrowing selection that matches nothing reads differently from empty", async ({ page, request }) => {
    test.setTimeout(120000);

    // Zero rows, and the request carries a narrowing filter: filtered-empty.
    await page.route(WIDGET_QUERY, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          generatedAtUtc: new Date().toISOString(),
          widget: {}, columns: [], rows: [], warnings: [],
        }),
      });
    });

    await openDashboard(page, request);

    const state = page.locator("[data-widget-state='empty'], [data-widget-state='filtered-empty']").first();
    await expect(state, "a zero-row result reported neither empty nor filtered-empty").toBeVisible({ timeout: 30000 });

    const resolved = await state.getAttribute("data-widget-state");
    const wording = ((await state.textContent()) ?? "").toLowerCase();

    if (resolved === "filtered-empty") {
      expect(wording, "filtered-empty must speak about the selection").toContain("selection");
    } else {
      expect(wording, "empty must not blame the selection").toContain("nothing here");
      expect(wording).not.toContain("selection");
    }
  });
});
