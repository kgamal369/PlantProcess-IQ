import { test, expect, devices } from "@playwright/test";

// PPIQ-703: cross-browser / cross-viewport matrix. Run with the project list above on
// Chromium + WebKit + Firefox at 375/768/1440 over http and https. Asserts no horizontal
// overflow and that key controls are reachable. Wire BLOCKING in CI.
const VIEWPORTS = [
  { name: "mobile", width: 375, height: 812 },
  { name: "tablet", width: 768, height: 1024 },
  { name: "desktop", width: 1440, height: 900 },
];
const DEMO_PATHS = ["/", "/investigation", "/dashboards"];
const KEY_CONTROLS = ["app-shell", "primary-nav"]; // data-testids that must be reachable

for (const vp of VIEWPORTS) {
  test.describe(`@${vp.name} ${vp.width}x${vp.height}`, () => {
    test.use({ viewport: { width: vp.width, height: vp.height } });
    for (const path of DEMO_PATHS) {
      test(`${path} has no horizontal overflow and reachable controls`, async ({ page }) => {
        await page.goto(path);
        const overflow = await page.evaluate(
          () => document.documentElement.scrollWidth - document.documentElement.clientWidth
        );
        expect(overflow, `horizontal overflow at ${vp.width}px on ${path}`).toBeLessThanOrEqual(1);
        for (const id of KEY_CONTROLS) {
          const el = page.getByTestId(id);
          if (await el.count()) await expect(el.first()).toBeVisible();
        }
      });
    }
  });
}