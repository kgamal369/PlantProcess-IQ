import { expect, request, test } from "@playwright/test";

const apiBaseUrl =
  process.env.PPIQ_API_BASE_URL ??
  process.env.VITE_API_BASE_URL ??
  "http://localhost:5063";

const adminUser =
  process.env.PPIQ_SMOKE_USERNAME ??
  process.env.VITE_SMOKE_USERNAME ??
  "admin";

const adminPassword =
  process.env.PPIQ_SMOKE_PASSWORD ??
  process.env.VITE_SMOKE_PASSWORD;

const adminEndpointGroups = [
  { name: "Admin overview", path: "/admin/overview", anonymous: [401, 403], admin: [200] },
  { name: "Jobs monitor", path: "/admin/jobs-monitor", anonymous: [401, 403], admin: [200] },
  { name: "Connector admin", path: "/admin/connectors/provider-types", anonymous: [401, 403], admin: [200] },
  { name: "Schema configuration", path: "/admin/schema-configuration/views", anonymous: [401, 403], admin: [200] },
  { name: "Generic schema mapping catalog", path: "/admin/schema-mapping/catalog", anonymous: [401, 403], admin: [200] },
  { name: "Generic schema mapping readiness", path: "/admin/schema-mapping/readiness", anonymous: [401, 403], admin: [200] },
  { name: "License current", path: "/admin/license/current", anonymous: [401, 403], admin: [200] },
  { name: "Demo lifecycle", path: "/admin/demo-lifecycle/status", anonymous: [401, 403, 404], admin: [200, 404] },
];

test.describe("PPIQ-T101 admin JWT auth matrix", () => {
  test("anonymous users are blocked and admin users can reach every admin endpoint group", async () => {
    test.skip(!adminPassword, "Set PPIQ_SMOKE_PASSWORD before running the auth matrix probe.");

    const anonymous = await request.newContext({ baseURL: apiBaseUrl });

    for (const row of adminEndpointGroups) {
      const response = await anonymous.get(row.path);
      expect(
        row.anonymous,
        `${row.name}: anonymous GET ${row.path} returned ${response.status()}`
      ).toContain(response.status());
    }

    await anonymous.dispose();

    const loginContext = await request.newContext({ baseURL: apiBaseUrl });
    const loginResponse = await loginContext.post("/auth/login", {
      data: {
        userName: adminUser,
        password: adminPassword,
        requestedRole: "Admin",
      },
    });

    expect(loginResponse.ok(), `Admin login returned ${loginResponse.status()}`).toBeTruthy();

    const loginJson = await loginResponse.json();
    const token =
      loginJson.accessToken ??
      loginJson.token ??
      loginJson.jwt ??
      loginJson.bearerToken;

    expect(token, "Login response must contain a JWT access token.").toBeTruthy();

    const admin = await request.newContext({
      baseURL: apiBaseUrl,
      extraHTTPHeaders: {
        Authorization: `Bearer ${token}`,
      },
    });

    for (const row of adminEndpointGroups) {
      const response = await admin.get(row.path);
      expect(
        row.admin,
        `${row.name}: admin GET ${row.path} returned ${response.status()}`
      ).toContain(response.status());
    }

    await admin.dispose();
    await loginContext.dispose();
  });
});