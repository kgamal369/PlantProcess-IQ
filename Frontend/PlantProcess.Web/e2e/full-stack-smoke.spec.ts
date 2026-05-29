import { expect, test } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";
import { prepareAuthenticatedPage } from "./helpers/hardening";

test.describe("PlantProcess IQ full-stack smoke", () => {
  test("frontend opens and backend health responds", async ({ page, request }) => {
    const token = await login(request);

    const response = await request.get(`${apiBaseUrl}/health`, {
      headers: {
        Authorization: `Bearer ${token}`,
      },
    });

    expect(response.ok()).toBeTruthy();

    await prepareAuthenticatedPage(page, request);

    await page.goto("/", {
      waitUntil: "domcontentloaded",
      timeout: 30_000,
    });

    await expect(page).toHaveTitle(/PlantProcess IQ/i);
    await expect(page.locator("body")).toContainText(/PlantProcess IQ|dashboard|quality|manufacturing/i);
  });
});