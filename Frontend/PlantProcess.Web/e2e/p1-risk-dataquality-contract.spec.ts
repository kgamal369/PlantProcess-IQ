import { expect, test } from "@playwright/test";
import { installNetworkGuard } from "./helpers/networkGuard";

async function login(request: any) {
  const response = await request.post("http://localhost:5063/auth/login", {
    data: {
      userName: "admin",
      password: "ChangeMe123!",
    },
  });

  expect(response.ok()).toBeTruthy();

  const body = await response.json();
  return body.accessToken as string;
}

test.describe("PlantProcess IQ P1 risk and data-quality contracts", () => {
  test("risk and data quality pages call real backend endpoints without contract failure", async ({ page, request }) => {
    const token = await login(request);

    await page.addInitScript((accessToken) => {
      window.localStorage.setItem("plantprocess.auth.accessToken", accessToken);
    }, token);

    const assertNoNetworkFailures = installNetworkGuard(page);

    await page.goto("/risk");
    await expect(page.locator("body")).toContainText(/risk/i);

    await page.goto("/data-quality");
    await expect(page.locator("body")).toContainText(/data quality|quality/i);

    await assertNoNetworkFailures();
  });

  test("backend risk and data-quality APIs return JSON shape", async ({ request }) => {
    const token = await login(request);

    const risk = await request.get("http://localhost:5063/analytics/dashboard/risk", {
      headers: { Authorization: `Bearer ${token}` },
    });

    expect(risk.ok()).toBeTruthy();
    const riskBody = await risk.json();
    expect(riskBody).toBeDefined();

    const dq = await request.get("http://localhost:5063/analytics/dashboard/data-quality", {
      headers: { Authorization: `Bearer ${token}` },
    });

    expect(dq.ok()).toBeTruthy();
    const dqBody = await dq.json();
    expect(dqBody).toBeDefined();
  });
});