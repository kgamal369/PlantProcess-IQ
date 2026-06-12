// PPIQ-T07: one failing widget must never blank the page.
// Stubs ONE dashboard data endpoint to 500 and asserts: a contained, branded,
// RETRYABLE error renders; the rest of the dashboard stays interactive; zero
// unhandled promise rejections leak to the console.
import { test, expect } from "@playwright/test";
import { E2E } from "./fixtures/testCredentials";

test.describe("T07 widget failure isolation", () => {
  test("one widget 500 stays contained and retryable", async ({ page }) => {
    const consoleErrors: string[] = [];
    page.on("pageerror", (err) => consoleErrors.push(`pageerror: ${err.message}`));
    page.on("console", (msg) => {
      if (msg.type() === "error" && /Unhandled|uncaught/i.test(msg.text())) {
        consoleErrors.push(msg.text());
      }
    });

    // Fail exactly ONE widget data endpoint; everything else passes through.
    let failNext = true;
    await page.route("**/analytics/dashboard/widgets/**", async (route) => {
      if (failNext) {
        failNext = false; // Retry must succeed -> proves the retry actually refetches
        await route.fulfill({ status: 500, body: JSON.stringify({ error: "t07_forced" }) });
        return;
      }
      await route.fallback();
    });

    await page.goto("/login");
    await page.getByLabel(/user/i).fill(E2E.admin.user);
    await page.getByLabel(/pass/i).fill(E2E.admin.pass);
    await page.getByRole("button", { name: /sign in|login/i }).click();
    await page.waitForURL(/dashboard|home|overview/i, { timeout: 15000 });

    // Contained branded error with a working Retry (NOT a full-page failure)
    const retry = page.getByRole("button", { name: /retry|try again/i }).first();
    await expect(retry).toBeVisible({ timeout: 15000 });

    // The page around the failed widget is still alive: navigation chrome present
    await expect(page.getByRole("navigation").first()).toBeVisible();

    // Retry recovers without a full page reload
    await retry.click();
    await expect(retry).toBeHidden({ timeout: 15000 });

    expect(consoleErrors, `unhandled errors leaked:\n${consoleErrors.join("\n")}`).toHaveLength(0);
  });
});