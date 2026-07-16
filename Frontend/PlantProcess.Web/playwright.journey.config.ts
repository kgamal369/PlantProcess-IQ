import { defineConfig, devices } from "@playwright/test";
import baseConfig from "./playwright.config";

export default defineConfig({
  ...baseConfig,
  testDir: "./e2e/journey-certification",
  outputDir: "./test-results/journey-certification/artifacts",
  timeout: 120_000,
  expect: { timeout: 25_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: [
    ["line"],
    ["json", { outputFile: "test-results/journey-certification/playwright.json" }],
    ["html", { outputFolder: "playwright-report-journey", open: "never" }],
  ],
  projects: [
    {
      name: "journey-chromium",
      use: {
        ...devices["Desktop Chrome"],
        viewport: { width: 1440, height: 950 },
      },
    },
  ],
});
