import { expect, test } from "@playwright/test";

test.describe("P11 UI interaction regression", () => {
  test("T-069 key UI pages still render after Phase 11 hardening", async ({ page }) => {
    await page.goto("/dashboard", { waitUntil: "domcontentloaded" });
    await expect(page.locator("body")).toBeVisible();

    await page.goto("/phase10/license", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("phase10-license-demo")).toBeVisible();

    await page.goto("/phase9/executive", { waitUntil: "domcontentloaded" });
    await expect(page.getByTestId("phase9-executive-dashboard")).toBeVisible();
  });
});