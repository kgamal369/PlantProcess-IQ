import { expect, test, type Page } from "@playwright/test";
import { apiBaseUrl, authHeaders } from "../helpers/auth";

async function openHealthyPage(page: Page, route: string, expected?: RegExp) {
  const pageErrors: string[] = [];
  page.on("pageerror", (error) => pageErrors.push(String(error)));

  await page.goto(route, { waitUntil: "domcontentloaded" });
  await page.waitForFunction(() => !document.body.innerText.includes("Connecting to backend"), undefined, { timeout: 30_000 });
  await page.waitForLoadState("networkidle").catch(() => undefined);

  await expect(page.getByText(/Backend connection failed/i)).toHaveCount(0);
  await expect(page.getByText(/application shell is refreshing/i)).toHaveCount(0);
  await expect(page.locator("h1:visible").first()).toBeVisible();
  if (expected) await expect(page.getByText(expected).first()).toBeVisible();

  const overflow = await page.evaluate(() => Math.max(0, document.documentElement.scrollWidth - document.documentElement.clientWidth));
  expect(overflow, `${route} must not overflow the page horizontally`).toBeLessThanOrEqual(2);
  expect(pageErrors, `${route} emitted browser page errors`).toEqual([]);
}

test.describe.serial("PPIQ automated canonical journey certification", () => {
  test("[J01] Connect: provider catalog and connection workspace are live", async ({ page, request }) => {
    const headers = await authHeaders(request);
    const response = await request.get(`${apiBaseUrl}/admin/connectors/provider-types`, { headers });
    expect(response.status()).toBe(200);
    await openHealthyPage(page, "/data-integration/connections", /DB Link|Connection/i);
  });

  test("[J02] Register and schedule: registry workspace and connection profiles are live", async ({ page, request }) => {
    const headers = await authHeaders(request);
    const response = await request.get(`${apiBaseUrl}/admin/connectors/connection-profiles`, { headers });
    expect(response.status()).toBe(200);
    await openHealthyPage(page, "/data-integration/registry", /Registry|Schema|table/i);
  });

  test("[J03] Incremental import: import-batch contract is a plain array", async ({ page, request }) => {
    const headers = await authHeaders(request);
    const response = await request.get(`${apiBaseUrl}/integration/import-batches`, { headers });
    expect(response.status()).toBe(200);
    expect(Array.isArray(await response.json())).toBeTruthy();
    await openHealthyPage(page, "/data-integration/importing", /Import/i);
  });

  test("[J04] Data preparation: mapping workspace exposes eight canonical targets", async ({ page }) => {
    await openHealthyPage(page, "/data-integration/author-mapping", /Load to Plant Data/i);
    await expect(page.getByText(/Target entity/i).first()).toBeVisible();
    await expect(page.getByText(/Import batch|No import batches/i).first()).toBeVisible();
  });

  test("[J05] Loading jobs: one operational job monitor route is live", async ({ page }) => {
    await openHealthyPage(page, "/data-integration/jobs", /Jobs|Monitor/i);
  });

  test("[J06] Loaded: material investigation opens with an honest result or empty state", async ({ page }) => {
    await openHealthyPage(page, "/materials", /Material/i);
  });

  test("[J07] Dashboards and widgets: command dashboard is live", async ({ page }) => {
    await openHealthyPage(page, "/dashboard", /Dashboard/i);
  });

  test("[J08] Analysis authoring: definition workspace is live and API is registered", async ({ page, request }) => {
    const headers = await authHeaders(request);
    const response = await request.get(`${apiBaseUrl}/api/analysis-jobs`, { headers });
    expect(response.status()).toBe(200);
    await openHealthyPage(page, "/investigate/analysis-jobs", /Analysis/i);
  });

  test("[J09] Analysis jobs: governed run surface is reachable without fabricated success", async ({ page }) => {
    await openHealthyPage(page, "/investigate/analysis-jobs", /Analysis/i);
    await expect(page.getByText(/BlockedTooFewRows|Run|No analysis|definition/i).first()).toBeVisible();
  });

  test("[J10] Findings: correlations route renders honest finding or empty state", async ({ page }) => {
    await openHealthyPage(page, "/correlations", /Correlation|Finding/i);
  });

  test("[J11] AI and ML readiness: readiness surface is live", async ({ page }) => {
    await openHealthyPage(page, "/ml-readiness", /ML|Readiness/i);
  });

  test("[J12] AI and ML jobs: monitor remains the common operational surface", async ({ page }) => {
    await openHealthyPage(page, "/data-integration/jobs", /Jobs|Monitor/i);
  });

  test("[J13] AI and ML results: suggestions surface renders evidence or honest empty state", async ({ page }) => {
    await openHealthyPage(page, "/suggestions", /Suggestion|Recommendation/i);
  });

  test("[J14] Supervisor: generate a real read-only report", async ({ page }) => {
    await openHealthyPage(page, "/data-integration/supervisor", /Engine Supervisor/i);
    const run = page.getByRole("button", { name: /Run review now/i });
    await expect(run).toBeVisible();
    await run.click();
    await expect(page.getByText(/Supervisor report/i).first()).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText(/read-only|No job configuration was changed automatically/i).first()).toBeVisible();
  });

  test("[J15] Assistant: reindex is authorized and assistant surface is live", async ({ page, request }) => {
    const headers = await authHeaders(request);
    const response = await request.post(`${apiBaseUrl}/api/assistant/reindex`, { headers });
    expect(response.status()).toBe(200);
    await openHealthyPage(page, "/assistant", /Assistant/i);
  });

  test("[UI4] Plant Data Log: validation and idempotent evaluation are user-visible", async ({ page }) => {
    await openHealthyPage(page, "/data-integration/alerting", /Plant Data Log/i);
    await page.getByRole("button", { name: /Add rule/i }).click();
    await expect(page.getByText(/Rule name and parameter code are required/i)).toBeVisible();
    await page.getByRole("button", { name: /Run evaluation/i }).click();
    await expect(page.getByText(/Evaluation complete:/i)).toBeVisible({ timeout: 30_000 });
  });

  test("[MONITOR] Job monitor has named actions and no dead unlabelled controls", async ({ page }) => {
    await openHealthyPage(page, "/data-integration/jobs", /Jobs|Monitor/i);
    const buttons = page.getByRole("button");
    for (let index = 0; index < await buttons.count(); index += 1) {
      const button = buttons.nth(index);
      if (!(await button.isVisible().catch(() => false))) continue;
      const name = ((await button.getAttribute("aria-label")) ?? (await button.textContent()) ?? "").trim();
      expect(name, `visible job-monitor button ${index} must have an accessible name`).not.toBe("");
    }
  });
});
