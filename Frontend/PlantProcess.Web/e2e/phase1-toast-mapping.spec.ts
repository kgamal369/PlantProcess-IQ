// ============================================================
// FILE: Frontend/PlantProcess.Web/e2e/phase1-toast-mapping.spec.ts
// Task: PPIQ-HARD-002
// ============================================================

import { expect, test } from "@playwright/test";
import {
  installHardeningPageGuard,
  prepareAuthenticatedPage,
} from "./helpers/phase1Hardening";

test.describe("PPIQ-HARD-002 — toast notification mapping", () => {
  test("customer-safe toast root should exist and app should not stack fatal UI errors", async ({
    page,
    request,
  }) => {
    await prepareAuthenticatedPage(page, request);

    const guard = installHardeningPageGuard(page);

    await page.goto("/dashboard", {
      waitUntil: "domcontentloaded",
      timeout: 30_000,
    });

    await page
      .waitForLoadState("networkidle", {
        timeout: 8_000,
      })
      .catch(() => {
        // Background dashboard loading/polling is acceptable.
      });

    await expect(page.locator("body")).toBeVisible({
      timeout: 20_000,
    });

    const toastRootCandidate = page.locator("[data-sonner-toaster]");

    await expect(toastRootCandidate).toHaveCount(1, {
      timeout: 10_000,
    });

    await guard.assertNoUnexpectedFailures();
  });
});