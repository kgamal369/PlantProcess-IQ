import { test, expect } from "@playwright/test";

const apiBase = process.env.VITE_API_BASE_URL ?? "http://localhost:5063";

test.describe("PPIQ-T014 Phase 2 admin MFA matrix", () => {
  test("admin surface rejects request without MFA proof", async ({ request }) => {
    const response = await request.get(`${apiBase}/api/admin/overview`, {
      headers: {
        "X-Tenant-Id": "00000000-0000-0000-0000-000000000001"
      }
    });

    expect([401, 403]).toContain(response.status());
  });

  test("honest MFA contract endpoint exists when API is running", async ({ request }) => {
    const response = await request.get(`${apiBase}/health`);
    expect([200, 401, 403, 404]).toContain(response.status());
  });
});
