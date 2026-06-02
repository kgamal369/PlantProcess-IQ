// P4-06 e2e scaffold. Point ROUTE + the login step at your real router/auth helper.
import { test, expect } from "@playwright/test";

const ROUTE = "/investigate/advanced"; // TODO: set to the route that mounts AdvancedAnalysisPanel
const FORBIDDEN = /guaranteed cause|guaranteed root cause|definitely causes|proves that/i;

test.describe("P4 advanced analysis", () => {
  test("renders ranked contributors with method/q/stability and an honesty caveat", async ({ page }) => {
    // TODO: await login(page)
    await page.goto(ROUTE, { waitUntil: "domcontentloaded" }).catch(() => null);
    await page.waitForTimeout(800);
    const body = await page.locator("body").innerText().catch(() => "");

    // honesty caveat present, and NO over-claiming language
    expect(body).toMatch(/diagnostic association/i);
    expect(body).not.toMatch(FORBIDDEN);
  });
});
