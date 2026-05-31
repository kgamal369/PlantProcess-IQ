import { expect, test } from "@playwright/test";
import { apiBaseUrl, login } from "../helpers/auth";

test.describe("PPIQ Phase 03 two-stage delta import", () => {
  test("overview is authenticated and exposes source-shaped dump registry", async ({ request }) => {
    const token = await login(request);

    const response = await request.get(`${apiBaseUrl}/admin/two-stage-import/overview`, {
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: "application/json",
      },
    });

    expect(response.ok(), `overview failed with ${response.status()}`).toBeTruthy();

    const body = await response.json();

    expect(body).toHaveProperty("isReady");
    expect(body).toHaveProperty("sourceTables");
    expect(Array.isArray(body.sourceTables)).toBeTruthy();
    expect(body).toHaveProperty("jobs");
    expect(Array.isArray(body.jobs)).toBeTruthy();
  });

  test("stage1/stage2 full cycle endpoint is authenticated", async ({ request }) => {
    const token = await login(request);

    const response = await request.post(`${apiBaseUrl}/admin/two-stage-import/run-full-cycle`, {
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: "application/json",
        "Content-Type": "application/json",
      },
      data: {
        requestedBy: "Playwright Phase03",
        maxRows: 1000,
        timeoutSeconds: 120,
        maxMinutes: 1,
      },
    });

    expect(response.status(), "full-cycle endpoint must not reject authenticated E2E admin").not.toBe(401);
    expect(response.status(), "full-cycle endpoint must not reject authenticated E2E admin").not.toBe(403);
    expect(response.status(), "full-cycle endpoint must not crash").not.toBe(500);

    expect([200, 400, 404, 409, 422]).toContain(response.status());

    if (response.status() === 200) {
      const body = await response.json();

      expect(body).toHaveProperty("stage");
      expect(String(body.stage)).toMatch(/TwoStageFullCycle|FullCycle|Stage/i);
      expect(Array.isArray(body.rows)).toBeTruthy();
    }
  });
});
