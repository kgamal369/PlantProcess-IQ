// ============================================================
// FILE: Frontend/PlantProcess.Web/playwright.phase9.config.ts
//
// Phase 09 UI quality matrix:
// - Reuses the existing backend/frontend webServer from playwright.config.ts.
// - Expands browser coverage to Chromium / Firefox / WebKit.
// - Keeps tests deterministic and serial by default.
// ============================================================

import { defineConfig, devices } from "@playwright/test";
import baseConfig from "./playwright.config";

export default defineConfig({
  ...baseConfig,
  timeout: 100_000,
  expect: {
    timeout: 20_000,
  },
  fullyParallel: false,
  workers: process.env.CI ? 2 : 1,
  projects: [
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"] },
    },
    {
      name: "firefox",
      use: { ...devices["Desktop Firefox"] },
    },
    {
      name: "webkit",
      use: { ...devices["Desktop Safari"] },
    },
  ],
});