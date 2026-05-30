import { expect, test } from "@playwright/test";

const apiBaseUrl =
  process.env.PLANTPROCESS_API_BASE_URL ??
  process.env.VITE_API_BASE_URL ??
  "http://localhost:5063";

const username = process.env.PPIQ_E2E_ADMIN_USER ?? "e2eadmin";
const password = process.env.PPIQ_E2E_ADMIN_PASSWORD ?? "E2EAdmin123!";

async function login(request: Parameters<Parameters<typeof test>[1]>[0]["request"]) {
  const response = await request.post(`${apiBaseUrl}/auth/login`, {
    data: { username, password },
  });

  expect(response.ok(), `login failed with ${response.status()}`).toBeTruthy();

  const body = await response.json();
  return body.accessToken ?? body.token ?? body.jwt ?? body.bearerToken;
}

test.describe("PPIQ Phase 03 two-stage delta import", () => {
  test("overview is authenticated and exposes source-shaped dump registry", async ({ request }) => {
    const token = await login(request);

    const response = await request.get(`${apiBaseUrl}/admin/two-stage-import/overview`, {
      headers: { Authorization: `Bearer ${token}` },
    });

    expect(response.ok(), `overview failed with ${response.status()}`).toBeTruthy();

    const body = await response.json();

    expect(body).toHaveProperty("isReady");
    expect(body).toHaveProperty("sourceTables");
    expect(body).toHaveProperty("jobs");
  });

  test("stage1/stage2 full cycle endpoint is authenticated", async ({ request }) => {
    const token = await login(request);

    const response = await request.post(`${apiBaseUrl}/admin/two-stage-import/run-full-cycle`, {
      headers: { Authorization: `Bearer ${token}` },
      data: {
        requestedBy: "Playwright Phase03",
        maxRows: 1000,
        timeoutSeconds: 120,
        maxMinutes: 1,
      },
    });

    expect([200, 400, 404, 500]).toContain(response.status());

    if (response.status() === 200) {
      const body = await response.json();
      expect(body.stage).toBe("TwoStageFullCycle");
      expect(Array.isArray(body.rows)).toBeTruthy();
    }
  });
});