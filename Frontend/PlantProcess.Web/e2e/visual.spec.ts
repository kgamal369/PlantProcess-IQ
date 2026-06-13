import { expect, test } from "@playwright/test";

// PPIQ-T21: visual-regression baseline across engines x viewports (see playwright.config projects:
// chromium / firefox / chromium-tablet / firefox-tablet). First run records baselines per project;
// later runs diff. Captures the first customer-facing surface; extend with dashboard/investigation
// once a logged-in storage state is wired.
test("login surface visual baseline", async ({ page }) => {
  const res = await page.goto("/login").catch(() => null);
  if (!res || !res.ok()) {
    await page.goto("/").catch(() => {});
  }
  await page.waitForLoadState("networkidle").catch(() => {});
  await expect(page).toHaveScreenshot("login-surface.png", {
    fullPage: true,
    maxDiffPixelRatio: 0.02,
  });
});
