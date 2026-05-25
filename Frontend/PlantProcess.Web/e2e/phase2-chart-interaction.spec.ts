import { expect, test } from "@playwright/test";
import {
  expectCustomerSafeShell,
  installPhase2StrictGuard,
} from "./helpers/phase2Guard";

test.describe("PPIQ-HARD-020 — chart widget interaction audit", () => {
  test("dashboard chart area should render and support safe user interaction", async ({
    page,
  }) => {
    const guard = installPhase2StrictGuard(page);

    await page.goto("/dashboard", {
      waitUntil: "networkidle",
      timeout: 30_000,
    });

    await expectCustomerSafeShell(page);

    const body = page.locator("body");
    await expect(body).toContainText(/dashboard|widget|quality|risk/i, {
      timeout: 15_000,
    });

    const chartCandidates = page.locator(
      ".recharts-wrapper, svg, canvas, [data-testid*='chart'], [class*='chart']"
    );

    const chartCount = await chartCandidates.count();

    expect(
      chartCount,
      "Dashboard should expose chart-rendered areas for customer demo value."
    ).toBeGreaterThan(0);

    const firstChart = chartCandidates.first();

    if (await firstChart.isVisible().catch(() => false)) {
      const box = await firstChart.boundingBox();

      if (box) {
        await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
        await page.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
        await page.waitForTimeout(500);
      }
    }

    await expectCustomerSafeShell(page);

    await guard.assertClean();
  });

  test("dashboard should not crash when resizing viewport around chart widgets", async ({
    page,
  }) => {
    const guard = installPhase2StrictGuard(page);

    const viewports = [
      { width: 1440, height: 950 },
      { width: 1024, height: 800 },
      { width: 768, height: 900 },
      { width: 390, height: 844 },
    ];

    for (const viewport of viewports) {
      await page.setViewportSize(viewport);

      await page.goto("/dashboard", {
        waitUntil: "networkidle",
        timeout: 30_000,
      });

      await expectCustomerSafeShell(page);

      await expect(page.locator("body")).toContainText(/dashboard|widget|quality|risk/i, {
        timeout: 15_000,
      });
    }

    await guard.assertClean();
  });
});