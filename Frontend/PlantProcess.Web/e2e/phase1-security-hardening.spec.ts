import { expect, request, test } from "@playwright/test";

const apiBaseUrl =
  process.env.PPIQ_API_BASE_URL ??
  process.env.VITE_API_BASE_URL ??
  "http://localhost:5063";

const smokeUserName =
  process.env.PPIQ_SMOKE_USERNAME ??
  process.env.VITE_SMOKE_USERNAME ??
  "ppiq_ci_probe_admin";

const smokePassword =
  process.env.PPIQ_SMOKE_PASSWORD ??
  process.env.VITE_SMOKE_PASSWORD;

test.describe("PPIQ Phase 1 security hardening", () => {
  test("anonymous users cannot reach admin endpoints and authenticated admin can", async () => {
    if (!smokePassword || smokePassword === "YOUR_REAL_ROTATED_PASSWORD") {
      throw new Error(
        "Set PPIQ_SMOKE_PASSWORD to a real rotated password before running this probe."
      );
    }

    const anonymousContext = await request.newContext({
      baseURL: apiBaseUrl,
    });

    const anonymousAdminResponse = await anonymousContext.get("/admin/jobs-monitor");

    expect(
      [401, 403],
      `Anonymous /admin/jobs-monitor must return 401 or 403, got ${anonymousAdminResponse.status()}`
    ).toContain(anonymousAdminResponse.status());

    await anonymousContext.dispose();

    const authContext = await request.newContext({
      baseURL: apiBaseUrl,
    });

    const loginResponse = await authContext.post("/auth/login", {
      data: {
        userName: smokeUserName,
        password: smokePassword,
        requestedRole: "Admin",
      },
    });

    expect(
      loginResponse.ok(),
      `Login failed with ${loginResponse.status()}: ${await loginResponse.text()}`
    ).toBeTruthy();

    const loginBody = await loginResponse.json();

    const accessToken =
      loginBody.accessToken ??
      loginBody.token ??
      loginBody.jwt ??
      loginBody.bearerToken;

    expect(accessToken, "Login response must contain an access token.").toBeTruthy();

    const authenticatedContext = await request.newContext({
      baseURL: apiBaseUrl,
      extraHTTPHeaders: {
        Authorization: `Bearer ${accessToken}`,
      },
    });

    const authenticatedAdminResponse = await authenticatedContext.get("/admin/jobs-monitor");

    expect(
      authenticatedAdminResponse.ok(),
      `Authenticated /admin/jobs-monitor failed with ${authenticatedAdminResponse.status()}: ${await authenticatedAdminResponse.text()}`
    ).toBeTruthy();

    await authenticatedContext.dispose();
    await authContext.dispose();
  });
});
