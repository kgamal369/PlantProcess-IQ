import { defineConfig, devices } from "@playwright/test";

const isCi = !!process.env.CI;

const baseURL =
  process.env.PLAYWRIGHT_BASE_URL ||
  process.env.VITE_APP_BASE_URL ||
  "http://localhost:5173";

const apiURL =
  process.env.PLAYWRIGHT_API_URL ||
  process.env.VITE_API_BASE_URL ||
  "http://localhost:5063";

export default defineConfig({
  testDir: "./e2e",
  timeout: 90_000,
  expect: {
    timeout: 15_000
  },
  fullyParallel: false,
  retries: isCi ? 1 : 0,
  reporter: [
    ["list"],
    ["html", { outputFolder: "playwright-report", open: "never" }]
  ],
  use: {
    baseURL,
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure"
  },
  webServer: isCi
    ? undefined
    : [
        {
          command:
            "dotnet run --project ../../Backend/PlantProcess.Api/PlantProcess.Api.csproj --urls http://localhost:5063",
          url: `${apiURL}/health`,
          timeout: 120_000,
          reuseExistingServer: true,
          env: {
            ASPNETCORE_ENVIRONMENT: "Development",
            ASPNETCORE_URLS: "http://localhost:5063",
            PLANTPROCESS_ALLOWED_ORIGINS: "http://localhost:5173",
            ConnectionStrings__PlantProcessDb:
              "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123",
            "PlantProcess__Auth__BootstrapAdminUser": "admin",
            "PlantProcess__Auth__BootstrapAdminPassword": "ChangeMe123!",
            "PlantProcess__Auth__SigningKey":
              "SuperSecretPlaywrightKeyThatIsAtLeast32Bytes!!",
            "PlantProcess__Auth__Issuer": "PlantProcessIQ",
            "PlantProcess__Auth__Audience": "PlantProcessIQ.Client"
          }
        },
        {
          command: "npm run dev -- --host 0.0.0.0 --port 5173",
          url: baseURL,
          timeout: 120_000,
          reuseExistingServer: true
        }
      ],
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] }
    }
  ]
});