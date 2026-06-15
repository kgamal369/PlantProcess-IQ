import { defineConfig, devices } from "@playwright/test";
import baseConfig from "./playwright.config";

export default defineConfig({
  ...baseConfig,
  testDir: "./e2e",
  testMatch: /ppiq-v1-readiness-dry-run\.spec\.ts/,
  retries: 0,
  reporter: [["list"], ["html", { open: "never", outputFolder: "playwright-report-readiness" }]],
  use: {
    ...baseConfig.use,
    baseURL: process.env.PPIQ_APP_HTTP_URL || "http://127.0.0.1:5173",
    video: "on",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [{ name: "chromium-headed-proof", use: { ...devices["Desktop Chrome"] } }],
});