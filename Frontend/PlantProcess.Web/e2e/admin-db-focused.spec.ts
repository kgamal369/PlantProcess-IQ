// ============================================================
// File: Frontend/PlantProcess.Web/e2e/admin-db-focused.spec.ts
// Task: FE-HARD-016
//
// Focused QA:
//   - Admin DB configuration route loads directly.
//   - Existing configuration summary renders.
//   - Optimistic save / test connection controls do not crash.
//   - Error/empty/loading states are customer-safe.
// ============================================================

import { expect, test } from "@playwright/test";
import {
  gotoAndAssertCustomerSafePage,
  installHardeningPageGuard,
  prepareAuthenticatedPage,
} from "./helpers/hardening";

test.describe("FE-HARD-016 — AdminDbConfigurationTab focused QA", () => {
  test("admin DB configuration loads safely and exposes DB configuration workflow", async ({
    page,
    request,
  }) => {
    await prepareAuthenticatedPage(page, request);

    const guard = installHardeningPageGuard(page);

    await gotoAndAssertCustomerSafePage(
      page,
      "/admin/db-configuration",
      /database|db configuration|connection|source|admin/i
    );

    const body = page.locator("body");

    await expect(body).toContainText(/database|connection|source|configuration/i, {
      timeout: 15_000,
    });

    const candidateButtons = page.getByRole("button").filter({
      hasText: /test|save|refresh|validate|connection/i,
    });

    const buttonCount = await candidateButtons.count();

    expect(
      buttonCount,
      "Admin DB configuration should expose at least one DB workflow action button"
    ).toBeGreaterThan(0);

    const firstAction = candidateButtons.first();

    if (await firstAction.isEnabled().catch(() => false)) {
      await firstAction.click();

      await page.waitForTimeout(750);

      const text = (await body.innerText()).toLowerCase();

      expect(text).not.toContain("cannot read properties");
      expect(text).not.toContain("is not a function");
      expect(text).not.toContain("uncaught");
      expect(text).not.toContain("stack trace");
    }

    await guard.assertNoUnexpectedFailures();
  });

  test("admin DB configuration shows controlled state when backend fails", async ({
    page,
    request,
  }) => {
    await prepareAuthenticatedPage(page, request);

    await page.route("**/admin/db-configuration/**", async (route) => {
      await route.fulfill({
        status: 503,
        contentType: "application/json",
        body: JSON.stringify({
          message: "Simulated DB configuration failure.",
        }),
      });
    });

    const guard = installHardeningPageGuard(page, {
      allowServerFailureUrlFragments: ["/admin/db-configuration"],
    });

    await page.goto("/admin/db-configuration", {
      waitUntil: "domcontentloaded",
      timeout: 30_000,
    });

    await page.waitForLoadState("networkidle", {
      timeout: 8_000,
    }).catch(() => {
      // Controlled retries are allowed.
    });

    const text = (await page.locator("body").innerText()).toLowerCase();

    expect(text).toMatch(/admin|database|error|failed|unavailable|try again|configuration/);
    expect(text).not.toContain("cannot read properties");
    expect(text).not.toContain("uncaught");
    expect(text).not.toContain("stack trace");

    expect(guard.getPageErrors()).toEqual([]);
  });
});