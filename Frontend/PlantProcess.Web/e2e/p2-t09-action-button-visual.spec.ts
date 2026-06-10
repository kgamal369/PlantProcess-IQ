import { expect, test } from "@playwright/test";

const representativeRoutes = [
  { name: "Material Investigation", path: "/material-investigation" },
  { name: "Dashboard", path: "/dashboard" },
  { name: "Admin", path: "/admin" },
  { name: "Risk Dashboard", path: "/risk-dashboard" },
  { name: "Value Executive", path: "/value/executive" },
  { name: "Widget Schema Drift", path: "/dashboard/widgets/schema-drift" },
  { name: "Commercial License", path: "/commercial/license" },
];

test.describe("P2-T09 action button visual baseline", () => {
  test.skip(
    !process.env.PPIQ_RUN_VISUAL_BASELINE,
    "Set PPIQ_RUN_VISUAL_BASELINE=1 to refresh P2-T09 visual snapshots.",
  );

  for (const route of representativeRoutes) {
    test(route.name + " action button hierarchy", async ({ page }) => {
      await page.goto(route.path);
      await page.waitForLoadState("networkidle");

      const buttons = page.locator(".ppiq-std-button");
      await expect(buttons.first()).toBeVisible();

      await expect(page).toHaveScreenshot(
        "p2-t09-" + route.name.toLowerCase().replace(/[^a-z0-9]+/g, "-") + ".png",
        { fullPage: true },
      );
    });
  }
});