import { defineConfig, devices } from "@playwright/test";

// PPIQ Phase-7 responsive + http/https matrix.
// BASE: set PPIQ_WEB_BASE (default http://localhost). For the https leg set
// PPIQ_WEB_BASE_HTTPS (default derived by swapping the scheme).
const BASE = process.env.PPIQ_WEB_BASE || "http://localhost";

export default defineConfig({
  testDir: "./tests/e2e",
  testMatch: /phase7-responsive\.spec\.ts/,
  timeout: 60_000,
  retries: 0,
  reporter: [["list"], ["html", { open: "never", outputFolder: "playwright-report-phase7" }]],
  use: { baseURL: BASE, ignoreHTTPSErrors: false },
  projects: [
    { name: "chromium-375",  use: { ...devices["Desktop Chrome"],  viewport: { width: 375,  height: 800 } } },
    { name: "chromium-768",  use: { ...devices["Desktop Chrome"],  viewport: { width: 768,  height: 1024 } } },
    { name: "chromium-1440", use: { ...devices["Desktop Chrome"],  viewport: { width: 1440, height: 900 } } },
    { name: "webkit-375",    use: { ...devices["Desktop Safari"],  viewport: { width: 375,  height: 800 } } },
    { name: "webkit-768",    use: { ...devices["Desktop Safari"],  viewport: { width: 768,  height: 1024 } } },
    { name: "webkit-1440",   use: { ...devices["Desktop Safari"],  viewport: { width: 1440, height: 900 } } },
  ],
});