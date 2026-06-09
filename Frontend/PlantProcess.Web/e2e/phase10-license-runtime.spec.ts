import { expect, test } from "@playwright/test";

test.describe("P10 license tier-toggle and expiry regression", () => {
  test("T-058/T-062 live tier toggle hides and restores Enterprise-only features", async ({ page }) => {
    await page.goto("/phase10/license", { waitUntil: "domcontentloaded" });

    await expect(page.getByTestId("phase10-license-demo")).toBeVisible();
    await expect(page.getByText(/Phase 10 License Runtime Demo/i)).toBeVisible();

    await page.getByRole("button", { name: "Pro" }).click();
    await expect(page.getByText(/Enterprise-only connectors/i)).toBeVisible();
    await expect(page.getByText(/Upgrade from Pro to Enterprise/i)).toBeVisible();

    await page.getByRole("button", { name: "Enterprise" }).click();
    await expect(page.getByText(/Included in current tier/i)).toBeVisible();
  });
});