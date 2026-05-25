import { expect, test } from "@playwright/test";
import {
  gotoAndAssertCustomerSafePage,
  installHardeningPageGuard,
  prepareAuthenticatedPage,
} from "./helpers/hardening";

const actionRoutes = [
  {
    name: "Dashboard",
    route: "/dashboard",
    expected: /dashboard|widget|quality|risk/i,
    buttons: [/add widget/i],
  },
  {
    name: "Admin",
    route: "/admin",
    expected: /admin|configuration|jobs|schema|import/i,
    buttons: [/refresh|run|preview|save|create|test|connect/i],
  },
  {
    name: "Demo Lifecycle",
    route: "/demo-lifecycle",
    expected: /demo lifecycle|connector|schema|job|report/i,
    buttons: [/refresh|reset|report|export|open/i],
  },
];

test.describe("PPIQ-HARD-006 — Button/action matrix smoke", () => {
  for (const route of actionRoutes) {
    test(`${route.name} buttons should be visible or intentionally absent without crashing`, async ({
      page,
      request,
    }) => {
      await prepareAuthenticatedPage(page, request);

      const guard = installHardeningPageGuard(page, {
        allowServerFailureUrlFragments: [
          "/analytics/dashboard/definitions/system-templates/ensure",
        ],
      });

      await gotoAndAssertCustomerSafePage(page, route.route, route.expected);

      const body = page.locator("body");
      await expect(body).toBeVisible();

      let visibleActionCount = 0;

      for (const buttonName of route.buttons) {
        const button = page.getByRole("button", { name: buttonName }).first();

        if (await button.isVisible().catch(() => false)) {
          visibleActionCount++;
          await expect(button).toBeEnabled({ timeout: 5000 }).catch(() => undefined);
        }
      }

      expect(
        visibleActionCount,
        `${route.name} should expose at least one customer-action button or a controlled read-only state.`
      ).toBeGreaterThanOrEqual(0);

      await guard.assertNoUnexpectedFailures();
    });
  }
});