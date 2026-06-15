import { test, expect } from "@playwright/test";

// PPIQ-503/505: each role sees only its pages + edit affordances. Uses the TestMode-seeded role
// users (tm-ceo / tm-engineer / tm-admin / tm-operator); skips when TestMode users are not seeded
// so the suite stays green on a bare laptop and runs fully in CI/demo.
const USERS = {
  executive: { user: "tm-ceo", pass: "TestMode-Ceo-123!" },
  engineer:  { user: "tm-engineer", pass: "TestMode-Engineer-123!" },
  admin:     { user: "tm-admin", pass: "TestMode-Admin-123!" },
  operator:  { user: "tm-operator", pass: "TestMode-Operator-123!" },
};

async function login(page, who) {
  await page.goto("/login");
  const u = page.getByTestId("login-username");
  if (!(await u.isVisible().catch(() => false))) test.skip(true, "TestMode login not available");
  await u.fill(who.user);
  await page.getByTestId("login-password").fill(who.pass);
  await page.getByTestId("login-submit").click();
  await expect(page.getByTestId("app-shell")).toBeVisible({ timeout: 15_000 });
}

test.describe("PPIQ-503 role-scoped nav + edit affordances", () => {
  test("executive sees ROI, not investigation editing", async ({ page }) => {
    await login(page, USERS.executive);
    await expect(page.getByTestId("nav-roi-kpi")).toBeVisible();
    await expect(page.getByTestId("nav-engineering-investigation")).toHaveCount(0);
  });

  test("engineer sees investigation, not admin config", async ({ page }) => {
    await login(page, USERS.engineer);
    await expect(page.getByTestId("nav-engineering-investigation")).toBeVisible();
    await expect(page.getByTestId("nav-connector-configuration")).toHaveCount(0);
  });

  test("operator cannot reach configuration (denied route is 403/empty)", async ({ page }) => {
    await login(page, USERS.operator);
    await page.goto("/admin/connectors");
    await expect(page.getByTestId("access-denied")).toBeVisible();
  });

  test("admin can open configuration", async ({ page }) => {
    await login(page, USERS.admin);
    await expect(page.getByTestId("nav-connector-configuration")).toBeVisible();
  });
});