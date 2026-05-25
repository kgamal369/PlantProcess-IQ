import { expect, test } from "@playwright/test";
import {
  installHardeningPageGuard,
  prepareAuthenticatedPage,
} from "./helpers/phase1Hardening";

test.describe("PPIQ-HARD-002 — toast notification mapping", () => {
  test("customer-safe toast root should exist and app should not stack fatal UI errors", async ({
    page,
    request,
  }) => {
    await prepareAuthenticatedPage(page, request);

    const guard = installHardeningPageGuard(page);

    await page.goto("/dashboard", { waitUntil: "networkidle" });

    await expect(page.locator("body")).toBeVisible();

    const toastRootCandidate = page.locator("[data-sonner-toaster]");
    await expect(toastRootCandidate).toHaveCount(1, { timeout: 10_000 });

    await guard.assertNoUnexpectedFailures();
  });
});