import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  timeout: 90_000,
  expect: {
    timeout: 15_000
  },
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  reporter: [
    ["list"],
    ["html", { outputFolder: "playwright-report", open: "never" }]
  ],
  use: {
    baseURL: "http://localhost:5173",
    trace: "on-first-retry",
    screenshot: "only-on-failure",
    video: "retain-on-failure"
  },
  webServer: [
    {
      command: "dotnet run --project ../../Backend/PlantProcess.Api/PlantProcess.Api.csproj --urls http://localhost:5063",
      url: "http://localhost:5063/health",
      timeout: 120_000,
      reuseExistingServer: !process.env.CI,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: "http://localhost:5063",
        PLANTPROCESS_ALLOWED_ORIGINS: "http://localhost:5173",
        ConnectionStrings__PlantProcessDb: "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=plantprocess123"
      }
    },
    {
      command: "npx vite --host 127.0.0.1 --port 5173",
      url: "http://localhost:5173",
      timeout: 120_000,
      reuseExistingServer: !process.env.CI
    }
  ],
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] }
    }
  ]
});
