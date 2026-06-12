// PPIQ-T14: tier-toggle proof. Run TWICE against two stack profiles (T22 ForceTier):
//   round A: PPIQ_TESTMODE__ForceTier=Enterprise  -> gated API answers 200
//   round B: PPIQ_TESTMODE__ForceTier=Light       -> gated API answers a license block
// Driven by env so the same spec serves both rounds:
//   PPIQ_E2E_TIER_TOGGLE=1 PPIQ_E2E_EXPECT_TIER=enterprise|light npm run e2e -- t14
import { test, expect } from "@playwright/test";
import { E2E } from "./fixtures/testCredentials";

const enabled = process.env.PPIQ_E2E_TIER_TOGGLE === "1";
const expectTier = (process.env.PPIQ_E2E_EXPECT_TIER ?? "enterprise").toLowerCase();

test.describe("T14 tier toggle", () => {
  test.skip(!enabled, "set PPIQ_E2E_TIER_TOGGLE=1 and restart the stack with the target ForceTier");

  test(`gated surface behaves per tier (${expectTier})`, async ({ page, request }) => {
    await page.goto("/login");
    await page.getByLabel(/user/i).fill(E2E.admin.user);
    await page.getByLabel(/pass/i).fill(E2E.admin.pass);
    await page.getByRole("button", { name: /sign in|login/i }).click();
    await page.waitForURL(/dashboard|home|overview/i, { timeout: 15000 });

    const token = await page.evaluate(() =>
      (window as unknown as { __ppiqAccessToken?: string }).__ppiqAccessToken ?? "");

    const res = await request.get(`${E2E.baseUrl}/admin/connectors/providers`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    });

    if (expectTier === "enterprise") {
      expect(res.status(), "Enterprise must pass the DbLinkConfiguration paywall").toBe(200);
    } else {
      expect([402, 403], "Light must be blocked by the paywall").toContain(res.status());
      // and the FE shows the locked affordance instead of a raw failure
      await page.goto("/admin");
      await expect(
        page.getByText(/upgrade|locked|not included in your plan/i).first()
      ).toBeVisible({ timeout: 15000 });
    }
  });
});