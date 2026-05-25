import { test } from "@playwright/test";
import {
  gotoCustomerSafeRoute,
  installPhase2StrictGuard,
  phase2CriticalRoutes,
} from "./helpers/phase2Guard";

test.describe("PPIQ-HARD-031 — navigation and browser refresh survival", () => {
  for (const route of phase2CriticalRoutes) {
    test(`${route.name} should load directly and survive refresh`, async ({
      page,
    }) => {
      const guard = installPhase2StrictGuard(page);

      await gotoCustomerSafeRoute(page, route.route, route.expectedText);

      await page.reload({
        waitUntil: "networkidle",
        timeout: 30_000,
      });

      await gotoCustomerSafeRoute(page, route.route, route.expectedText);

      await guard.assertClean();
    });
  }

  test("critical route chain should survive sequential navigation", async ({
    page,
  }) => {
    const guard = installPhase2StrictGuard(page);

    for (const route of phase2CriticalRoutes.filter((x) => x.critical)) {
      await gotoCustomerSafeRoute(page, route.route, route.expectedText);
    }

    await guard.assertClean();
  });
});