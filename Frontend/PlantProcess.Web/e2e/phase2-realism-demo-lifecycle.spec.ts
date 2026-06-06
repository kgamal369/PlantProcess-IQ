import { expect, test } from "@playwright/test";
import { prepareAuthenticatedPage } from "./helpers/hardening";

test.describe("Phase 2 realism demo lifecycle", () => {
  test("demo lifecycle page tells the complete product story without fake-ML wording", async ({
    page,
    request,
  }) => {
    await prepareAuthenticatedPage(page, request);

    await page.goto("/demo-lifecycle");

    const body = page.locator("body");

    await expect(body).toContainText(/demo|lifecycle|connect|stage|map|monitor|dashboard|report/i);
    await expect(body).toContainText(/ML readiness|No trained production model|readiness/i);

    await expect(body).not.toContainText(/evidence-backed root-cause investigation/i);
    await expect(body).not.toContainText(/autonomous control/i);
    await expect(body).not.toContainText(/writes to PLC/i);
  });
});