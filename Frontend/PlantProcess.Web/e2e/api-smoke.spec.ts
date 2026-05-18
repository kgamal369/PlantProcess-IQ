import { expect, test } from "@playwright/test";

test.describe("PlantProcess IQ backend API smoke", () => {
  test("health endpoint responds", async ({ request }) => {
    const response = await request.get("http://localhost:5063/health");

    expect(response.ok()).toBeTruthy();
  });

  test("swagger document is generated in development", async ({ request }) => {
    const response = await request.get("http://localhost:5063/swagger/v1/swagger.json");

    expect(response.ok()).toBeTruthy();

    const body = await response.json();

    expect(body.info?.title).toContain("PlantProcess IQ");
    expect(body.paths).toBeTruthy();
    expect(Object.keys(body.paths).length).toBeGreaterThan(5);
  });

  test("admin jobs monitor endpoint responds", async ({ request }) => {
    const response = await request.get("http://localhost:5063/admin/jobs-monitor");

    expect(response.ok()).toBeTruthy();

    const body = await response.json();

    expect(body).toBeTruthy();
  });
});
