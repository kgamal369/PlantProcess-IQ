// e2e/api/new-surfaces-contract.spec.ts
// @new-surfaces - contract checks for the 13-Jul surfaces against a RUNNING API.
// Run:  npx playwright test e2e/api/new-surfaces-contract.spec.ts
// Env:  PLAYWRIGHT_API_URL (default http://localhost:5063),
//       VITE_SMOKE_USERNAME / VITE_SMOKE_PASSWORD (default e2eadmin / E2EAdmin123!)
// NOTE: UI-click specs for the three new pages are added AFTER the M1-11 walk
// records their stable selectors - blind selector guesses make flaky tests.
import { test, expect, APIRequestContext } from "@playwright/test";

const apiUrl = process.env.PLAYWRIGHT_API_URL ?? "http://localhost:5063";
const userName = process.env.VITE_SMOKE_USERNAME ?? "e2eadmin";
const password = process.env.VITE_SMOKE_PASSWORD ?? "E2EAdmin123!";

async function authHeaders(request: APIRequestContext): Promise<Record<string, string>> {
  const login = await request.post(`${apiUrl}/api/auth/login`, {
    // send both casings; unknown JSON members are ignored by the binder
    data: { userName, username: userName, password },
  });
  expect(login.ok(), `login must succeed (${login.status()})`).toBeTruthy();
  const body = await login.json();
  const token = body.token ?? body.accessToken ?? body.jwt;
  expect(token, "login response must expose a token").toBeTruthy();
  return { Authorization: `Bearer ${token}` };
}

test.describe("@new-surfaces API contracts", () => {
  test("supervisor run + reports + monitor row", async ({ request }) => {
    const headers = await authHeaders(request);

    const run = await request.post(`${apiUrl}/api/supervisor/run`, { headers });
    expect(run.status()).toBe(200);
    const report = await run.json();
    expect(report.title).toContain("Supervisor report");
    expect(report.body).toContain("No job configuration was changed automatically");

    const reports = await request.get(`${apiUrl}/api/supervisor/reports`, { headers });
    expect(reports.status()).toBe(200);
    const list = await reports.json();
    expect(Array.isArray(list)).toBeTruthy();
    expect(list.some((r: { itemKey: string }) => r.itemKey === report.itemKey)).toBeTruthy();

    const logs = await request.get(`${apiUrl}/admin/job-logs?jobType=SUPERVISOR`, { headers });
    expect(logs.status()).toBe(200);
    const logJson = await logs.json();
    expect((logJson.entries ?? []).length).toBeGreaterThan(0);
  });

  test("alerts validation + lifecycle + idempotent evaluate", async ({ request }) => {
    const headers = await authHeaders(request);

    const bad = await request.post(`${apiUrl}/api/alerts/rules`, {
      headers, data: { ruleName: "t", parameterCode: "X", comparator: "!=", limitValue: 1 },
    });
    expect(bad.status()).toBe(400);

    const suffix = Math.random().toString(36).slice(2, 8);
    const create = await request.post(`${apiUrl}/api/alerts/rules`, {
      headers,
      data: { ruleName: `e2e-rule-${suffix}`, parameterCode: `E2E_NONE_${suffix}`,
              comparator: ">", limitValue: 999999, severity: "Info" },
    });
    expect(create.status()).toBe(200);
    const rule = await create.json();

    const eval1 = await request.post(`${apiUrl}/api/alerts/evaluate`, { headers });
    expect(eval1.status()).toBe(200);

    const eval2 = await request.post(`${apiUrl}/api/alerts/evaluate`, { headers });
    expect(eval2.status()).toBe(200);
    expect((await eval2.json()).logged).toBe(0);

    const log = await request.get(`${apiUrl}/api/alerts/log`, { headers });
    expect(log.status()).toBe(200);
    expect(Array.isArray(await log.json())).toBeTruthy();

    const del = await request.delete(`${apiUrl}/api/alerts/rules/${rule.id}`, { headers });
    expect(del.status()).toBe(200);
  });

  test("assistant reindex registered + import-batches is an array", async ({ request }) => {
    const headers = await authHeaders(request);

    const reindex = await request.post(`${apiUrl}/api/assistant/reindex`, { headers });
    expect(reindex.status(), "404=AddAssistant regressed; 403=matrix row regressed").toBe(200);

    const batches = await request.get(`${apiUrl}/integration/import-batches`, { headers });
    expect(batches.status()).toBe(200);
    expect(Array.isArray(await batches.json()),
      "author-mapping page renders this as an array").toBeTruthy();
  });
});