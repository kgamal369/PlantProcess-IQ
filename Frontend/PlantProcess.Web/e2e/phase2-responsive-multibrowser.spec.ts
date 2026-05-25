// ============================================================
// FILE: Frontend/PlantProcess.Web/e2e/phase2-responsive-multibrowser.spec.ts
// Task: PPIQ-HARD-028
// ============================================================

import { expect, test } from "@playwright/test";
import {
  expectCustomerSafeShell,
  installPhase2StrictGuard,
  phase2CriticalRoutes,
} from "./helpers/phase2Guard";

const responsiveRoutes = [
  "/dashboard",
  "/admin",
  "/demo-lifecycle",
  "/materials",
  "/data-quality",
];

test.describe("PPIQ-HARD-028 — responsive design audit", () => {
  for (const route of responsiveRoutes) {
    test(`${route} should remain usable on current browser/viewport`, async ({
      page,
    }) => {
      const guard = installPhase2StrictGuard(page);

      await page.goto(route, {
        waitUntil: "domcontentloaded",
        timeout: 30_000,
      });

      await page
        .waitForLoadState("networkidle", {
          timeout: 8_000,
        })
        .catch(() => {
          // Long-lived background requests must not fail responsive audit alone.
        });

      await expectCustomerSafeShell(page);

      const body = page.locator("body");

      const matchingContract = phase2CriticalRoutes.find((x) => x.route === route);

      if (matchingContract) {
        await expect(body).toContainText(matchingContract.expectedText, {
          timeout: 20_000,
        });
      }

      const horizontalOverflow = await page.evaluate(() => {
        return document.documentElement.scrollWidth > window.innerWidth + 8;
      });

      expect(
        horizontalOverflow,
        `${route} should not create uncontrolled horizontal overflow.`
      ).toBeFalsy();

      await guard.assertClean();
    });
  }
});