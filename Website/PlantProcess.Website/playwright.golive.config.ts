import { defineConfig, devices } from "@playwright/test";
// Chromium-only go-live config pointed at the local static server.
const BASE = process.env.PPIQ_WEB_BASE || "http://127.0.0.1:4173";
export default defineConfig({
  testDir: "./tests/e2e",
  testMatch: /phase7-golive\.spec\.ts/,
  timeout: 60_000,
  retries: 0,
  reporter: [["list"]],
  use: { baseURL: BASE, ...devices["Desktop Chrome"] },
});