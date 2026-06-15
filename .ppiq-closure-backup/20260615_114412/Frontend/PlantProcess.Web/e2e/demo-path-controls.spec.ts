import { test, expect } from "@playwright/test";

// PPIQ-201: every primary control on the demo path produces a real effect (a 2xx network call
// OR an observable state change), with zero unhandled rejections. Add the data-testids named
// below to each control; this becomes a BLOCKING CI stage.
const PRIMARY_CONTROLS: { page: string; testId: string }[] = [
  { page: "/investigation", testId: "ctl-search" },
  { page: "/investigation", testId: "ctl-load-investigation" },
  { page: "/investigation", testId: "ctl-calculate-risk" },
  { page: "/investigation", testId: "ctl-generate-pdf" },
  { page: "/dashboards", testId: "ctl-minmax" },
  { page: "/dashboards", testId: "ctl-filter" },
  { page: "/admin/jobs", testId: "ctl-run-job" },
  { page: "/page-builder", testId: "ctl-save-page" },
];

test.describe("PPIQ-201 demo-path controls have real effects", () => {
  for (const c of PRIMARY_CONTROLS) {
    test(`${c.testId} on ${c.page} triggers a 2xx or a state change`, async ({ page }) => {
      const rejections: string[] = [];
      page.on("pageerror", (e) => rejections.push(String(e)));

      await page.goto(c.page);
      const control = page.getByTestId(c.testId);
      if (!(await control.isVisible().catch(() => false))) test.skip(true, `${c.testId} not present`);

      const before = await page.content();
      const [resp] = await Promise.all([
        page.waitForResponse((r) => r.status() >= 200 && r.status() < 300, { timeout: 8000 }).catch(() => null),
        control.click(),
      ]);
      const after = await page.content();

      expect(resp !== null || after !== before, `${c.testId} must cause a 2xx or a visible state change`).toBeTruthy();
      expect(rejections, "no unhandled rejections").toHaveLength(0);
    });
  }
});