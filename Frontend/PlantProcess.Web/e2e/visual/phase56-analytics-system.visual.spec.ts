import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs";
import { expect, test } from "@playwright/test";


const phase56VisualSpecDir = path.dirname(fileURLToPath(import.meta.url));
const manifest = JSON.parse(
  fs.readFileSync(
    path.join(phase56VisualSpecDir, "phase56-baseline-manifest.json"),
    "utf8",
  ),
);

const viewports: Record<string, { width: number; height: number }> = {
  "1920x1080": { width: 1920, height: 1080 },
  "1440x900": { width: 1440, height: 900 },
  "768x1024": { width: 768, height: 1024 },
};

for (const route of manifest.routes) {
  for (const theme of manifest.themes) {
    for (const viewportName of manifest.viewports) {
      test("PPIQ visual " + route + " " + theme + " " + viewportName, async ({ page }) => {
        await page.setViewportSize(viewports[viewportName]);
        await page.emulateMedia({ colorScheme: theme as "dark" | "light" });
        await page.goto(route);
        await expect(page.locator("body")).not.toContainText(/could not be loaded|could not load/i);
        await expect(page.locator("main, [data-phase56-page]").first()).toBeVisible({ timeout: 30000 });

        await expect(page).toHaveScreenshot(
          "phase56-" +
            route.replace(/[^a-z0-9]+/gi, "-").replace(/^-|-$/g, "") +
            "-" +
            theme +
            "-" +
            viewportName +
            ".png",
          { maxDiffPixelRatio: 0.005, fullPage: true }
        );
      });
    }
  }
}
