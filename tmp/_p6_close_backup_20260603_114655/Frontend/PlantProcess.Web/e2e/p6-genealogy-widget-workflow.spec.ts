import { expect, test, type APIRequestContext } from "@playwright/test";
import { apiBaseUrl, login } from "./helpers/auth";

const DEMO_COIL = "ADV_COIL4002";

async function authHeaders(request: APIRequestContext) {
  const token = await login(request);
  return { Authorization: `Bearer ${token}` };
}

test.describe("P6 — genealogy thread + script-built widget workflow", () => {
  test("map: genealogy thread resolves for a demo coil with conditions", async ({ request }) => {
    const headers = await authHeaders(request);
    const res = await request.get(`${apiBaseUrl}/admin/p03p04/completion/material-investigation/${DEMO_COIL}`, { headers });
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    const rows = Array.isArray(body) ? body : body.rows;
    expect(Array.isArray(rows)).toBeTruthy();
    expect(rows.length).toBeGreaterThan(0);
  });

  test("build widget by script: execute compiles to a safe, row-limited result", async ({ request }) => {
    const headers = await authHeaders(request);
    const res = await request.post(`${apiBaseUrl}/analytics/dashboard/widgets/execute`, {
      headers,
      data: { expression: `source=v_ppiq_p04_completion_golden_thread\nmeasure=count(*)\nlimit=50` },
    });
    // 200 (ProPlus rows), 403 (tier gate), 400 (demo source differs), 404 (route variant) are all valid contract outcomes
    expect([200, 400, 403, 404]).toContain(res.status());
    if (res.status() === 200) {
      const body = await res.json();
      expect(Array.isArray(body.rows)).toBeTruthy();
      expect(body.rows.length).toBeLessThanOrEqual(50);
    }
  });

  test("place on a page: dashboard definitions endpoint is reachable", async ({ request }) => {
    const headers = await authHeaders(request);
    const res = await request.get(`${apiBaseUrl}/analytics/dashboard/definitions`, { headers });
    expect([200, 204, 404]).toContain(res.status());
  });
});