import { expect, test } from "@playwright/test";

test.describe("PlantProcess IQ critical shell regression", () => {
  test("admin page exposes core operational sections", async ({ page }) => {
    await page.goto("/admin");

    await expect(page.locator("body")).toContainText(/admin/i);
    await expect(page.locator("body")).toContainText(/jobs|configuration|schema|import/i);
  });

  test("dashboard page exposes dashboard and widget experience", async ({ page }) => {
    await page.goto("/dashboard");

    await expect(page.locator("body")).toContainText(/dashboard|widget|quality|risk/i);
  });

  test("material investigation route remains available", async ({ page }) => {
    await page.goto("/materials");

    await expect(page.locator("body")).toContainText(/material|investigation|search/i);
  });
});
