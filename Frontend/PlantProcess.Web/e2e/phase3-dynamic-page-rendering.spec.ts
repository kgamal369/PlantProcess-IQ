import { expect, test } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";

const slug = "e2e-dynamic-rendered-page";

const payload = {
  slug,
  title: "E2E Dynamic Rendered Page",
  visibility: "Shared",
  layoutJson: {
    grid: { columns: 12, rowHeight: 80 },
    widgets: [
      { id: "dyn-risk", kind: "kpi", title: "Dynamic Risk KPI", x: 0, y: 0, w: 3, h: 2, source: "schema_view:risk_summary" },
      { id: "dyn-defects", kind: "bar", title: "Dynamic Defect Breakdown", x: 3, y: 0, w: 5, h: 3, source: "schema_view:defect_breakdown" },
      { id: "dyn-trend", kind: "line", title: "Dynamic Defect Trend", x: 8, y: 0, w: 4, h: 3, source: "schema_view:quality_daily" },
    ],
  },
  widgetBindingsJson: {
    bindings: [
      { widgetId: "dyn-risk", source: "schema_view:risk_summary" },
      { widgetId: "dyn-defects", source: "schema_view:defect_breakdown" },
      { widgetId: "dyn-trend", source: "schema_view:quality_daily" },
    ],
  },
};

test.describe("P03 dynamic /pages/{slug} rendering", () => {
  test("renders a stored backend PageDefinition as a dynamic page", async ({ page, request }) => {
    const token = await login(request);

    await request.delete(apiBaseUrl + "/pages/" + slug, {
      headers: { Authorization: "Bearer " + token },
    }).catch(() => undefined);

    const create = await request.post(apiBaseUrl + "/pages", {
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        Authorization: "Bearer " + token,
      },
      data: payload,
    });

    expect(create.ok(), "create failed: " + create.status() + " " + await create.text()).toBeTruthy();

    /* P01: no browser token seeding; AuthProvider performs cookie refresh/login bootstrap. */

    await page.goto("/pages/" + slug);

    await expect(page.getByRole("heading", { name: "E2E Dynamic Rendered Page" })).toBeVisible({
      timeout: 15_000,
    });

    await expect(page.locator("[data-dynamic-page-status='loaded']")).toBeVisible();
    await expect(page.locator("[data-runtime-widget-id]")).toHaveCount(3);
    await expect(page.locator("[data-runtime-widget-id='dyn-risk']")).toContainText("Dynamic Risk KPI");
    await expect(page.locator("[data-runtime-widget-id='dyn-defects']")).toContainText("schema_view:defect_breakdown");

    const missingSlug = "missing-dynamic-page-" + Date.now();
    await page.goto("/pages/" + missingSlug);
    await expect(page.locator("[data-dynamic-page-status='not-found']")).toBeVisible({
      timeout: 15_000,
    });
  });
});
