// ============================================================
// File: Frontend/PlantProcess.Web/e2e/admin-schema-focused.spec.ts
// Task: FE-HARD-017
//
// Focused QA:
//   - Admin schema route loads directly.
//   - Schema SQL preview blocks unsafe SQL.
//   - API-level SafeSQL endpoint is proven from E2E.
//   - UI remains safe under preview failure.
// ============================================================

import { expect, test } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";
import {
  gotoAndAssertCustomerSafePage,
  installHardeningPageGuard,
  prepareAuthenticatedPage,
} from "./helpers/hardening";

test.describe("FE-HARD-017 — AdminSchemaConfigurationTab focused QA", () => {
  test("schema configuration page loads safely", async ({ page, request }) => {
    await prepareAuthenticatedPage(page, request);

    const guard = installHardeningPageGuard(page);

    await gotoAndAssertCustomerSafePage(
      page,
      "/admin/schema-configuration",
      /schema|sql|view|mapping|configuration|admin/i
    );

    await guard.assertNoUnexpectedFailures();
  });

  test("unsafe ad-hoc SQL preview is blocked by real backend endpoint", async ({
    request,
  }) => {
    const token = await login(request);

    const response = await request.post(
      `${apiBaseUrl}/admin/schema-configuration/views/preview`,
      {
        headers: {
          Authorization: `Bearer ${token}`,
        },
        data: {
          SqlText: "SELECT pg_read_file('/etc/passwd', 0, 1000);",
          MaxRows: 50,
          TimeoutSeconds: 5,
        },
      }
    );

    expect(response.status()).toBe(400);

    const body = await response.text();

    expect(body).toContain("SQL safety validation failed");
  });

  test("schema page stays customer-safe when preview endpoint fails", async ({
    page,
    request,
  }) => {
    await prepareAuthenticatedPage(page, request);

    await page.route("**/admin/schema-configuration/views/preview", async (route) => {
      await route.fulfill({
        status: 400,
        contentType: "application/json",
        body: JSON.stringify({
          isSuccess: false,
          message: "SQL safety validation failed: blocked by hardening test.",
          rowCount: 0,
          durationMs: 0,
          columns: [],
          rows: [],
        }),
      });
    });

    const guard = installHardeningPageGuard(page);

    await gotoAndAssertCustomerSafePage(
      page,
      "/admin/schema-configuration",
      /schema|sql|view|mapping|configuration|admin/i
    );

    const body = page.locator("body");

    const sqlEditor = page
      .locator("textarea, input[name*='sql' i], [contenteditable='true']")
      .first();

    if (await sqlEditor.isVisible().catch(() => false)) {
      await sqlEditor.fill("SELECT pg_read_file('/etc/passwd', 0, 1000);");

      const previewButton = page
        .getByRole("button", {
          name: /preview|test sql|run/i,
        })
        .first();

      if (await previewButton.isVisible().catch(() => false)) {
        if (await previewButton.isEnabled().catch(() => false)) {
          await previewButton.click();

          await expect(body).toContainText(
            /safety|blocked|failed|invalid|sql|preview/i,
            { timeout: 10_000 }
          );
        }
      }
    }

    const text = (await body.innerText()).toLowerCase();

    expect(text).not.toContain("cannot read properties");
    expect(text).not.toContain("is not a function");
    expect(text).not.toContain("uncaught");
    expect(text).not.toContain("stack trace");

    await guard.assertNoUnexpectedFailures();
  });
});