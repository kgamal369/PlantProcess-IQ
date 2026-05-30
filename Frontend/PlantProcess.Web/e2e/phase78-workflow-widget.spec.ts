import { expect, test } from "@playwright/test";

const routes = [
  "/admin?adminTab=connector-truth",
  "/demo-lifecycle",
  "/suggestions",
  "/pages/executive-quality-review",
  "/widget-script-compiler",
];

for (const route of routes) {
  test("Phase 7/8 route smoke " + route, async ({ page }) => {
    await page.goto(route);
    await expect(page.locator("main, [data-phase78-page]").first()).toBeVisible({ timeout: 30000 });
    await expect(page.locator("body")).not.toContainText(/could not be loaded|could not load/i);
  });
}

test("Demo reset confirmation requires RESET", async ({ page }) => {
  await page.goto("/demo-lifecycle");
  await page.getByRole("button", { name: /reset demo/i }).click();
  await expect(page.getByRole("button", { name: /confirm reset/i })).toBeDisabled();
  await page.getByLabel(/confirmation/i).fill("RESET");
  await expect(page.getByRole("button", { name: /confirm reset/i })).toBeEnabled();
});
