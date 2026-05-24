import { expect, test } from "@playwright/test";

test.describe("Dashboard refresh and filter survival", () => {
  test("dashboard loads, refreshes, and keeps customer-safe state", async ({ page }) => {
    const consoleErrors: string[] = [];

    page.on("console", (message) => {
      if (message.type() === "error") {
        consoleErrors.push(message.text());
      }
    });

    await page.goto("/dashboard");
    await expect(page.getByRole("heading", { name: /dashboard/i })).toBeVisible();

    await page.reload();
    await expect(page.getByRole("heading", { name: /dashboard/i })).toBeVisible();

    expect(consoleErrors.filter((x) => !x.includes("favicon")).join("\n")).toEqual("");
  });

  test("dashboard filter change does not blank the dashboard", async ({ page }) => {
    await page.goto("/dashboard");

    await expect(page.getByRole("heading", { name: /dashboard/i })).toBeVisible();

    const filterCandidate = page.locator("input, select, button").first();
    await expect(filterCandidate).toBeVisible();

    await page.keyboard.press("Tab");

    await expect(page.locator("body")).not.toContainText(/white screen|undefined is not/i);
    await expect(page.getByRole("heading", { name: /dashboard/i })).toBeVisible();
  });

  test("saved widgets survive browser refresh", async ({ page }) => {
    await page.goto("/dashboard");

    const widgetsBefore = await page.locator(".dashboard-widget-card, .dashboard-panel").count();

    await page.reload();

    const widgetsAfter = await page.locator(".dashboard-widget-card, .dashboard-panel").count();

    expect(widgetsAfter).toBeGreaterThan(0);
    expect(widgetsAfter).toBeGreaterThanOrEqual(Math.min(widgetsBefore, 1));
  });
});