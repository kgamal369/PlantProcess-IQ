// PPIQ T-040. THE GOLDEN GATE CONVERGENCE CONFIGURATION.
//
// NO webServer, DELIBERATELY. The repository's base playwright.config.ts spawns
// its own API with a connection string pointing at ppiq_app. The convergence run
// must observe the presentation database and the API instance already serving
// it, so this configuration starts nothing and the runner refuses if either
// server is missing. A run that quietly started a second backend would produce
// evidence about the wrong installation.
import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.PPIQ_T040_BASE_URL || "http://localhost:5173";

export default defineConfig({
  testDir: "./e2e",
  testMatch: /t040-golden-gate\.spec\.ts$/,
  timeout: 120_000,
  expect: { timeout: 20_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [["list"], ["json", { outputFile: "test-results/t040-convergence.json" }]],
  outputDir: "test-results/t040",
  use: {
    baseURL,
    viewport: { width: 1600, height: 1000 },
    trace: "retain-on-failure",
    video: "off",
    screenshot: "off",
    actionTimeout: 20_000,
    navigationTimeout: 30_000,
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});