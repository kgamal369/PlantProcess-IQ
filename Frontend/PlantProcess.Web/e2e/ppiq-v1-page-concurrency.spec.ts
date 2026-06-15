import { expect, test } from "@playwright/test";

const apiUrl = process.env.PLAYWRIGHT_API_URL || "http://127.0.0.1:5063";
const userName = process.env.PPIQ_SMOKE_USERNAME || "e2eadmin";
const password = process.env.PPIQ_SMOKE_PASSWORD || "";

async function login(page: import("@playwright/test").Page) {
  const response = await page.request.post(`${apiUrl}/auth/login`, { data: { userName, password } });
  expect(response.ok(), await response.text()).toBeTruthy();
}

test("PPIQ-601 two sessions reject silent last-write-wins", async ({ browser }) => {
  const a = await browser.newContext();
  const b = await browser.newContext();
  const pageA = await a.newPage();
  const pageB = await b.newPage();
  await login(pageA);
  await login(pageB);

  const slug = `concurrency-${Date.now()}`;
  await pageA.goto("/page-builder");
  await pageA.getByLabel("Slug").fill(slug);
  await pageA.getByLabel("Title").fill("Concurrency baseline");
  await pageA.getByTestId("ctl-save-page").click();
  await expect(pageA.getByRole("status")).toContainText(/saved/i);

  await pageB.goto("/page-builder");
  await pageB.getByLabel("Slug").fill(slug);
  await pageB.getByRole("button", { name: /load by slug/i }).click();
  await expect(pageB.getByRole("status")).toContainText(/loaded/i);
  await pageA.getByRole("button", { name: /load by slug/i }).click();
  await expect(pageA.getByRole("status")).toContainText(/loaded/i);

  const titleA = pageA.getByLabel("Title");
  const titleB = pageB.getByLabel("Title");
  await titleA.fill(`Concurrency A ${Date.now()}`);
  await pageA.getByTestId("ctl-save-page").click();
  await expect(pageA.getByRole("status")).toContainText(/saved/i);

  await titleB.fill(`Concurrency B ${Date.now()}`);
  await pageB.getByTestId("ctl-save-page").click();
  await expect(pageB.getByTestId("conflict-dialog")).toBeVisible();
  await expect(pageB.getByTestId("conflict-editor")).toContainText(/current version|changed by/i);
  await expect(pageB.getByTestId("conflict-overwrite")).toBeDisabled();
  await pageB.getByTestId("conflict-overwrite-confirm").check();
  await expect(pageB.getByTestId("conflict-overwrite")).toBeEnabled();

  await a.close();
  await b.close();
});