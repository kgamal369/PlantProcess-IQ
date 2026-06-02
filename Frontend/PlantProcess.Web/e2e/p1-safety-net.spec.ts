// P1-07: navigates every top-level route and asserts the page never shows a raw
// 500, a stack trace, or a forbidden "could not load" message.
// Adjust ROUTES + the login step to match the real router/auth helper.
import { test, expect } from "@playwright/test";

const ROUTES = [
  "/", "/command-center", "/admin/db-links", "/admin/source-objects",
  "/admin/importing", "/admin/schema-configuration", "/admin/jobs",
  "/page-builder", "/widget-builder", "/investigate/material",
  "/investigate/correlation", "/investigate/advanced", "/investigate/suggestions",
  "/assistant", "/admin/license", "/admin/users", "/reports", "/demo",
];

const FORBIDDEN = /could not be loaded|could not load|Unhandled exception|at PlantProcess\.|System\.Exception|HTTP 500|Internal Server Error/i;

test.describe("P1-07 safety net", () => {
  for (const route of ROUTES) {
    test(`no raw error on ${route}`, async ({ page }) => {
      const resp = await page.goto(route, { waitUntil: "domcontentloaded" }).catch(() => null);
      // A missing route may 404 client-side; that is fine. A server 500 is not.
      if (resp) expect(resp.status(), `server status for ${route}`).toBeLessThan(500);
      await page.waitForTimeout(400);
      const body = await page.locator("body").innerText().catch(() => "");
      expect(body, `forbidden error text on ${route}`).not.toMatch(FORBIDDEN);
    });
  }
});
