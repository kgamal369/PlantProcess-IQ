// e2e/ui-new-surfaces.spec.ts - UI-click evidence for the three 13-Jul pages.
// Auth: real /api/auth/login response bootstrapped into localStorage
// (plantprocess.auth.user) - no login-form selectors needed.
// Run (API + web up):  npx playwright test e2e/ui-new-surfaces.spec.ts
import { test, expect } from "@playwright/test";

const apiUrl = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5063";
const userName = process.env.VITE_SMOKE_USERNAME ?? "e2eadmin";
const password = process.env.VITE_SMOKE_PASSWORD ?? "E2EAdmin123!";

test.beforeEach(async ({ page, request }) => {
  const login = await request.post(`${apiUrl}/api/auth/login`, {
    data: { userName, username: userName, password },
  });
  expect(login.ok(), `login must succeed (${login.status()})`).toBeTruthy();
  const body = await login.json();
  await page.addInitScript((auth) => {
    window.localStorage.setItem("plantprocess.auth.user", JSON.stringify(auth));
  }, body);
});

test("Engine Supervisor page renders and runs a review", async ({ page }) => {
  await page.goto("/data-integration/supervisor");
  await expect(page.getByText("Engine Supervisor").first()).toBeVisible();
  const run = page.getByRole("button", { name: /Run review now/i });
  await expect(run).toBeVisible();
  await run.click();
  await expect(page.getByText(/Supervisor report /).first()).toBeVisible({ timeout: 20000 });
});

test("Plant Data Log page renders form, validates, and evaluates", async ({ page }) => {
  await page.goto("/data-integration/alerting");
  await expect(page.getByText("Plant Data Log").first()).toBeVisible();

  await page.getByRole("button", { name: /Add rule/i }).click();
  await expect(page.getByText(/Rule name and parameter code are required/i)).toBeVisible();

  await page.getByRole("button", { name: /Run evaluation/i }).click();
  await expect(page.getByText(/Evaluation complete:/i)).toBeVisible({ timeout: 20000 });
});

test("Load to Plant Data page renders (empty state or batch picker) + journey rail", async ({ page }) => {
  await page.goto("/data-integration/author-mapping");
  await expect(page.getByText("Load to Plant Data").first()).toBeVisible();
  await expect(
    page.getByText(/No import batches yet/i).or(page.getByText(/Import batch/i)).first()
  ).toBeVisible({ timeout: 15000 });
  // journey rail nodes present on the page shell
  await expect(page.getByText("Connect").first()).toBeVisible();
  await expect(page.getByText("Assistant").first()).toBeVisible();
});