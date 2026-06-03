import { test, expect } from "@playwright/test";
import { phase9Routes, waitForPhase9PageReady } from "./helpers/phase9ActionMatrix";

const breakpoints = [
  { name: "mobile", width: 390, height: 900 },
  { name: "tablet", width: 768, height: 1024 },
  { name: "desktop", width: 1440, height: 950 },
];

const sampledRoutes = phase9Routes.filter((route) =>
  [
    "/dashboard",
    "/admin",
    "/materials",
    "/risk",
    "/data-quality",
    "/correlations",
    "/ml-readiness",
    "/suggestions",
    "/commercial/license",
    "/demo-lifecycle",
  ].includes(route.path),
);

test.describe("Phase 09 — P9-03 responsive, refresh, loading and error-state survival", () => {
  for (const viewport of breakpoints) {
    for (const route of sampledRoutes) {
      test(`${route.name} survives ${viewport.name} viewport`, async ({ page }) => {
        await page.setViewportSize({ width: viewport.width, height: viewport.height });
        await waitForPhase9PageReady(page, route);

        await page.reload({ waitUntil: "domcontentloaded" });
        await page.waitForLoadState("networkidle", { timeout: 20_000 }).catch(() => undefined);

        const metrics = await page.evaluate(() => ({
          scrollWidth: document.documentElement.scrollWidth,
          clientWidth: document.documentElement.clientWidth,
          bodyText: document.body.innerText,
          busyCount: document.querySelectorAll("[aria-busy='true']").length,
        }));

        expect(metrics.scrollWidth, `${route.name} must not have major horizontal overflow`).toBeLessThanOrEqual(
          metrics.clientWidth + 12,
        );

        expect(metrics.bodyText, `${route.name} must not expose raw runtime failure text`).not.toMatch(
          /cannot read properties|unhandled runtime|networkerror when attempting|vite error overlay/i,
        );

        expect(metrics.busyCount, `${route.name} must not leave the page permanently busy after network idle`).toBeLessThan(8);
      });
    }
  }
});