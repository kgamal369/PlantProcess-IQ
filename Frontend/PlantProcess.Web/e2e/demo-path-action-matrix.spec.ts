import { expect, test, type Page } from "@playwright/test";

const apiUrl = process.env.PLAYWRIGHT_API_URL || "http://127.0.0.1:5063";
const userName = process.env.PPIQ_SMOKE_USERNAME || "e2eadmin";
const password = process.env.PPIQ_SMOKE_PASSWORD || "";

// The V1 customer demo journey. Every interactive control on these routes must be
// either live (enabled + named) or honestly disabled (carries data-disabled-reason).
// Edit this list as the demo path evolves; absence of controls on a route is a failure.
const demoPathRoutes: string[] = [
  "/dashboard",
  "/materials",
  "/material-investigation",
  "/correlations",
  "/risk",
  "/data-quality",
  "/suggestions",
  "/analytics-widgets",
  "/page-builder",
  "/executive",
  "/mapping-health",
  "/edge-collector",
  "/historian-connector",
  "/widget-script-compiler",
  "/admin",
  "/demo-lifecycle",
  "/commercial-license",
];

async function login(page: Page) {
  expect(password, "PPIQ_SMOKE_PASSWORD must be configured").not.toBe("");
  const response = await page.request.post(`${apiUrl}/auth/login`, { data: { userName, password } });
  expect(response.ok(), `Login failed: ${response.status()} ${await response.text()}`).toBeTruthy();
}

test.describe("Demo-path action matrix (no-dead-button proof)", () => {
  test.beforeEach(async ({ page }) => { await login(page); });

  for (const route of demoPathRoutes) {
    test(`every interactive control on ${route} is live or honestly disabled`, async ({ page }) => {
      const errors: string[] = [];
      page.on("pageerror", (e) => errors.push(String(e)));

      await page.goto(route, { waitUntil: "domcontentloaded" });
      await page.waitForLoadState("networkidle").catch(() => undefined);

      const buttons = page.getByRole("button");
      const count = await buttons.count();

      let enabled = 0;
      let honestlyDisabled = 0;
      const deadDisabled: string[] = [];
      const unnamedEnabled: string[] = [];

      for (let i = 0; i < count; i += 1) {
        const control = buttons.nth(i);
        if (!(await control.isVisible().catch(() => false))) continue;

        const name = ((await control.getAttribute("aria-label")) || (await control.textContent()) || "").trim();
        const isDisabled = (await control.isDisabled().catch(() => false))
          || (await control.getAttribute("aria-disabled")) === "true";
        const reason = await control.getAttribute("data-disabled-reason");

        if (isDisabled) {
          if (reason && reason.trim().length > 0) honestlyDisabled += 1;
          else deadDisabled.push(name || `button#${i}`);
        } else {
          enabled += 1;
          if (name.length === 0) unnamedEnabled.push(`button#${i}`);
        }
      }

      // Structural invariants - the no-dead-button proof:
      expect(deadDisabled, `${route}: disabled controls with no data-disabled-reason (silent dead controls): ${deadDisabled.join(", ")}`).toEqual([]);
      expect(unnamedEnabled, `${route}: enabled controls with no accessible name (likely dead controls): ${unnamedEnabled.join(", ")}`).toEqual([]);
      expect(count, `${route}: no interactive controls found - route may be broken or unreachable.`).toBeGreaterThan(0);
      expect(errors, `${route}: page raised runtime errors: ${errors.join(" | ")}`).toEqual([]);

      // Per-route line for the matrix report.
      console.log(`[action-matrix] ${route} total=${count} enabled=${enabled} honestlyDisabled=${honestlyDisabled}`);
    });
  }
});