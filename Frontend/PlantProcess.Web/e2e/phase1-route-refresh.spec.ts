import { test } from "@playwright/test";
import { phase1RouteContracts } from "../src/hardening/routeContracts";
import {
  gotoAndAssertCustomerSafePage,
  installHardeningPageGuard,
  prepareAuthenticatedPage,
  refreshAndAssertStillSafe,
} from "./helpers/phase1Hardening";

test.describe("PPIQ-HARD-001 / HARD-004 — route containment and refresh contract", () => {
  for (const contract of phase1RouteContracts.filter((x) => x.mustRefreshSafely)) {
    test(`${contract.name} should load directly and survive browser refresh`, async ({
      page,
      request,
    }) => {
      await prepareAuthenticatedPage(page, request);

      const guard = installHardeningPageGuard(page);

      await gotoAndAssertCustomerSafePage(
        page,
        contract.route,
        contract.expectedText
      );

      await refreshAndAssertStillSafe(page, contract.expectedText);

      await guard.assertNoUnexpectedFailures();
    });
  }
});