// PPIQ-T10: the demo-critical path - login, dashboard definitions 200, dashboard
// renders with ZERO error toasts and zero contained widget errors. Pairs with the
// T07 isolation spec (which proves errors stay contained when forced); this one
// proves the happy path is actually happy.
import { test, expect } from "@playwright/test";
import { E2E } from "./fixtures/testCredentials";

test.describe("T10 dashboard loads clean", () => {
  test("definitions 200 and dashboard renders without errors", async ({ page }) => {
    let definitionsStatus = 0;
    page.on("response", (res) => {
      if (res.url().includes("/analytics/dashboard/definitions")) {
        definitionsStatus = res.status();
      }
    });

    await page.goto("/login");
    await page.getByLabel(/user/i).fill(E2E.admin.user);
    await page.getByLabel(/pass/i).fill(E2E.admin.pass);
    await page.getByRole("button", { name: /sign in|login/i }).click();
    await page.waitForURL(/dashboard|home|overview/i, { timeout: 15000 });

    await expect(page.getByRole("navigation").first()).toBeVisible({ timeout: 15000 });
    expect(definitionsStatus, "/analytics/dashboard/definitions must be 200").toBe(200);

    // no error affordances anywhere on the freshly loaded dashboard
    await expect(page.getByRole("button", { name: /retry|try again/i })).toHaveCount(0);
    await expect(page.getByText(/could not load|failed to load|something went wrong/i)).toHaveCount(0);
  });
});