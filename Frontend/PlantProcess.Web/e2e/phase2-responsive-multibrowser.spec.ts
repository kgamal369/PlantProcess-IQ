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
        waitUntil: "networkidle",
        timeout: 30_000,
      });

      await expectCustomerSafeShell(page);

      const body = page.locator("body");

      await expect(body).toBeVisible();

      const matchingContract = phase2CriticalRoutes.find((x) => x.route === route);

      if (matchingContract) {
        await expect(body).toContainText(matchingContract.expectedText, {
          timeout: 15_000,
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