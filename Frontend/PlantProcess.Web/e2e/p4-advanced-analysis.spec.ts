// P4-06 e2e — real routes. If the app needs auth, add a login step / storageState in playwright.config.
import { test, expect } from "@playwright/test";
const FORBIDDEN = /guaranteed cause|guaranteed root cause|definitely causes|proves that/i;

test.describe("P4 advanced analysis (§7.4)", () => {
  test("investigation renders with the honesty caveat and no over-claiming language", async ({ page }) => {
    await page.goto("/investigate/advanced?outcomeKey=defect.edge_crack_rate&windowDays=30", { waitUntil: "networkidle" }).catch(() => null);
    await page.waitForTimeout(700);
    const body = await page.locator("body").innerText().catch(() => "");
    expect(body).toMatch(/Suspected contributors/i);
    expect(body).toMatch(/diagnostic association/i);
    expect(body).not.toMatch(FORBIDDEN);
  });

  test("inspection-job page loads the workflow", async ({ page }) => {
    await page.goto("/investigate/inspect", { waitUntil: "networkidle" }).catch(() => null);
    await expect(page.getByText(/New inspection job/i)).toBeVisible({ timeout: 5000 });
  });
});
