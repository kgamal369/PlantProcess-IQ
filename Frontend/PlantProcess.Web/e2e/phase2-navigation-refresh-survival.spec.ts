// ============================================================
// FILE: Frontend/PlantProcess.Web/e2e/phase2-navigation-refresh-survival.spec.ts
// Task: PPIQ-HARD-031
// ============================================================

import { test } from "@playwright/test";
import {
  gotoCustomerSafeRoute,
  installPhase2StrictGuard,
  phase2CriticalRoutes,
} from "./helpers/phase2Guard";
import { prepareAuthenticatedPage } from "./helpers/hardening";

test.describe("PPIQ-HARD-031 — navigation and browser refresh survival", () => {
  for (const route of phase2CriticalRoutes) {
   test(`${route.name} should load directly and survive refresh`, async ({
      page,
      request,
    }) => {
      await prepareAuthenticatedPage(page, request);

      const guard = installPhase2StrictGuard(page);

      await gotoCustomerSafeRoute(page, route.route, route.expectedText);

      await page.reload({
        waitUntil: "domcontentloaded",
        timeout: 30_000,
      });

      await page
        .waitForLoadState("networkidle", {
          timeout: 8_000,
        })
        .catch(() => {
          // Polling/background retries are acceptable.
        });

      await gotoCustomerSafeRoute(page, route.route, route.expectedText);

      await guard.assertClean();
    });
  }

    test("critical route chain should survive sequential navigation", async ({
      page,
      request,
    }) => {
      await prepareAuthenticatedPage(page, request);

      const guard = installPhase2StrictGuard(page);

      for (const route of phase2CriticalRoutes.filter((x) => x.critical)) {
      await gotoCustomerSafeRoute(page, route.route, route.expectedText);
    }

    await guard.assertClean();
  });
});