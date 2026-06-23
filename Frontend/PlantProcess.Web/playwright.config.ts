// ============================================================
// FILE: Frontend/PlantProcess.Web/playwright.config.ts
//
// Purpose:
//   Deterministic Playwright E2E configuration for PlantProcess IQ.
//
// Critical auth fix:
//   Do NOT use default bootstrap credentials for E2E login.
//   That pair is treated as bootstrap admin and is rejected with 403
//   once a real configured admin exists.
//
//   E2E now uses:
//     legacy hardcoded E2E credentials
//
// This user is configured as a real admin, not bootstrap admin.
// ============================================================

import { defineConfig, devices } from "@playwright/test";

function firstAbsoluteHttpUrl(candidates: Array<string | undefined>, fallback: string): string {
  for (const candidate of candidates) {
    if (!candidate) continue;
    try {
      const parsed = new URL(candidate);
      if (parsed.protocol === "http:" || parsed.protocol === "https:") return candidate;
    } catch {
      // Ignore relative or malformed values like "/api" so they never break webServer/baseURL.
    }
  }
  return fallback;
}

const externalBaseUrl = firstAbsoluteHttpUrl([process.env.PLAYWRIGHT_BASE_URL], "");

const frontendBaseUrl = firstAbsoluteHttpUrl(
  [process.env.PLAYWRIGHT_BASE_URL, process.env.VITE_APP_BASE_URL],
  "http://localhost:5173"
);

const apiBaseUrl = firstAbsoluteHttpUrl(
  [process.env.PLAYWRIGHT_API_URL, process.env.VITE_API_BASE_URL],
  "http://localhost:5063"
);

const backendProject =
  process.env.PLAYWRIGHT_BACKEND_PROJECT ||
  "../../Backend/PlantProcess.Api/PlantProcess.Api.csproj";

const frontendHost = process.env.PLAYWRIGHT_FRONTEND_HOST || "127.0.0.1";
const frontendPort = Number(process.env.PLAYWRIGHT_FRONTEND_PORT || "5173");

const e2eUserName = "e2eadmin";
const e2ePassword = process.env.PPIQ_SMOKE_PASSWORD || "E2eAdmin_Local123!";

// Make the values visible to Playwright test code itself.
// webServer.env is only for spawned backend/frontend processes.
process.env.PPIQ_SMOKE_USERNAME ??= e2eUserName;
process.env.PPIQ_SMOKE_PASSWORD ??= e2ePassword;
process.env.VITE_SMOKE_USERNAME ??= e2eUserName;
process.env.VITE_SMOKE_PASSWORD ??= e2ePassword;
process.env.VITE_API_BASE_URL ??= apiBaseUrl;
process.env.PLAYWRIGHT_API_URL ??= apiBaseUrl;

