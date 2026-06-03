import { test, expect } from "@playwright/test";
import { phase9Routes, waitForPhase9PageReady } from "../helpers/phase9ActionMatrix";

const visualRoutes = phase9Routes.filter((route) =>
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
    "/brand",
  ].includes(route.path),
);

test.describe("Phase 09 — P9-04 visual regression baseline", () => {
  for (const route of visualRoutes) {
    test(`${route.name} visual baseline`, async ({ page, browserName }) => {
      await page.setViewportSize({ width: 1440, height: 950 });
      await waitForPhase9PageReady(page, route);

      await expect(page).toHaveScreenshot(
        `phase9-${browserName}-${route.path.replaceAll("/", "_").replace(/^_/, "") || "home"}.png`,
        {
          fullPage: true,
          animations: "disabled",
          maxDiffPixelRatio: 0.025,
        },
      );
    });
  }
});