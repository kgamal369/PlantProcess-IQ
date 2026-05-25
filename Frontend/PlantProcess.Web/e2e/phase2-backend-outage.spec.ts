import { expect, test } from "@playwright/test";
import { expectCustomerSafeShell } from "./helpers/phase2Guard";

const apiUrlPatterns = [
  /localhost:5063/i,
  /api\.localhost/i,
  /\/api\//i,
  /\/health/i,
  /\/db-health/i,
  /\/auth\//i,
  /\/analytics\//i,
  /\/admin\//i,
  /\/demo\//i,
  /\/reports\//i,
];

test.describe("PPIQ-HARD-009 — controlled backend outage states", () => {
  test("app should show controlled backend failure instead of white screen", async ({
    page,
  }) => {
    await page.route("**/*", async (route) => {
      const url = route.request().url();

      if (apiUrlPatterns.some((pattern) => pattern.test(url))) {
        await route.abort("failed");
        return;
      }

      await route.continue();
    });

    await page.goto("/dashboard", {
      waitUntil: "domcontentloaded",
      timeout: 30_000,
    });

    await page.waitForTimeout(1500);

    await expectCustomerSafeShell(page);

    await expect(page.locator("body")).toContainText(
      /backend|connection|failed|could not|retry|offline|unavailable|load/i,
      { timeout: 15_000 }
    );
  });

  test("dashboard route should remain customer-safe when widget APIs fail", async ({
    page,
  }) => {
    await page.route("**/analytics/**", async (route) => {
      await route.fulfill({
        status: 503,
        contentType: "application/json",
        body: JSON.stringify({
          title: "Simulated backend outage",
          detail: "Phase 2 outage test.",
        }),
      });
    });

    await page.goto("/dashboard", {
      waitUntil: "networkidle",
      timeout: 30_000,
    });

    await expectCustomerSafeShell(page);

    await expect(page.locator("body")).toContainText(
      /dashboard|widget|could not|failed|retry|unavailable|quality|risk/i,
      { timeout: 15_000 }
    );
  });
});