export default defineConfig({
  testDir: "./e2e",

  timeout: 90_000,

  expect: {
    timeout: 20_000,
  },

  fullyParallel: false,

  // Keep local E2E deterministic while hardening.
  workers: process.env.CI ? 2 : 1,

  retries: process.env.CI ? 1 : 0,

  reporter: [
    ["line"],
    ["html", { outputFolder: "playwright-report", open: "never" }],
  ],

  use: {
    baseURL: frontendBaseUrl,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure",
    actionTimeout: 20_000,
    navigationTimeout: 30_000,
  },

  webServer: externalBaseUrl ? undefined : [
    {
      command: `dotnet run --project ${backendProject} --urls ${apiBaseUrl}`,
      url: `${apiBaseUrl}/health`,
      timeout: 120_000,
      reuseExistingServer: true,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: apiBaseUrl,

        ConnectionStrings__PlantProcessDb:
          process.env.ConnectionStrings__PlantProcessDb || process.env.PPIQ_TEST_CONNECTION_STRING || "Host=localhost;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only",

        PLANTPROCESS_ALLOWED_ORIGINS:
          "http://localhost:5173,http://localhost:3000",

        PlantProcess__AllowedOrigins__0: "http://localhost:5173",
        PlantProcess__AllowedOrigins__1: "http://localhost:3000",

        PlantProcess__RequireDatabaseConnectionString: "true",
        PlantProcess__PlantTimeZoneId: "Europe/Berlin",
        PlantProcess__PlantUtcOffsetMinutes: "60",

        PlantProcess__Auth__Issuer: "PlantProcessIQ",
        PlantProcess__Auth__Audience: "PlantProcessIQ.Client",
        PlantProcess__Auth__SigningKey:
          process.env.PlantProcess__Auth__SigningKey || "ppiq-local-signing-key-not-for-production-0a1b2c3d4e5f60718293a4b5c6",
        PlantProcess__Auth__AccessTokenMinutes: "120",

        // Keep bootstrap different from the E2E real admin user.
        // This avoids the backend bootstrap-protection 403 path.
        PlantProcess__Auth__BootstrapAdminUser: "bootstrap-admin",
        PlantProcess__Auth__BootstrapAdminPassword:
          "BootstrapAdmin_DoNotUse123!",
        PlantProcess__Auth__BootstrapAdminForcePasswordChange: "true",

        // Real E2E admin user.
        PlantProcess__Auth__Users__0__UserName: e2eUserName,
        PlantProcess__Auth__Users__0__Password: e2ePassword,
        PlantProcess__Auth__Users__0__Role: "Admin",
        PlantProcess__Auth__Users__0__DisplayName: "Playwright E2E Admin",
        PlantProcess__Auth__Users__0__IsBootstrapAdmin: "false",
        PlantProcess__Auth__Users__0__ForcePasswordChangeOnFirstLogin: "false",

        PlantProcess__Auth__Users__1__UserName: "engineer",
        PlantProcess__Auth__Users__1__Password: "Engineer123!",
        PlantProcess__Auth__Users__1__Role: "Engineer",
        PlantProcess__Auth__Users__1__DisplayName: "Playwright Engineer",
        PlantProcess__Auth__Users__1__IsBootstrapAdmin: "false",
        PlantProcess__Auth__Users__1__ForcePasswordChangeOnFirstLogin: "false",

        PlantProcess__Auth__Users__2__UserName: "datamanager",
        PlantProcess__Auth__Users__2__Password: "DataManager123!",
        PlantProcess__Auth__Users__2__Role: "DataManager",
        PlantProcess__Auth__Users__2__DisplayName:
          "Playwright Data Manager",
        PlantProcess__Auth__Users__2__IsBootstrapAdmin: "false",
        PlantProcess__Auth__Users__2__ForcePasswordChangeOnFirstLogin: "false",

        PlantProcess__Auth__Users__3__UserName: "viewer",
        PlantProcess__Auth__Users__3__Password: "Viewer123!",
        PlantProcess__Auth__Users__3__Role: "Viewer",
        PlantProcess__Auth__Users__3__DisplayName: "Playwright Viewer",
        PlantProcess__Auth__Users__3__IsBootstrapAdmin: "false",
        PlantProcess__Auth__Users__3__ForcePasswordChangeOnFirstLogin: "false",

        PPIQ_SMOKE_USERNAME: e2eUserName,
        PPIQ_SMOKE_PASSWORD: e2ePassword,
      },
    },

    {
      command: `npx vite --host ${frontendHost} --port ${frontendPort}`,
      url: frontendBaseUrl,
      timeout: 120_000,
      reuseExistingServer: true,
      env: {
        VITE_API_BASE_URL: apiBaseUrl,
        VITE_HOST: frontendHost,
        VITE_PORT: String(frontendPort),

        // Required by AuthContext auto-bootstrap.
        VITE_SMOKE_USERNAME: e2eUserName,
        VITE_SMOKE_PASSWORD: e2ePassword,

        PPIQ_SMOKE_USERNAME: e2eUserName,
        PPIQ_SMOKE_PASSWORD: e2ePassword,
      },
    },
  ],

  projects: [
    {
      name: "chromium",
      use: {
        ...devices["Desktop Chrome"],
      },
    },
    {
      name: "firefox",
      testMatch: /visual\.spec\.ts$/,
      use: { ...devices["Desktop Firefox"] },
    },
    {
      name: "chromium-tablet",
      testMatch: /visual\.spec\.ts$/,
      use: { ...devices["Desktop Chrome"], viewport: { width: 768, height: 1024 } },
    },
    {
      name: "firefox-tablet",
      testMatch: /visual\.spec\.ts$/,
      use: { ...devices["Desktop Firefox"], viewport: { width: 768, height: 1024 } },
    },  ],
});
