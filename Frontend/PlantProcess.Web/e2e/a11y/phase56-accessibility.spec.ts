import { expect, test } from "@playwright/test";

const routes = [
  "/dashboard",
  "/materials",
  "/risk",
  "/data-quality",
  "/correlations",
  "/ml-readiness",
  "/demo-lifecycle",
  "/admin-preview",
  "/admin",
  "/brand",
];

for (const route of routes) {
  test("PPIQ Phase 6 accessibility smoke " + route, async ({ page }) => {
    await page.goto(route);
    await expect(page.locator("main, [data-phase56-page]").first()).toBeVisible({ timeout: 30000 });
    await expect(page.locator("body")).not.toContainText(/could not be loaded|could not load/i);

    const missingButtonNames = await page.locator("button").evaluateAll((buttons) =>
      buttons
        .filter((button) => !button.textContent?.trim() && !button.getAttribute("aria-label"))
        .map((button) => button.outerHTML)
    );

    const missingInputs = await page.locator("input, textarea, select").evaluateAll((controls) =>
      controls
        .filter((control) => {
          const id = control.getAttribute("id");
          const hasLabel = id ? Boolean(document.querySelector("label[for='" + id + "']")) : false;
          return !hasLabel && !control.getAttribute("aria-label") && !control.getAttribute("aria-labelledby");
        })
        .map((control) => control.outerHTML)
    );

    expect(missingButtonNames).toEqual([]);
    expect(missingInputs).toEqual([]);
  });
}
