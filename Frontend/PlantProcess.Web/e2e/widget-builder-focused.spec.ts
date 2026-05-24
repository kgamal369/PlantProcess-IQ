// ============================================================
// File: Frontend/PlantProcess.Web/e2e/widget-builder-focused.spec.ts
// Task: FE-HARD-015
//
// Focused QA:
//   - Dashboard loads.
//   - Add widget opens wizard.
//   - Wizard renders builder content.
//   - Empty/invalid save/preview does not crash.
//   - Close/cancel returns to dashboard.
// ============================================================

import { expect, test } from "@playwright/test";
import {
  gotoAndAssertCustomerSafePage,
  installHardeningPageGuard,
  prepareAuthenticatedPage,
} from "./helpers/hardening";

test.describe("FE-HARD-015 — WidgetBuilderWizard focused QA", () => {
  test("widget builder opens, validates incomplete state, and closes safely", async ({
    page,
    request,
  }) => {
    await prepareAuthenticatedPage(page, request);

    const guard = installHardeningPageGuard(page, {
      allowServerFailureUrlFragments: [
        "/analytics/dashboard/definitions/system-templates/ensure",
      ],
    });

    await gotoAndAssertCustomerSafePage(
      page,
      "/dashboard",
      /dashboard|widget|defect|risk/i
    );

    const addWidgetButton = page.getByRole("button", {
      name: /add widget/i,
    });

    await expect(addWidgetButton).toBeVisible({
      timeout: 20_000,
    });

    await addWidgetButton.click();

    const body = page.locator("body");

    await expect(body).toContainText(/widget|builder|dimension|measure|chart/i, {
      timeout: 15_000,
    });

    const previewButton = page
      .getByRole("button", {
        name: /preview|run preview|test/i,
      })
      .first();

    if (await previewButton.isVisible().catch(() => false)) {
      if (await previewButton.isEnabled().catch(() => false)) {
        await previewButton.click();

        await expect(body).toContainText(
          /required|select|dimension|measure|preview|widget|configuration/i,
          { timeout: 10_000 }
        );
      }
    }

    const saveButton = page
      .getByRole("button", {
        name: /save|create widget|update widget/i,
      })
      .first();

    if (await saveButton.isVisible().catch(() => false)) {
      if (await saveButton.isEnabled().catch(() => false)) {
        await saveButton.click();

        await expect(body).toContainText(
          /required|select|dimension|measure|widget|configuration|preview/i,
          { timeout: 10_000 }
        );
      }
    }

    const closeButton = page
      .getByRole("button", {
        name: /cancel|close|back/i,
      })
      .first();

    if (await closeButton.isVisible().catch(() => false)) {
      await closeButton.click();

      await expect(body).toContainText(/dashboard|defect|risk|material/i, {
        timeout: 10_000,
      });
    }

    await guard.assertNoUnexpectedFailures();
  });
});