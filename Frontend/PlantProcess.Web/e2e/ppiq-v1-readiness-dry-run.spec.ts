import { expect, test } from "@playwright/test";

const apiUrl = process.env.PLAYWRIGHT_API_URL || "http://127.0.0.1:5063";
const userName = process.env.PPIQ_SMOKE_USERNAME || "e2eadmin";
const password = process.env.PPIQ_SMOKE_PASSWORD || "";

async function login(page: import("@playwright/test").Page) {
  expect(password, "PPIQ_SMOKE_PASSWORD must be configured").not.toBe("");
  const response = await page.request.post(`${apiUrl}/auth/login`, {
    data: { userName, password },
    headers: { Accept: "application/json", "Content-Type": "application/json" },
  });
  expect(response.ok(), `Login failed: ${response.status()} ${await response.text()}`).toBeTruthy();
}

test("PPIQ-103 recorded customer dry-run is fully green", async ({ page }) => {
  const consoleErrors: string[] = [];
  page.on("pageerror", (error) => consoleErrors.push(String(error)));
  page.on("console", (message) => { if (message.type() === "error") consoleErrors.push(message.text()); });

  await login(page);
  await page.goto("/demo-lifecycle", { waitUntil: "domcontentloaded" });
  await expect(page.getByTestId("run-demo-readiness")).toBeVisible();
  await page.getByTestId("run-demo-readiness").click();
  await expect(page.getByTestId("demo-readiness-result")).toBeVisible({ timeout: 30_000 });
  await expect(page.getByTestId("demo-readiness-status")).toHaveText("READY");

  for (const route of ["/dashboard", "/materials", "/page-builder", "/admin", "/ml-readiness"]) {
    await page.goto(route, { waitUntil: "domcontentloaded" });
    await expect(page.locator("body")).not.toContainText(/could not load|unhandled|unexpected error/i);
  }

  expect(consoleErrors, consoleErrors.join("\n")).toEqual([]);
});