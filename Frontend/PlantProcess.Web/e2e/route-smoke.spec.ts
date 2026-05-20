import { expect, test } from "@playwright/test";

const routes = [
  { path: "/dashboard",      text: /dashboard|plantprocess iq/i },
  { path: "/materials",      text: /material|investigation|plantprocess iq/i },
  { path: "/risk",           text: /risk|plantprocess iq/i },
  { path: "/data-quality",   text: /data quality|quality|plantprocess iq/i },
  { path: "/correlations",   text: /correlation|plantprocess iq/i },
  { path: "/admin",          text: /admin|jobs|configuration|plantprocess iq/i },
  { path: "/demo-lifecycle", text: /lifecycle|connector|ML|PlantProcess IQ/i },
];

test.describe("PlantProcess IQ route smoke regression", () => {
  for (const route of routes) {
    test(`opens ${route.path} without browser errors`, async ({ page }) => {
      const pageErrors: string[] = [];

      page.on("pageerror", (error) => {
        pageErrors.push(error.message);
      });

      await page.goto(route.path);
      await expect(page.locator("body")).toContainText(route.text);

      expect(pageErrors, `Browser page errors on ${route.path}`).toEqual([]);
    });
  }
});