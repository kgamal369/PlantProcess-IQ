import { expect, test, type Page } from "@playwright/test";

const apiUrl = process.env.PLAYWRIGHT_API_URL || "http://127.0.0.1:5063";
const userName = process.env.PPIQ_SMOKE_USERNAME || "e2eadmin";
const password = process.env.PPIQ_SMOKE_PASSWORD || "";

async function login(page: Page) {
  const response = await page.request.post(`${apiUrl}/auth/login`, { data: { userName, password } });
  expect(response.ok(), await response.text()).toBeTruthy();
}

async function assertResponsive(page: Page, url: string) {
  const response = await page.goto(url, { waitUntil: "domcontentloaded", timeout: 45_000 });
  expect(response, `No navigation response for ${url}`).not.toBeNull();
  expect(response!.status(), `${url} returned ${response!.status()}`).toBeLessThan(400);
  await expect(page.locator("body")).toBeVisible();
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 8);
  expect(overflow, `${url} has uncontrolled horizontal overflow`).toBeFalsy();
  await expect(page.locator("body")).not.toContainText(/could not load|unhandled exception/i);
}

test("PPIQ-703 app and website reflow over current project protocol", async ({ page }, testInfo) => {
  const isHttps = testInfo.project.name.endsWith("-https");
  const appBase = isHttps ? process.env.PPIQ_APP_HTTPS_URL : process.env.PPIQ_APP_HTTP_URL;
  const websiteBase = isHttps ? process.env.PPIQ_WEB_HTTPS_URL : process.env.PPIQ_WEB_HTTP_URL;
  expect(appBase, `${testInfo.project.name}: app URL is required`).toBeTruthy();
  expect(websiteBase, `${testInfo.project.name}: website URL is required`).toBeTruthy();

  await login(page);
  for (const route of ["/dashboard", "/materials", "/page-builder", "/demo-lifecycle"]) {
    await assertResponsive(page, `${appBase}${route}`);
  }
  for (const route of ["/", "/products", "/products/mes"]) {
    await assertResponsive(page, `${websiteBase}${route}`);
  }
});