// P00A-TEST-REGISTER: DELETE-ARCHIVED
// ArchivedAtUtc: 2026-05-31T11:07:14.744Z
// OriginalPath: Frontend/PlantProcess.Web/e2e/phase1-route-refresh.spec.ts
// Reason: Duplicate route refresh coverage; consolidate into refresh-survival journey.

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