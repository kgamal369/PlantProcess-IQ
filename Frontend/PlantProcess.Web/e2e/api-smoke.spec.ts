import { expect, test } from "@playwright/test";
import { authenticatedGet, login, apiBaseUrl } from "./helpers/auth";

test.describe("PlantProcess IQ backend API smoke", () => {
  test("login returns access token", async ({ request }) => {
    const token = await login(request);
    expect(token.length).toBeGreaterThan(20);
  });

  test("health endpoint responds with authenticated user", async ({ request }) => {
    const response = await authenticatedGet(request, "/health");

    expect(
      response.ok(),
      `/health should return 200 for authenticated smoke user but returned HTTP ${response.status()}`
    ).toBeTruthy();

    const body = await response.json();
    expect(body).toBeDefined();
  });

  test("db-health endpoint responds with authenticated user", async ({ request }) => {
    const response = await authenticatedGet(request, "/db-health");

    expect(
      [200, 503].includes(response.status()),
      `/db-health should return 200 or controlled 503 for authenticated smoke user but returned HTTP ${response.status()}`
    ).toBeTruthy();

    const body = await response.json();
    expect(body).toBeDefined();
  });

  test("readiness endpoint responds with authenticated user", async ({ request }) => {
    const response = await authenticatedGet(request, "/health/ready");

    expect(
      [200, 503, 404].includes(response.status()),
      `/health/ready should return controlled status but returned HTTP ${response.status()}`
    ).toBeTruthy();
  });

  test("admin jobs monitor endpoint responds", async ({ request }) => {
    const response = await authenticatedGet(request, "/admin/jobs-monitor");

    expect(
      response.ok(),
      `/admin/jobs-monitor should return 200 but returned HTTP ${response.status()}`
    ).toBeTruthy();

    const body = await response.json();
    expect(body).toBeDefined();
  });

  test("dashboard metadata endpoint responds", async ({ request }) => {
    const response = await authenticatedGet(
      request,
      "/analytics/dashboard/metadata"
    );

    expect(
      response.ok(),
      `/analytics/dashboard/metadata should return 200 but returned HTTP ${response.status()}`
    ).toBeTruthy();

    const body = await response.json();
    expect(body).toBeDefined();
  });

  test("connector provider-types endpoint responds", async ({ request }) => {
    const response = await authenticatedGet(
      request,
      "/admin/connectors/provider-types"
    );

    expect(
      response.ok(),
      `/admin/connectors/provider-types should return 200 but returned HTTP ${response.status()}`
    ).toBeTruthy();

    const body = await response.json();
    expect(body).toBeDefined();
  });

  test("data quality dashboard endpoint responds", async ({ request }) => {
    const response = await authenticatedGet(
      request,
      "/analytics/dashboard/data-quality"
    );

    expect(
      response.ok(),
      `/analytics/dashboard/data-quality should return 200 but returned HTTP ${response.status()}`
    ).toBeTruthy();

    const body = await response.json();
    expect(body).toBeDefined();
  });
});