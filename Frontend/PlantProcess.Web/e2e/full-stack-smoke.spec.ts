import { expect, test } from "@playwright/test";

test.describe("PlantProcess IQ full-stack smoke", () => {
  test("frontend opens and backend health responds", async ({ page, request }) => {
    const response = await request.get("http://localhost:5063/health");
    expect(response.ok()).toBeTruthy();

    await page.goto("/");
    await expect(page).toHaveTitle(/PlantProcess IQ/i);
  });
});
