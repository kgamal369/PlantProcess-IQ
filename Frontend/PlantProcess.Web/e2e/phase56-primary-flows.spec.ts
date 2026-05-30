import { expect, test } from "@playwright/test";

const flows = [
  { route: "/dashboard", text: /Command Dashboard|Materials/i },
  { route: "/materials", text: /Material Investigation|Quality Events/i },
  { route: "/risk", text: /Risk Intelligence|Medium/i },
  { route: "/data-quality", text: /Data Quality|Severity/i },
  { route: "/correlations", text: /Correlations|threshold/i },
];

for (const flow of flows) {
  test("PPIQ Phase 5 primary flow " + flow.route, async ({ page }) => {
    await page.goto(flow.route);
    await expect(page.locator("body")).not.toContainText(/could not be loaded|could not load/i);
    await expect(page.locator("main, [data-phase56-page]").first()).toBeVisible({ timeout: 30000 });
    await expect(page.locator("body")).toContainText(flow.text);
  });
}

test("PPIQ Phase 5 material search canonical path", async ({ page }) => {
  await page.goto("/materials");
  await page.getByPlaceholder(/search material/i).fill("ADV_COIL4002");
  await page.getByRole("button", { name: /search/i }).click();
  await expect(page.locator("body")).not.toContainText(/could not be loaded|could not load/i);
  await expect(page.locator("body")).toContainText(/Genealogy|Process History|Quality Events|Feature Vector|Risk/i);
});
