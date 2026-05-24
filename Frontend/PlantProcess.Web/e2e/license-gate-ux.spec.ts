import { expect, test } from "@playwright/test";

test.describe("License gate UX", () => {
  test("Light tier shows locked feature overlay instead of blank content", async ({ page }) => {
    await page.goto("/admin-preview");

    await page.getByRole("button", { name: /license/i }).click();
    await page.getByRole("button", { name: /light/i }).click();

    await page.getByRole("button", { name: /users/i }).click();

    await expect(page.getByText(/locked in the current license/i)).toBeVisible();
    await expect(page.getByText(/Enterprise/i)).toBeVisible();
  });

  test("Higher tier unlocks gated content", async ({ page }) => {
    await page.goto("/admin-preview");

    await page.getByRole("button", { name: /license/i }).click();
    await page.getByRole("button", { name: /enterprise/i }).click();

    await page.getByRole("button", { name: /users/i }).click();

    await expect(page.getByText(/Users, Roles/i)).toBeVisible();
    await expect(page.getByText(/locked in the current license/i)).not.toBeVisible();
  });
});