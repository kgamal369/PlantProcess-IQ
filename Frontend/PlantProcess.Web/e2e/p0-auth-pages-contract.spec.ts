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
  expect(body.accessToken).toBeTruthy();
  expect(body.role).toBe("Admin");

  return body.accessToken as string;
}

test.describe("PlantProcess IQ P0 authenticated page contract", () => {
  test("core pages load without auth, route, DTO, or console failures", async ({ page, request }) => {
    const token = await login(request);

    await page.addInitScript((accessToken) => {
      window.localStorage.setItem("plantprocess.auth.accessToken", accessToken);
    }, token);

    const assertNoNetworkFailures = installNetworkGuard(page);

    for (const route of [
      "/dashboard",
      "/risk",
      "/data-quality",
      "/correlations",
      "/materials",
      "/admin",
    ]) {
      await page.goto(route);
      await expect(page.locator("body")).toContainText(/PlantProcess|Dashboard|Risk|Data|Admin|Material|Correlation/i);
    }

    await assertNoNetworkFailures();
  });
});